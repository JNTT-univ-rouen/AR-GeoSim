using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ARSandbox;
using ARSandbox.WaterSimulation;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;


namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient : MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        public WaterSimulation WaterSimulation;
        public Shader ServerShader;
        private RenderTexture _serverRenderTexture;
        private SandboxDescriptor sandboxDescriptor;

        private GameObject currentMode;
        public ModeSelector ModeHandler;
        private bool isWaterSimulationOn;
        
        private string _sanitizedUrl;
        private bool _running= false;
        private List<string> logMessages = new List<string>();
        
        private bool ServerFrameReceived = false;
        private bool ReadyForNewFrame = true;
        private bool _configSaved = false;
        private UnityWebRequest webRequest;
        private byte[] tempImageData;
        private List<Arucos> tempArcuos;
        
        [Header("Marqueurs ArUco")]
        [Tooltip("Le serveur detecte sur l'image retournee verticalement (np.flipud, cote Python) : la ligne 0 qu'il renvoie correspond alors a la derniere ligne envoyee. Si les marqueurs tombent en miroir haut/bas dans le bac, decocher.")]
        public bool flipMarkerY = true;

        [Tooltip("Decalage en z applique a la position trouvee, comme le fait HandInput pour les gestes de la main : l'effet doit se declencher juste au-dessus du sable, pas dedans.")]
        public float markerDropOffsetZ = -5f;

        //UI Elements
        public TMP_Text requestLog;
        public TMP_InputField  ipInput;
        public TMP_InputField  portInput;
        public TMP_InputField  endpointInput;
        public TMP_Dropdown httpDropdown;
        public Text startStopButtonText;
       

        private void OnEnable()
        {
            currentMode = ModeHandler.CurrentMode;
            if (currentMode == WaterSimulation.gameObject) isWaterSimulationOn = true;
            if (isWaterSimulationOn == false)
            {
                Sandbox.SetSandboxShader(ServerShader);
                Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
            }
            sandboxDescriptor = Sandbox.GetSandboxDescriptor();
            LoadConfig();
        }

        public void ToggleStartStop()
        {
            if (_running)
            {
                Stop();
            }
            else
            {
                Run();
            }
        }
        private void Run()
        {
            requestLog.text = "";
            _sanitizedUrl = ParseSanitizedUrl();
            _running = true;
            _configSaved = false;
            startStopButtonText.text = "Stop";
            
            //reset
            ServerFrameReceived = false;
            ReadyForNewFrame = true;
            webRequest = null;
        }
        
        private void Stop()
        {
            _running = false;
            // Release the RenderTexture when the object is disabled
            if (_serverRenderTexture != null)
            {
                _serverRenderTexture.Release();
                Destroy(_serverRenderTexture);
                _serverRenderTexture = null;
            }
            startStopButtonText.text = "Start";
        }

        private void OnDisable()
        {
            Stop();
            // Release the RenderTexture when the object is disabled
            if (_serverRenderTexture != null)
            {
                _serverRenderTexture.Release();
                Destroy(_serverRenderTexture);
                _serverRenderTexture = null;
            }
            //reset the shader to default
            Sandbox.SetDefaultShader();
        }

        public class ImageResponse
        {
            [JsonProperty("aruco")]
            public List<Arucos> Aruco { get; set; }
            
            [JsonProperty("image")]
            public string Image { get; set; }

            
        }
        public class Arucos
        {
            [JsonProperty("id")]
            public int Id  { get; set; }
            [JsonProperty("position")]
            public List<int> Position { get; set; }
        }
        
        private void Update()
        {
            if (!_running) return;
            
            if (ServerFrameReceived)
            {
                ServerFrameReceived = false;
                ProcessServerFrame();
            }

            if (ReadyForNewFrame)
            {
                ReadyForNewFrame = false;
                switch (endpointInput.text)
                {
                    case "sandbox":
                        SendFramePayload();
                        break;
                    case "test":
                        SendInfraredImage();
                        break;
                }

                 // Start the method without waiting for it to complete
                // Faut que j'ajoute un if => check le endpoint et en fonction j'envoie la fonction correspondante.
            }
            
        }

        private void SendInfraredImage()
        {
            //Faut que je revois dans le programme originel comment ils envoient la photo. Je peux générer la photo, je peux ausssi accéder à la texture2D si besoin (il me semble). Après ça faudra voir ce que je peux en faire sur python
            var infraredTexture = Sandbox.InfraredPicture();

            // Dimensions gardees a part : elles servent aussi dans la reponse,
            // pour retrouver la position monde de chaque marqueur.
            int imageWidth = infraredTexture.width;
            int imageHeight = infraredTexture.height;

            float[] pixelData = new float[infraredTexture.width * infraredTexture.height];
            Color[] pixels = infraredTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixelData[i] = pixels[i].r; // Assuming RHalf stores data in the red channel
            }

            var pixelDataBytes = new byte[pixelData.Length * sizeof(float)];
            Buffer.BlockCopy(pixelData, 0, pixelDataBytes, 0, pixelDataBytes.Length);

            string url = $"{_sanitizedUrl}?width={imageWidth}&height={imageHeight}&minDepth={sandboxDescriptor.MinDepth}&maxDepth={sandboxDescriptor.MaxDepth}";

            UnityWebRequest webRequest = new UnityWebRequest(url, httpDropdown.options[httpDropdown.value].text);
            webRequest.uploadHandler = new UploadHandlerRaw(pixelDataBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            webRequest.SendWebRequest().completed += (AsyncOperation operation) =>
            {
                if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    UserLogError(webRequest.error);
                    Stop();
                }
                else
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    ImageResponse responseData = JsonConvert.DeserializeObject<ImageResponse>(jsonResponse);
                    if (responseData != null && !string.IsNullOrEmpty(responseData.Image))
                    {
                        UserLog(webRequest.responseCode+" - "+webRequest.result);
                        if (!_configSaved) SaveConfig(); // Save the config after the first successful request
                        tempImageData = Convert.FromBase64String(responseData.Image);
                        tempArcuos = responseData.Aruco ?? new List<Arucos>();
                        foreach (var aruco in tempArcuos)
                        {
                            if (TryArucoToWorldPos(aruco, imageHeight, out Vector3 worldPosition))
                            {
                                UserLog($"ArUco {aruco.Id} : pixel ({aruco.Position[0]},{aruco.Position[1]}) -> monde {worldPosition}");
                                WaterSimulation.DropWater(worldPosition);
                            }
                            else
                            {
                                UserLog($"ArUco {aruco.Id} ignore : hors de la zone calibree");
                            }
                        }
                        ServerFrameReceived = true;
                    }
                    else
                    {
                        UserLogError("Image data not found in response");
                        Stop();
                    }
                }
            };

            //Destroy the temporary Texture2D to free up memory
            //Destroy(infraredTexture);
        }
        /// <summary>
        /// Convertit la position d'un marqueur, exprimee en pixels de l'image
        /// envoyee au serveur, en position monde du Sandbox.
        ///
        /// L'image infrarouge envoyee est deja recadree sur la zone calibree
        /// (rawInfraredTex fait exactement calibrationDescriptor.DataSize) : un
        /// de ses pixels est donc directement une position "data", celle que
        /// Sandbox.DataPosToWorldPos sait convertir. Le z vient du relief reel
        /// sous ce point et non d'une valeur fixe, sinon l'effet se declenche
        /// sous le sable ou bien au-dessus du bac.
        ///
        /// Renvoie false si le marqueur tombe hors de la zone calibree : dans
        /// ce cas il n'y a pas de hauteur de sable a lui associer.
        /// </summary>
        private bool TryArucoToWorldPos(Arucos aruco, int imageHeight, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (aruco == null || aruco.Position == null || aruco.Position.Count < 2)
            {
                return false;
            }

            int x = aruco.Position[0];
            int y = flipMarkerY ? imageHeight - 1 - aruco.Position[1] : aruco.Position[1];

            worldPosition = Sandbox.DataPosToWorldPos(new Point(x, y));

            // GetDepthFromWorldPos renvoie -1 quand le point sort du maillage.
            float surfaceZ = Sandbox.GetDepthFromWorldPos(worldPosition);
            if (surfaceZ < 0f)
            {
                return false;
            }

            worldPosition.z = surfaceZ + markerDropOffsetZ;
            return true;
        }

        private void SendFramePayload()
        {
            var renderTexture = Sandbox.CurrentProcessedRT;

            if (renderTexture.format != RenderTextureFormat.RHalf)
            {
                Debug.LogError("Input RenderTexture is not in RHalf format");
                Stop();
                return;
            }

            var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RHalf, false);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;

            float[] pixelData = new float[texture2D.width * texture2D.height];
            Color[] pixels = texture2D.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixelData[i] = pixels[i].r; // Assuming RHalf stores data in the red channel
            }

            var pixelDataBytes = new byte[pixelData.Length * sizeof(float)];
            Buffer.BlockCopy(pixelData, 0, pixelDataBytes, 0, pixelDataBytes.Length);

            string url = $"{_sanitizedUrl}?width={texture2D.width}&height={texture2D.height}&minDepth={sandboxDescriptor.MinDepth}&maxDepth={sandboxDescriptor.MaxDepth}";

            UnityWebRequest webRequest = new UnityWebRequest(url, httpDropdown.options[httpDropdown.value].text);
            webRequest.uploadHandler = new UploadHandlerRaw(pixelDataBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            webRequest.SendWebRequest().completed += (AsyncOperation operation) =>
            {
                if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    UserLogError(webRequest.error);
                    Stop();
                }
                else
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    ImageResponse responseData = JsonConvert.DeserializeObject<ImageResponse>(jsonResponse);
                    if (responseData != null && !string.IsNullOrEmpty(responseData.Image))
                    {
                        UserLog(webRequest.responseCode+" - "+webRequest.result);
                        if (!_configSaved) SaveConfig(); // Save the config after the first successful request
                        tempImageData = Convert.FromBase64String(responseData.Image);
                        ServerFrameReceived = true;
                    }
                    else
                    {
                        UserLogError("Image data not found in response");
                        Stop();
                    }
                }
            };

            // Destroy the temporary Texture2D to free up memory
            Destroy(texture2D);
        }
        
        private async void ProcessServerFrame()
        {
            Texture2D tempTexture = new Texture2D(2, 2);
            tempTexture.LoadImage(tempImageData);

            if (_serverRenderTexture == null)
            {
                _serverRenderTexture = new RenderTexture(tempTexture.width, tempTexture.height, 0, RenderTextureFormat.ARGB32);
                _serverRenderTexture.Create();
            }

            RenderTexture.active = _serverRenderTexture;
            Graphics.Blit(tempTexture, _serverRenderTexture);
            RenderTexture.active = null;
            //Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);

            Destroy(tempTexture);
            ReadyForNewFrame = true;
        }

        private string ParseSanitizedUrl()
        {
            //set and remove whitespace
            string ip = ipInput.text.Replace(" ", "").Replace("\u200B","").Trim();
            string port = portInput.text.Replace(" ", "").Replace("\u200B","").Trim();
            string endpoint = endpointInput.text.Replace(" ", "").Replace("\u200B","").Trim();

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(endpoint))
            {
                UserLogError("IP, Port, or Endpoint is empty");
                
                Stop();
            }
            //remove http://
            if (ip.StartsWith("http://"))
            {
                ip = ip.Substring(7);
            }
            //check if port is a number
            if (!int.TryParse(port, out _))
            {
                UserLogError("Port is not a number");
                Stop();
            }
            //remove / from endpoint
            if (endpoint.StartsWith("/"))
            {
                endpoint = endpoint.Substring(1);
            }
            // remove trailing /
            if (endpoint.EndsWith("/"))
            {
                endpoint = endpoint.Substring(0, endpoint.Length - 1);
            }
            
            return $"http://{ip}:{port}/{endpoint}";
        }
        
        private string GetTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private void AddLogMessage(string message, string color)
        {
            string timestampedMessage = $"[{GetTimestamp()}] {message}";
            if (logMessages.Count >= 12)
            {
                logMessages.RemoveAt(0);
            }
            logMessages.Add($"<color={color}>{timestampedMessage}</color>");
            UpdateLogUI();
        }

        private void UpdateLogUI()
        {
            requestLog.text = string.Join("\n", logMessages);
        }

        private void UserLog(string message)
        {
            Debug.Log(message);
            AddLogMessage(message, "white");
        }

        private void UserLogError(string message)
        {
            Debug.LogError(message);
            AddLogMessage(message, "red");
        }

        private void SaveConfig()
        {
            var config = new SandboxClientConfig
            {
                Ip = ipInput.text,
                Port = portInput.text,
                Endpoint = endpointInput.text,
                HttpMethod = httpDropdown.value
            };
            SandboxClientConfig.SaveConfig(config);
            UserLog("Config saved");
            _configSaved = true;
        }
        
        private void LoadConfig()
        {
            var config = SandboxClientConfig.LoadConfig();
            ipInput.text = config.Ip;
            portInput.text = config.Port;
            endpointInput.text = config.Endpoint;
            httpDropdown.value = config.HttpMethod;
        }

        
    }
}