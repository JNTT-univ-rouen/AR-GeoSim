using UnityEngine;

/// <summary>
/// Fait le pont entre le Sandbox Kinect (ARSandbox.Sandbox) et une grille
/// hexagonale (HexGrid) : aligne/redimensionne automatiquement le HexGrid
/// sur l'emprise du Sandbox (voir FitHexGridToSandbox), et lit régulièrement
/// la profondeur moyenne sous chaque hexagone dans HexCell.DepthT.
///
/// Le HexGrid reste volontairement plat : les caméras du Sandbox sont
/// toutes orthographiques vue du dessus, donc un relief 3D par hexagone n'y
/// serait pas visible (le relief réel reste porté par le sable physique +
/// les shaders du Sandbox). DepthT est une donnée pure, pas encore utilisée
/// par l'affichage - elle sert de base à de futures propriétés physiques
/// par tuile (ex: pente = écart de profondeur avec les voisins).
///
/// Mise en place :
/// 1. Ajoute ce script sur un GameObject de la scène.
/// 2. Assigne "sandbox" et "hexGrid" dans l'inspecteur.
/// 3. Place le HexGrid en enfant du Sandbox et règle sa rotation à la main
///    pour que son plan sol (X/Z, en repère HexGrid) coïncide avec le plan
///    sol du Sandbox (X/Y, en repère Sandbox) : la position et l'échelle,
///    elles, sont recalculées automatiquement (voir FitHexGridToSandbox).
///
/// Remarque : ce script suppose que le sol du Sandbox est sur le plan
/// local X/Y (comme MESH_XY_STRIDE.x/.y et calibrationDescriptor.DataStart
/// le suggèrent). Si les valeurs de DepthT semblent incohérentes, c'est le
/// signe que cette hypothèse est fausse pour ta scène : essaie d'échanger
/// sandboxLocalPos.y par sandboxLocalPos.z dans UpdateHexDepthData().
/// </summary>
public class SandboxHexBridge : MonoBehaviour
{
    public ARSandbox.Sandbox sandbox;
    public HexGrid hexGrid;

    [Tooltip("Nombre de frames entre deux lectures GPU->CPU (coûteuses)")]
    public int updateEveryNFrames = 10;

    [Header("Auto-alignement / auto-fit")]
    [Tooltip("Recale automatiquement position + échelle du HexGrid sur l'emprise du Sandbox (SandboxDescriptor.MeshStart -> MeshEnd) dès que celui-ci est prêt, et à chaque recalibration.")]
    public bool autoFitOnReady = true;

    [Tooltip("Position du plan de base du HexGrid sur l'axe profondeur du Sandbox : 0 = SandboxDescriptor.MinDepthScaled, 1 = MaxDepthScaled. C'est cet axe (pas 0) qui porte le relief du Sandbox (cf. MESH_Z_SCALE), donc le HexGrid doit être placé dedans pour être visible.")]
    [Range(0f, 1f)]
    public float depthBaselineT = 0.5f;

    [Tooltip("Facteur de couverture du HexGrid par rapport à l'emprise du Sandbox : 1 = taille exacte, 1.1 = 10% plus grand. Centré sur le Sandbox, le surplus déborde symétriquement de chaque côté, pour éviter une bande sans hexagones sur les bords si le calibrage est légèrement plus petit que la zone physique visible.")]
    [Range(1f, 1.5f)]
    public float coverageMargin = 1.1f;

    int frameCounter;
    bool hasFitCurrentCalibration;

    void OnEnable()
    {
        ARSandbox.Sandbox.OnSandboxReady += HandleSandboxReady;
    }

    void OnDisable()
    {
        ARSandbox.Sandbox.OnSandboxReady -= HandleSandboxReady;
    }

    void HandleSandboxReady()
    {
        hasFitCurrentCalibration = false;
        if (autoFitOnReady) FitHexGridToSandbox();
    }

    void Update()
    {
        if (sandbox == null || hexGrid == null) return;
        if (!sandbox.SandboxReady) return;

        // Filet de sécurité si ce composant était désactivé/absent quand
        // OnSandboxReady a été levé (ex: le Sandbox était déjà calibré
        // avant l'activation de cet objet).
        if (autoFitOnReady && !hasFitCurrentCalibration) FitHexGridToSandbox();

        frameCounter++;
        if (frameCounter < updateEveryNFrames) return;
        frameCounter = 0;

        sandbox.RefreshDepthArrayCache();
        UpdateHexDepthData();
    }

    /// <summary>
    /// Repositionne et redimensionne le HexGrid pour que son empreinte au
    /// sol recouvre exactement la zone visible du Sandbox
    /// (SandboxDescriptor.MeshStart -> MeshEnd, en espace local du
    /// Sandbox). Ne touche pas à la rotation du HexGrid : celle-ci doit
    /// déjà être réglée à la main pour que son plan sol (X/Z) coïncide
    /// avec le plan sol du Sandbox (X/Y).
    /// </summary>
    public void FitHexGridToSandbox()
    {
        if (sandbox == null || hexGrid == null || !sandbox.SandboxReady) return;

        ARSandbox.SandboxDescriptor desc = sandbox.GetSandboxDescriptor();
        if (desc == null) return;

        Transform hexTransform = hexGrid.transform;

        if (hexTransform.parent != sandbox.transform)
        {
            hexTransform.SetParent(sandbox.transform, worldPositionStays: false);
        }

        // Emprise brute (non mise à l'échelle) de la grille générée, dans
        // son propre espace local : X/Z = sol, Y = élévation.
        Bounds rawBounds = hexGrid.GetLocalBounds();
        float rawWidth = Mathf.Max(rawBounds.size.x, 0.0001f);
        float rawDepth = Mathf.Max(rawBounds.size.z, 0.0001f);

        // La mise à l'échelle se fait dans l'espace local du HexGrid, donc
        // avant l'application de sa rotation : X/Z restent les axes "sol"
        // quelle que soit la rotation choisie pour l'aligner sur le Sandbox.
        // coverageMargin > 1 fait déborder la grille au-delà de l'emprise
        // calibrée, pour couvrir tout le bac même si le calibrage est un
        // peu plus petit que la zone physique réellement visible.
        Vector3 scale = hexTransform.localScale;
        scale.x = desc.MeshWidth * coverageMargin / rawWidth;
        scale.z = desc.MeshHeight * coverageMargin / rawDepth;
        hexTransform.localScale = scale;

        // Le centre de la grille (mis à l'échelle, puis tourné comme le
        // HexGrid) doit atterrir sur le centre du Sandbox, à (centre X/Y,
        // targetZ) dans l'espace local du Sandbox (= l'espace local du
        // parent de hexTransform) : on centre plutôt que d'ancrer sur le
        // coin min pour que le surplus de coverageMargin déborde de façon
        // symétrique de chaque côté, pas uniquement d'un seul côté.
        // targetZ vient de la profondeur calibrée du Sandbox, pas de 0 :
        // c'est l'axe Z qui porte le relief (MESH_Z_SCALE), donc un HexGrid
        // callé sur Z=0 sort de la zone où le relief (et donc la caméra)
        // est réellement rendu.
        float targetZ = Mathf.Lerp(desc.MinDepthScaled, desc.MaxDepthScaled, depthBaselineT);
        Vector3 rotatedScaledCenter = hexTransform.localRotation * Vector3.Scale(rawBounds.center, scale);
        Vector3 targetCenterInParent = new Vector3(
            (desc.MeshStart.x + desc.MeshEnd.x) * 0.5f,
            (desc.MeshStart.y + desc.MeshEnd.y) * 0.5f,
            targetZ);
        hexTransform.localPosition = targetCenterInParent - rotatedScaledCenter;

        hasFitCurrentCalibration = true;
    }

    // Lit la profondeur moyenne du Sandbox sous chaque hexagone et la stocke
    // dans HexCell.DepthT (donnée pure, cf. commentaire de classe ci-dessus :
    // n'affecte pas le mesh, pas de retriangulation ici).
    void UpdateHexDepthData()
    {
        HexCell[] cells = hexGrid.Cells;
        if (cells == null) return;

        // FitHexGridToSandbox applique une échelle non-uniforme au HexGrid :
        // le rayon d'échantillonnage doit suivre cette échelle, sinon la
        // zone lue sous chaque hexagone ne correspond plus à sa taille réelle.
        Vector3 hexScale = hexGrid.transform.localScale;
        float halfX = HexMetrics.innerRadius * hexScale.x;
        float halfY = HexMetrics.outerRadius * hexScale.z;

        foreach (HexCell cell in cells)
        {
            Vector3 worldPos = hexGrid.transform.TransformPoint(cell.Position);
            Vector3 sandboxLocalPos = sandbox.transform.InverseTransformPoint(worldPos);

            // Emprise approximative de l'hexagone (axes X/Y locaux du sandbox)
            Vector2 min = new Vector2(
                sandboxLocalPos.x - halfX,
                sandboxLocalPos.y - halfY);
            Vector2 max = new Vector2(
                sandboxLocalPos.x + halfX,
                sandboxLocalPos.y + halfY);

            float avgDepth = sandbox.GetAverageDepthInLocalRect(min, max);

            // Profondeur Kinect : petite valeur = proche du capteur = point haut du sandbox
            cell.DepthT = Mathf.InverseLerp(sandbox.MaxDepth, sandbox.MinDepth, avgDepth);
        }
    }
}