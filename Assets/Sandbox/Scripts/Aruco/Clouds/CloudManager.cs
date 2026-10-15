using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox.Aruco.Clouds
{
    /// <summary>
    /// Cree, place et fait vivre les nuages demandes par les marqueurs de
    /// precipitations.
    ///
    /// Regles tenues ici (cf. fiche Notion "Nuages declenches par les marqueurs
    /// de precipitations") :
    ///  - une carte doit rester STABLE dans la meme cellule pendant
    ///    `placementValidationDelay` avant de reserver une zone : une carte en
    ///    cours de pose, glissee sur le sable, ne reserve pas la cellule
    ///    traversee ;
    ///  - le nuage ne s'active que `activationDelay` secondes APRES le retrait
    ///    de la carte ;
    ///  - une zone = une cellule de l'HexGrid, meme si la grille est masquee :
    ///    deux cartes dans la meme cellule ne font qu'un nuage ;
    ///  - un nuage verse une quantite d'eau fixe, puis disparait ;
    ///  - au plus `maxClouds` nuages en meme temps.
    ///
    /// Une action ArUco est un asset : elle ne peut ni attendre ni tenir d'etat
    /// de scene. Tout ce qui dure dans le temps vit donc ici.
    /// </summary>
    public class CloudManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Sandbox sandbox;
        [SerializeField] private WaterSimulation.WaterSimulation waterSimulation;

        [Tooltip("Sert uniquement a decouper le bac en zones : une cellule = un nuage possible. La grille peut rester masquee.")]
        [SerializeField] private HexGrid hexGrid;

        [Header("Rendu")]
        [SerializeField] private Sprite rainSprite;
        [SerializeField] private Sprite stormSprite;

        [Tooltip("Shader Unlit/CloudSprite : transparence + ZTest Always, pour que le nuage passe devant le relief.")]
        [SerializeField] private Shader cloudShader;

        [Tooltip("Hauteur du sprite au-dessus du plan du Sandbox, en unites monde.")]
        [SerializeField] private float cloudHeight = 40f;

        [Header("Comportement")]
        [Tooltip("Duree pendant laquelle la carte doit rester dans la meme cellule pour que la zone soit reservee. Evite qu'une carte en cours de pose ne reserve une cellule au passage.")]
        [SerializeField] private float placementValidationDelay = 1f;

        [Tooltip("Delai entre le retrait de la carte et l'activation du nuage. S'ajoute au delai de tolerance du routeur (ArucoMarkerRouter.lostAfterSeconds, 1 s), qui court avant.")]
        [SerializeField] private float activationDelay = 1.5f;

        [Tooltip("Duree sans revoir une carte avant de la considerer retiree. Doit rester NETTEMENT plus grand que l'intervalle entre deux detections du serveur : c'est la tolerance aux trous de detection. Trop court, une carte posee bien a plat voit son decompte de stabilite repartir de zero a chaque image manquee, et ne declenche jamais de nuage.")]
        [SerializeField] private float markerLostDelay = 2.5f;

        [Tooltip("Nombre de detections consecutives dans une AUTRE cellule avant de considerer que la carte a vraiment ete deplacee. 1 = aucune tolerance. Une carte posee a cheval sur une bordure saute d'une cellule a l'autre au gre du bruit de detection : sans cette confirmation, son decompte repart a zero sans arret.")]
        [Range(1, 5)]
        [SerializeField] private int zoneChangeConfirmations = 2;

        [Header("Position du nuage dans sa cellule")]
        [Tooltip("0 = le nuage se pose au centre de la cellule, 1 = exactement sur la carte. Entre les deux, il suit la carte sans quitter sa cellule.")]
        [Range(0f, 1f)]
        [SerializeField] private float markerPull = 0.6f;

        [Tooltip("Ecart maximal au centre de la cellule, en fraction du rayon interieur de l'hexagone. Garantit qu'un nuage reste visuellement dans SA cellule, meme si la carte est posee tout au bord.")]
        [Range(0f, 1f)]
        [SerializeField] private float maxOffsetRatio = 0.5f;

        [Tooltip("Nombre maximal de nuages simultanes.")]
        [SerializeField] private int maxClouds = 3;

        [Header("Pluie (a regler pendant les tests)")]
        [Tooltip("Gouttes versees par seconde pendant que le nuage est actif.")]
        [SerializeField] private float dropsPerSecond = 6f;

        [Tooltip("Reserve d'eau d'un nuage, en nombre total de gouttes. Une fois versee, le nuage disparait.")]
        [SerializeField] private int waterReserve = 120;

        [Tooltip("Rayon d'arrosage autour du centre du nuage, en unites monde.")]
        [SerializeField] private float rainRadius = 8f;

        [Tooltip("Multiplicateur applique par la variante orage : pluie plus dense, reserve epuisee plus vite.")]
        [SerializeField] private float stormIntensityFactor = 2f;

        [Tooltip("Journalise les etapes dans la console : validation, activation, epuisement.")]
        [SerializeField] private bool logEvents = true;

        /// <summary>
        /// Une carte physique suivie sur le bac. Plusieurs cartes peuvent porter
        /// le MEME identifiant : chacune a donc son propre suivi, et c'est la
        /// proximite avec la position de l'image precedente qui dit laquelle est
        /// laquelle.
        /// </summary>
        private class PendingPlacement
        {
            public int MarkerId;
            public HexCell Cell;
            public Vector3 Position;

            // Temps CUMULE passe dans la zone courante, et non l'instant
            // d'entree : une detection manquee ne doit pas annuler ce qui a
            // deja ete acquis. C'est la difference entre "vue sans
            // interruption pendant 1 s", que le serveur ne garantit pas, et
            // "restee la 1 s", qui est ce qu'on veut verifier.
            public float StableTime;

            public float LastSeenTime;          // derniere image ou elle a ete vue
            public int ForeignSightings;        // detections d'affilee dans une autre cellule
            public ActiveCloud ReservedCloud;   // non nul une fois la pose validee
        }

        /// <summary>Un nuage, de la reservation a l'epuisement.</summary>
        private class ActiveCloud
        {
            public HexCell Cell;                 // zone occupee (null sans HexGrid)
            public Vector3 Position;             // centre, en coordonnees monde
            public SandboxCloud.CloudKind Kind;
            public bool MarkerStillPresent;
            public float ActivationTime;
            public bool Active;
            public float DropsRemaining;
            public float DropAccumulator;
            public SandboxCloud Visual;
        }

        private readonly List<PendingPlacement> placements = new List<PendingPlacement>();
        private readonly List<ActiveCloud> clouds = new List<ActiveCloud>();
        private Material cloudMaterial;

        // Nombre de cartes suivies par identifiant, pour ne journaliser qu'aux
        // changements (une carte posee, une carte retiree).
        private readonly Dictionary<int, int> lastLoggedCounts = new Dictionary<int, int>();

        /// <summary>
        /// Appele a chaque detection d'une carte de precipitations, apparition
        /// comme maintien. La zone n'est reservee qu'une fois la carte restee
        /// assez longtemps dans la meme cellule.
        /// </summary>
        public void OnPrecipitationMarkerSeen(int markerId, Vector3 worldPosition, SandboxCloud.CloudKind kind)
        {
            HexCell cell = CellAt(worldPosition);
            PendingPlacement placement = MatchPlacement(markerId, cell, worldPosition);

            if (placement == null)
            {
                // Aucune carte connue a cet endroit : c'en est une nouvelle, meme
                // si une autre carte porte deja le meme identifiant ailleurs.
                placement = new PendingPlacement
                {
                    MarkerId = markerId,
                    Cell = cell,
                    Position = worldPosition,
                    LastSeenTime = Time.time
                };
                placements.Add(placement);

                // Si un nuage attendait justement de s'activer ici, c'est que
                // cette carte n'etait pas partie : juste perdue de vue le temps
                // de quelques images. Elle reprend sa reservation sans refaire
                // le decompte de stabilite, faute de quoi un simple trou de
                // detection declenche la pluie alors que la carte est encore
                // posee sur le sable.
                AdoptReservedCloud(placement, cell, worldPosition, kind);

                LogCounts();
                return;
            }

            // Le temps credite est celui reellement ecoule depuis la derniere
            // detection, borne par la tolerance aux trous : la carte compte
            // comme presente pendant le trou, pas au-dela.
            float elapsed = Mathf.Min(Time.time - placement.LastSeenTime, markerLostDelay);
            placement.LastSeenTime = Time.time;

            if (!SameZone(placement.Cell, placement.Position, cell, worldPosition))
            {
                placement.ForeignSightings++;

                // Pas encore assez de detections pour y croire : c'est
                // probablement le bruit d'une carte posee sur une bordure. On
                // garde la zone, et le decompte continue.
                if (placement.ForeignSightings < zoneChangeConfirmations)
                {
                    placement.StableTime += elapsed;
                    TryReserve(placement, kind);
                    return;
                }

                // Deplacement confirme : le decompte repart, et une eventuelle
                // reservation faite au mauvais endroit est annulee.
                CancelReservation(placement);
                placement.Cell = cell;
                placement.Position = worldPosition;
                placement.StableTime = 0f;
                placement.ForeignSightings = 0;
                return;
            }

            placement.ForeignSightings = 0;
            placement.StableTime += elapsed;
            placement.Position = worldPosition;

            TryReserve(placement, kind);
        }

        /// <summary>
        /// Reserve la zone si la carte y est restee assez longtemps. Quand la
        /// reservation existe deja, la position du nuage suit la carte : c'est
        /// la ou elle se trouvait en partant qui compte, pas la ou elle a ete
        /// posee la premiere fois.
        /// </summary>
        private void TryReserve(PendingPlacement placement, SandboxCloud.CloudKind kind)
        {
            if (placement.ReservedCloud != null)
            {
                MoveCloudWithMarker(placement);
                return;
            }

            if (placement.StableTime < placementValidationDelay) return;

            placement.ReservedCloud = Reserve(placement.Cell, placement.Position, kind);
        }

        /// <summary>
        /// Rattache une carte reapparue au nuage deja reserve sur sa zone, tant
        /// que celui-ci n'a pas commence a pleuvoir. Le decompte de stabilite
        /// est considere comme acquis : la carte avait deja valide sa pose.
        /// </summary>
        private void AdoptReservedCloud(PendingPlacement placement, HexCell cell,
                                        Vector3 worldPosition, SandboxCloud.CloudKind kind)
        {
            ActiveCloud cloud = FindCloud(cell, worldPosition);
            if (cloud == null || cloud.Active || cloud.MarkerStillPresent) return;

            cloud.MarkerStillPresent = true;
            cloud.Kind = kind;
            placement.ReservedCloud = cloud;
            placement.StableTime = placementValidationDelay;

            MoveCloudWithMarker(placement);

            if (logEvents) Debug.Log("CloudManager : carte revue, activation du nuage suspendue.", this);
        }

        /// <summary>
        /// Recale un nuage encore en attente sur la position courante de sa
        /// carte, sans quitter la cellule reservee.
        /// </summary>
        private void MoveCloudWithMarker(PendingPlacement placement)
        {
            ActiveCloud cloud = placement.ReservedCloud;
            if (cloud == null || cloud.Active) return;

            cloud.Position = CloudPositionFor(cloud.Cell, placement.Position);
        }

        /// <summary>
        /// Nombre de cartes de cet identifiant actuellement suivies sur le bac.
        /// Plusieurs cartes peuvent porter le meme identifiant.
        /// </summary>
        public int CountMarkersInSandbox(int markerId)
        {
            int count = 0;
            foreach (PendingPlacement placement in placements)
            {
                if (placement.MarkerId == markerId) count++;
            }
            return count;
        }

        /// <summary>
        /// Retrouve la carte deja suivie qui correspond a cette detection : la
        /// plus proche, parmi celles du meme identifiant, dans un rayon d'une
        /// cellule. Sans ce rapprochement, deux cartes de meme identifiant
        /// seraient confondues et paraitraient sauter de l'une a l'autre a
        /// chaque rafraichissement.
        /// </summary>
        private PendingPlacement MatchPlacement(int markerId, HexCell cell, Vector3 worldPosition)
        {
            PendingPlacement best = null;
            float bestDistance = float.MaxValue;
            float maxDistance = HexMetrics.innerRadius * 2f;

            foreach (PendingPlacement placement in placements)
            {
                if (placement.MarkerId != markerId) continue;

                // Meme cellule : c'est la meme carte, sans discussion.
                if (cell != null && placement.Cell == cell) return placement;

                float distance = Vector2.Distance(
                    new Vector2(placement.Position.x, placement.Position.y),
                    new Vector2(worldPosition.x, worldPosition.y));

                if (distance < maxDistance && distance < bestDistance)
                {
                    best = placement;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Signal du routeur : plus AUCUNE carte de cet identifiant n'est vue.
        /// Les cartes encore suivies sont donc parties, mais le suivi par
        /// position (cf. ExpireLostPlacements) sait deja gerer le cas d'une
        /// seule carte retiree parmi plusieurs.
        /// </summary>
        public void OnPrecipitationMarkerRemoved(int markerId)
        {
            for (int i = placements.Count - 1; i >= 0; i--)
            {
                if (placements[i].MarkerId != markerId) continue;

                // Une carte dont la pose n'est PAS encore validee n'est pas
                // liberee ici : le routeur signale seulement qu'aucune carte de
                // cet identifiant n'est visible a cet instant, ce qui arrive
                // aussi quand la detection decroche. La laisser expirer sur son
                // propre delai (markerLostDelay) lui laisse le temps de
                // reapparaitre et de finir son decompte. Si elle a vraiment
                // quitte le bac, elle expirera une seconde plus tard et il ne
                // se passera rien - aucun effet de bord.
                if (placements[i].ReservedCloud == null) continue;

                ReleasePlacement(placements[i], i);
            }
            LogCounts();
        }

        /// <summary>
        /// Une carte n'a pas ete revue depuis assez longtemps : elle a quitte le
        /// bac. Si sa pose avait ete validee, son nuage s'activera apres le
        /// delai ; sinon il ne se passe rien.
        /// </summary>
        private void ReleasePlacement(PendingPlacement placement, int index)
        {
            placements.RemoveAt(index);

            ActiveCloud cloud = placement.ReservedCloud;
            if (cloud == null)
            {
                if (logEvents) Debug.Log("CloudManager : carte retiree avant validation, aucun nuage.", this);
                return;
            }

            cloud.MarkerStillPresent = false;
            cloud.ActivationTime = Time.time + activationDelay;

            if (logEvents) Debug.Log($"CloudManager : nuage {cloud.Kind} dans {activationDelay}s.", this);
        }

        /// <summary>
        /// Retire les cartes qui n'ont pas ete revues depuis `markerLostDelay`.
        /// Indispensable quand plusieurs cartes partagent un identifiant : le
        /// routeur ne signale la disparition que lorsque la DERNIERE a quitte le
        /// bac.
        /// </summary>
        private void ExpireLostPlacements()
        {
            bool changed = false;

            for (int i = placements.Count - 1; i >= 0; i--)
            {
                if (Time.time - placements[i].LastSeenTime < markerLostDelay) continue;
                ReleasePlacement(placements[i], i);
                changed = true;
            }

            if (changed) LogCounts();
        }

        private void LogCounts()
        {
            if (!logEvents) return;

            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (PendingPlacement placement in placements)
            {
                counts.TryGetValue(placement.MarkerId, out int current);
                counts[placement.MarkerId] = current + 1;
            }

            foreach (KeyValuePair<int, int> entry in counts)
            {
                lastLoggedCounts.TryGetValue(entry.Key, out int previous);
                if (previous == entry.Value) continue;

                lastLoggedCounts[entry.Key] = entry.Value;
                Debug.Log($"CloudManager : {entry.Value} carte(s) ArUco {entry.Key} sur le bac.", this);
            }

            // Identifiants qui ont disparu completement.
            List<int> gone = new List<int>();
            foreach (KeyValuePair<int, int> entry in lastLoggedCounts)
            {
                if (!counts.ContainsKey(entry.Key) && entry.Value != 0) gone.Add(entry.Key);
            }
            foreach (int markerId in gone)
            {
                lastLoggedCounts[markerId] = 0;
                Debug.Log($"CloudManager : plus aucune carte ArUco {markerId} sur le bac.", this);
            }
        }

        private ActiveCloud Reserve(HexCell cell, Vector3 worldPosition, SandboxCloud.CloudKind kind)
        {
            ActiveCloud existing = FindCloud(cell, worldPosition);
            if (existing != null)
            {
                if (existing.Active)
                {
                    // Un nuage pleut deja sur cette zone : la carte est ignoree.
                    // Le rattacher mettrait sa pluie en pause tant que la carte
                    // reste posee, ce qui donne l'impression d'un nuage oublie.
                    if (logEvents) Debug.Log("CloudManager : il pleut deja sur cette zone, carte ignoree.", this);
                    return null;
                }

                // Zone reservee mais pas encore active : la carte reprend la
                // reservation en cours plutot que d'en creer une seconde.
                existing.MarkerStillPresent = true;
                existing.Kind = kind;
                existing.Position = CloudPositionFor(existing.Cell, worldPosition);
                return existing;
            }

            if (clouds.Count >= maxClouds)
            {
                if (logEvents) Debug.Log($"CloudManager : {maxClouds} nuages deja en place, carte ignoree.", this);
                return null;
            }

            ActiveCloud cloud = new ActiveCloud
            {
                Cell = cell,
                Position = CloudPositionFor(cell, worldPosition),
                Kind = kind,
                MarkerStillPresent = true,
                DropsRemaining = waterReserve
            };
            clouds.Add(cloud);

            if (logEvents) Debug.Log($"CloudManager : zone reservee pour un nuage {kind}.", this);
            return cloud;
        }

        /// <summary>
        /// Annule une reservation faite au mauvais endroit, tant que le nuage
        /// n'a pas commence a pleuvoir.
        /// </summary>
        private void CancelReservation(PendingPlacement placement)
        {
            ActiveCloud cloud = placement.ReservedCloud;
            placement.ReservedCloud = null;

            if (cloud == null || cloud.Active) return;

            if (cloud.Visual != null) Destroy(cloud.Visual.gameObject);
            clouds.Remove(cloud);

            if (logEvents) Debug.Log("CloudManager : carte deplacee, reservation annulee.", this);
        }

        void Update()
        {
            ExpireLostPlacements();

            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                ActiveCloud cloud = clouds[i];

                if (cloud.MarkerStillPresent) continue;

                if (!cloud.Active)
                {
                    if (Time.time < cloud.ActivationTime) continue;
                    Activate(cloud);
                }

                Rain(cloud);

                if (cloud.DropsRemaining <= 0f)
                {
                    if (cloud.Visual != null) cloud.Visual.FadeOutAndDestroy();
                    clouds.RemoveAt(i);

                    if (logEvents) Debug.Log($"CloudManager : nuage {cloud.Kind} epuise.", this);
                }
            }
        }

        private void Activate(ActiveCloud cloud)
        {
            cloud.Active = true;

            Sprite sprite = cloud.Kind == SandboxCloud.CloudKind.Orage ? stormSprite : rainSprite;
            if (sprite == null)
            {
                Debug.LogWarning($"CloudManager : aucun sprite assigne pour {cloud.Kind}.", this);
                return;
            }

            GameObject go = new GameObject($"Cloud_{cloud.Kind}");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();

            cloud.Visual = go.AddComponent<SandboxCloud>();
            cloud.Visual.Initialise(sprite, GetCloudMaterial(), cloud.Position, HexMetrics.innerRadius * 2f);
        }

        private void Rain(ActiveCloud cloud)
        {
            if (waterSimulation == null) return;

            float rate = dropsPerSecond * (cloud.Kind == SandboxCloud.CloudKind.Orage ? stormIntensityFactor : 1f);
            cloud.DropAccumulator += rate * Time.deltaTime;

            while (cloud.DropAccumulator >= 1f && cloud.DropsRemaining > 0f)
            {
                cloud.DropAccumulator -= 1f;
                cloud.DropsRemaining -= 1f;

                Vector2 offset = Random.insideUnitCircle * rainRadius;
                Vector3 target = cloud.Position + new Vector3(offset.x, offset.y, 0f);

                // La goutte doit tomber sur la surface du sable, pas dedans.
                float surfaceZ = sandbox != null ? sandbox.GetDepthFromWorldPos(target) : target.z;
                if (surfaceZ >= 0f) target.z = surfaceZ - 5f;

                waterSimulation.DropWater(target);
            }

            if (cloud.Visual != null && waterReserve > 0)
            {
                cloud.Visual.SetFill(cloud.DropsRemaining / waterReserve);
            }
        }

        private HexCell CellAt(Vector3 worldPosition)
        {
            return hexGrid != null && hexGrid.IsReady ? hexGrid.GetCellAtPosition(worldPosition) : null;
        }

        /// <summary>
        /// Deux detections designent-elles la meme zone ? Avec l'HexGrid, c'est
        /// la cellule qui tranche ; sans elle, on retombe sur une distance de
        /// l'ordre d'une cellule.
        ///
        /// La distance est mesuree DANS LE PLAN du bac (x, y) : la position d'un
        /// nuage porte la hauteur d'affichage du sprite, celle d'une carte la
        /// hauteur du sable. Comparer en 3D rendait deux points de la meme
        /// cellule systematiquement "differents", et la reservation etait
        /// annulee puis refaite a chaque image.
        /// </summary>
        private bool SameZone(HexCell cellA, Vector3 positionA, HexCell cellB, Vector3 positionB)
        {
            if (cellA != null || cellB != null) return cellA == cellB;

            Vector2 planeA = new Vector2(positionA.x, positionA.y);
            Vector2 planeB = new Vector2(positionB.x, positionB.y);
            return Vector2.Distance(planeA, planeB) < HexMetrics.innerRadius * 2f;
        }

        private ActiveCloud FindCloud(HexCell cell, Vector3 worldPosition)
        {
            foreach (ActiveCloud cloud in clouds)
            {
                if (SameZone(cloud.Cell, cloud.Position, cell, worldPosition)) return cloud;
            }
            return null;
        }

        /// <summary>
        /// Ou le nuage se pose, et donc ou l'eau tombe : un entre-deux entre le
        /// centre de la cellule et la position reelle de la carte.
        ///
        /// Tout centrer sur la cellule donnait une pluie qui tombe toujours au
        /// meme endroit, sans rapport visible avec la carte posee. Suivre
        /// exactement la carte, a l'inverse, ferait deborder le nuage sur la
        /// cellule voisine et casserait la regle "un nuage par cellule".
        /// `markerPull` dose le suivi, `maxOffsetRatio` garantit qu'on reste
        /// dans l'hexagone.
        ///
        /// La hauteur est fixe, le sprite passant de toute facon devant le
        /// relief.
        /// </summary>
        private Vector3 CloudPositionFor(HexCell cell, Vector3 worldPosition)
        {
            if (cell == null || hexGrid == null)
            {
                // Hors grille : il n'y a pas de cellule a respecter.
                Vector3 loose = worldPosition;
                loose.z = cloudHeight;
                return loose;
            }

            Vector3 centre = hexGrid.transform.TransformPoint(cell.Position);
            Vector2 offset = new Vector2(worldPosition.x - centre.x, worldPosition.y - centre.y) * markerPull;

            // L'echelle de la grille est non uniforme (cf. SandboxHexBridge,
            // qui etire la grille sur l'emprise du bac) : le rayon utile n'est
            // pas le meme sur les deux axes. On ramene l'ecart dans un disque
            // unite pour le limiter, puis on le remet a l'echelle. Le rayon
            // INTERIEUR est pris sur les deux axes : c'est le plus grand cercle
            // entierement contenu dans l'hexagone.
            Vector3 gridScale = hexGrid.transform.localScale;
            float radiusX = Mathf.Max(HexMetrics.innerRadius * Mathf.Abs(gridScale.x) * maxOffsetRatio, 0.0001f);
            float radiusY = Mathf.Max(HexMetrics.innerRadius * Mathf.Abs(gridScale.z) * maxOffsetRatio, 0.0001f);

            Vector2 normalised = new Vector2(offset.x / radiusX, offset.y / radiusY);
            if (normalised.sqrMagnitude > 1f) normalised.Normalize();

            Vector3 position = centre;
            position.x += normalised.x * radiusX;
            position.y += normalised.y * radiusY;
            position.z = cloudHeight;
            return position;
        }

        private Material GetCloudMaterial()
        {
            if (cloudMaterial == null && cloudShader != null)
            {
                cloudMaterial = new Material(cloudShader);
            }
            return cloudMaterial;
        }
    }
}
