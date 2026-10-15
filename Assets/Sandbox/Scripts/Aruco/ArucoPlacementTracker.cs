using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// Suit les cartes posees sur le bac et dit quand une pose est valable.
    ///
    /// Deux problemes que ce suivi resout, et qu'on retrouve pour tout effet
    /// declenche par une carte posee :
    ///
    ///  - une carte en cours de pose glisse sur le sable. Sans delai, elle
    ///    declencherait un effet sur chaque cellule traversee. Une carte doit
    ///    donc rester dans la meme cellule pendant `validationDelay` avant
    ///    d'etre consideree posee.
    ///
    ///  - plusieurs cartes peuvent porter le MEME identifiant. Les suivre par
    ///    identifiant les confondrait : leurs positions alterneraient a chaque
    ///    rafraichissement et paraitraient bouger. Chaque detection est donc
    ///    rapprochee de la carte connue la plus proche, dans la limite d'une
    ///    cellule.
    ///
    /// Le suivi se fait par cellule de l'HexGrid quand elle est disponible,
    /// sinon par distance dans le plan du bac.
    /// </summary>
    public class ArucoPlacementTracker
    {
        public class Placement
        {
            public int MarkerId;
            public HexCell Cell;
            public Vector3 Position;
            public float StableSince;
            public float LastSeenTime;
            public bool Validated;

            /// <summary>Libre d'usage pour l'appelant (nuage reserve, etc.).</summary>
            public object Payload;
        }

        private readonly List<Placement> placements = new List<Placement>();
        private readonly List<Placement> lostBuffer = new List<Placement>();

        private readonly float validationDelay;
        private readonly float lostDelay;
        private readonly float zoneRadius;

        public IReadOnlyList<Placement> Placements => placements;

        public ArucoPlacementTracker(float validationDelay, float lostDelay, float zoneRadius)
        {
            this.validationDelay = validationDelay;
            this.lostDelay = lostDelay;
            this.zoneRadius = zoneRadius;
        }

        /// <summary>
        /// A appeler a chaque detection. Renvoie la carte suivie, et met
        /// `justValidated` a vrai a la frame exacte ou sa pose devient valable :
        /// c'est la que l'appelant declenche son effet.
        /// </summary>
        public Placement Report(int markerId, HexCell cell, Vector3 worldPosition, out bool justValidated)
        {
            justValidated = false;

            Placement placement = Match(markerId, cell, worldPosition);
            if (placement == null)
            {
                placement = new Placement
                {
                    MarkerId = markerId,
                    Cell = cell,
                    Position = worldPosition,
                    StableSince = Time.time,
                    LastSeenTime = Time.time
                };
                placements.Add(placement);
                return placement;
            }

            placement.LastSeenTime = Time.time;

            // Changement de zone : la carte bougeait encore, le decompte repart.
            if (!SameZone(placement.Cell, placement.Position, cell, worldPosition))
            {
                placement.Cell = cell;
                placement.Position = worldPosition;
                placement.StableSince = Time.time;
                placement.Validated = false;
                placement.Payload = null;
                return placement;
            }

            placement.Position = worldPosition;

            if (!placement.Validated && Time.time - placement.StableSince >= validationDelay)
            {
                placement.Validated = true;
                justValidated = true;
            }

            return placement;
        }

        /// <summary>
        /// Retire les cartes non revues depuis `lostDelay` et les renvoie. A
        /// appeler depuis Update : le routeur ne signale la disparition que
        /// lorsque la DERNIERE carte d'un identifiant a quitte le bac.
        /// </summary>
        public IReadOnlyList<Placement> CollectLost()
        {
            lostBuffer.Clear();

            for (int i = placements.Count - 1; i >= 0; i--)
            {
                if (Time.time - placements[i].LastSeenTime < lostDelay) continue;

                lostBuffer.Add(placements[i]);
                placements.RemoveAt(i);
            }

            return lostBuffer;
        }

        /// <summary>Nombre de cartes de cet identifiant actuellement sur le bac.</summary>
        public int Count(int markerId)
        {
            int count = 0;
            foreach (Placement placement in placements)
            {
                if (placement.MarkerId == markerId) count++;
            }
            return count;
        }

        private Placement Match(int markerId, HexCell cell, Vector3 worldPosition)
        {
            Placement best = null;
            float bestDistance = float.MaxValue;

            foreach (Placement placement in placements)
            {
                if (placement.MarkerId != markerId) continue;

                if (cell != null && placement.Cell == cell) return placement;

                float distance = PlaneDistance(placement.Position, worldPosition);
                if (distance < zoneRadius && distance < bestDistance)
                {
                    best = placement;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Deux points designent-ils la meme zone ? La distance est mesuree dans
        /// le plan du bac : la hauteur d'une carte et celle d'un effet n'ont
        /// aucune raison de coincider.
        /// </summary>
        private bool SameZone(HexCell cellA, Vector3 positionA, HexCell cellB, Vector3 positionB)
        {
            if (cellA != null || cellB != null) return cellA == cellB;
            return PlaneDistance(positionA, positionB) < zoneRadius;
        }

        private static float PlaneDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
        }
    }
}
