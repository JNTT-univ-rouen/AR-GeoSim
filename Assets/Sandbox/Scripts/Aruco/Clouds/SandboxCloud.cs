using UnityEngine;

namespace ARSandbox.Aruco.Clouds
{
    /// <summary>
    /// Partie visuelle d'un nuage : un sprite pose a plat au-dessus du bac.
    /// Ne contient aucune logique de pluie ni de minuterie, seulement
    /// l'apparence et sa reponse a la reserve d'eau restante.
    ///
    /// Les nuages sont crees par le gestionnaire, pas poses a la main dans la
    /// scene : il n'y a donc pas de prefab a maintenir.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SandboxCloud : MonoBehaviour
    {
        public enum CloudKind { Pluie, Orage }

        [Header("Taille")]
        [Tooltip("Part de la cellule hexagonale occupee par le nuage (1 = largeur complete).")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float cellCoverage = 0.8f;

        [Tooltip("Taille relative quand la reserve d'eau est presque vide : le nuage retrecit en se vidant.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float minScaleRatio = 0.55f;

        [Header("Orientation")]
        [Tooltip("Rotation du sprite autour de l'axe de vue, en degres. Le repere du Sandbox est tourne par rapport a celui de l'image : +90 remet le nuage a l'endroit vu du projecteur.")]
        [SerializeField] private float spriteRotationZ = 90f;

        [Header("Apparition")]
        [Tooltip("Duree du fondu d'apparition et de disparition, en secondes.")]
        [SerializeField] private float fadeDuration = 0.6f;

        private SpriteRenderer spriteRenderer;
        private float baseWorldSize = 1f;
        private float fill = 1f;
        private float alpha;
        private float targetAlpha = 1f;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Prepare le nuage : apparence, taille de reference et position.
        /// `hexWidth` est la largeur de la cellule visee, en unites monde.
        /// </summary>
        public void Initialise(Sprite sprite, Material material, Vector3 worldPosition, float hexWidth)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            spriteRenderer.sprite = sprite;
            if (material != null) spriteRenderer.sharedMaterial = material;

            transform.position = worldPosition;
            transform.localRotation = Quaternion.Euler(0f, 0f, spriteRotationZ);

            baseWorldSize = ComputeScaleFor(hexWidth);

            alpha = 0f;
            targetAlpha = 1f;
            ApplyVisualState();
        }

        /// <summary>
        /// Reserve d'eau restante, de 1 (plein) a 0 (vide). Sert de jauge : le
        /// nuage retrecit a mesure qu'il pleut.
        /// </summary>
        public void SetFill(float normalisedFill)
        {
            fill = Mathf.Clamp01(normalisedFill);
            ApplyVisualState();
        }

        /// <summary>Lance le fondu de disparition. L'objet se detruit a la fin.</summary>
        public void FadeOutAndDestroy()
        {
            targetAlpha = 0f;
        }

        void Update()
        {
            if (fadeDuration <= 0f)
            {
                alpha = targetAlpha;
            }
            else
            {
                alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime / fadeDuration);
            }

            ApplyVisualState();

            if (targetAlpha == 0f && alpha <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyVisualState()
        {
            if (spriteRenderer == null) return;

            float scale = baseWorldSize * Mathf.Lerp(minScaleRatio, 1f, fill);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, spriteRotationZ);

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        /// <summary>
        /// Echelle a appliquer pour que le sprite couvre `cellCoverage` de la
        /// largeur d'une cellule. Le sprite est carre, sa largeur en unites
        /// monde vaut taille en pixels / pixels par unite.
        /// </summary>
        private float ComputeScaleFor(float hexWidth)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return 1f;

            float spriteWorldWidth = spriteRenderer.sprite.rect.width / spriteRenderer.sprite.pixelsPerUnit;
            if (spriteWorldWidth <= 0.0001f) return 1f;

            return hexWidth * cellCoverage / spriteWorldWidth;
        }
    }
}
