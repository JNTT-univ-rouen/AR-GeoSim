using System.Collections.Generic;
using System.IO;
using ARSandbox.Aruco;
using ARSandbox.Aruco.Actions;
using UnityEditor;
using UnityEngine;

namespace ARSandbox.Aruco.EditorTools
{
    /// <summary>
    /// Cree les assets d'actions et le catalogue a partir du suivi des
    /// marqueurs tenu dans Notion (base "Suivi des marqueurs ArUco").
    ///
    /// Outil de demarrage, pas une source de verite : il ne cree que ce qui
    /// manque et ne touche jamais a un asset existant. Les reglages ajustes a
    /// la main dans l'inspecteur sont donc conserves si on le relance.
    ///
    /// Ajouter un marqueur plus tard peut se faire soit ici (ajouter une ligne
    /// puis relancer), soit directement dans le Project via
    /// Create > Sandbox > ArUco.
    /// </summary>
    public static class ArucoCatalogGenerator
    {
        const string AssetFolder = "Assets/Sandbox/ArucoActions";
        const string CatalogPath = AssetFolder + "/ArucoActionCatalog.asset";

        struct MarkerDefinition
        {
            public int Id;
            public string ObjectName;
            public ArucoActionFamily Family;
            public ArucoActionScope Scope;
            public bool Exclusive;

            /// <summary>Type d'action a creer. LogArucoAction = pas encore implemente.</summary>
            public System.Type ActionType;

            // Renseigne uniquement pour la famille Qualite du sol.
            public bool AbsorptionActive;
            public float AbsorptionDropsPerSecond;
            public bool Persistant;

            /// <summary>Vue du terrain a appliquer EN PLUS, via une action composee.</summary>
            public ReliefViewArucoAction.TerrainView SoilTerrainView;

            // Renseigne uniquement pour la famille Precipitations.
            public Clouds.SandboxCloud.CloudKind CloudKind;

            // Renseigne uniquement pour la famille Lecture relief.
            public ReliefViewArucoAction.TerrainView TerrainView;
            public ReliefViewArucoAction.LabelSetting Labels;
            public string CustomShaderPath;
        }

        static readonly MarkerDefinition[] Markers =
        {
            // Lecture relief.
            // 0 : vue noir et blanc + labels coupes = seules les courbes de niveau restent.
            Relief(0, "Isohypses exclusivement",
                   ReliefViewArucoAction.TerrainView.NoirEtBlanc,
                   ReliefViewArucoAction.LabelSetting.Desactiver),
            // 1 : meme vue, mais avec les nombres d'altitude sur les courbes.
            Relief(1, "Isohypses chiffres",
                   ReliefViewArucoAction.TerrainView.NoirEtBlanc,
                   ReliefViewArucoAction.LabelSetting.Activer),
            // 2 : shader normal = degrade de couleurs selon l'altitude.
            // Labels laisses en l'etat, faute de consigne : a changer dans
            // l'inspecteur ou ici si besoin.
            Relief(2, "Ajout couleur altitudes - multi",
                   ReliefViewArucoAction.TerrainView.Normal,
                   ReliefViewArucoAction.LabelSetting.NeChangeRien),
            // 3 : shader vert existant (celui du menu des saisons).
            Relief(3, "Ajout couleur altitudes - vert",
                   ReliefViewArucoAction.TerrainView.Vert,
                   ReliefViewArucoAction.LabelSetting.NeChangeRien),

            // 4 : camaieu violet, shader cree pour ce marqueur et donc absent du
            // menu des saisons : il est reference directement par l'asset.
            Relief(4, "Ajout couleur altitudes - violet",
                   ReliefViewArucoAction.TerrainView.Personnalise,
                   ReliefViewArucoAction.LabelSetting.NeChangeRien,
                   customShaderPath: "Assets/Sandbox/Shaders/SandboxShaderViolet.shader"),

            // Precipitations : la carte reserve une zone, le nuage s'active
            // quelques secondes apres son retrait. Debit et reserve se reglent
            // une fois pour toutes sur le CloudManager.
            Rain(5, "Pluie", Clouds.SandboxCloud.CloudKind.Pluie),
            Rain(6, "Averse orageuse", Clouds.SandboxCloud.CloudKind.Orage),

            // Occupation du sol : actions locales, la ou l'objet est pose.
            Simple(7, "Artificialisation", ArucoActionFamily.OccupationDuSol, ArucoActionScope.Locale),
            Simple(8, "Vegetalisation", ArucoActionFamily.OccupationDuSol, ArucoActionScope.Locale),
            Simple(9, "Suppression de haies", ArucoActionFamily.OccupationDuSol, ArucoActionScope.Locale),
            Simple(10, "Ajout de haies", ArucoActionFamily.OccupationDuSol, ArucoActionScope.Locale),

            // Qualite du sol : implementee, exclusive (un seul sol a la fois).
            // Valeur du curseur : plus petit = absorption plus rapide.
            // 11 et 13 font deux choses : changer la vue du terrain et regler
            // l'infiltration. Effet persistant, comme la famille Lecture relief.
            SoilView(11, "Sec", ReliefViewArucoAction.TerrainView.SecOrange, dropsPerSecond: 1f),

            Soil(12, "Artificialise", absorptionActive: false, dropsPerSecond: 1f),

            SoilView(13, "Poreux", ReliefViewArucoAction.TerrainView.Vert, dropsPerSecond: 10f),
        };

        static MarkerDefinition Simple(int id, string objectName, ArucoActionFamily family,
                                       ArucoActionScope scope = ArucoActionScope.Globale)
        {
            return new MarkerDefinition
            {
                Id = id,
                ObjectName = objectName,
                Family = family,
                Scope = scope,
                ActionType = typeof(LogArucoAction)
            };
        }

        static MarkerDefinition Rain(int id, string objectName, Clouds.SandboxCloud.CloudKind kind)
        {
            return new MarkerDefinition
            {
                Id = id,
                ObjectName = objectName,
                Family = ArucoActionFamily.Precipitations,
                // Locale : le nuage apparait la ou la carte etait posee.
                Scope = ArucoActionScope.Locale,
                // Pas d'exclusivite : plusieurs nuages peuvent coexister, la
                // limite est tenue par le CloudManager (maxClouds).
                Exclusive = false,
                ActionType = typeof(PrecipitationArucoAction),
                CloudKind = kind
            };
        }

        static MarkerDefinition Relief(int id, string objectName,
                                       ReliefViewArucoAction.TerrainView view,
                                       ReliefViewArucoAction.LabelSetting labels,
                                       string customShaderPath = null,
                                       ArucoActionFamily family = ArucoActionFamily.LectureRelief)
        {
            return new MarkerDefinition
            {
                Id = id,
                ObjectName = objectName,
                Family = family,
                Scope = ArucoActionScope.Globale,
                // Pas d'exclusivite : le changement de vue est persistant, il n'y
                // a donc pas d'action "en cours" a interrompre quand une autre
                // carte de la famille arrive.
                Exclusive = false,
                ActionType = typeof(ReliefViewArucoAction),
                TerrainView = view,
                Labels = labels,
                CustomShaderPath = customShaderPath
            };
        }

        static MarkerDefinition Soil(int id, string objectName, bool absorptionActive, float dropsPerSecond)
        {
            return new MarkerDefinition
            {
                Id = id,
                ObjectName = objectName,
                Family = ArucoActionFamily.QualiteDuSol,
                Scope = ArucoActionScope.Globale,
                Exclusive = true,
                ActionType = typeof(SoilQualityArucoAction),
                AbsorptionActive = absorptionActive,
                AbsorptionDropsPerSecond = dropsPerSecond,
                // L'etat du sol reste en place apres le retrait de la carte :
                // seule une autre carte le change.
                Persistant = true,
                SoilTerrainView = ReliefViewArucoAction.TerrainView.NeChangeRien
            };
        }

        /// <summary>
        /// Qualite du sol qui change AUSSI la vue du terrain : l'asset du
        /// marqueur est alors une action composee, qui enchaine une vue et une
        /// infiltration. Effet persistant, comme la famille Lecture relief.
        /// </summary>
        static MarkerDefinition SoilView(int id, string objectName,
                                         ReliefViewArucoAction.TerrainView view,
                                         float dropsPerSecond)
        {
            return new MarkerDefinition
            {
                Id = id,
                ObjectName = objectName,
                Family = ArucoActionFamily.QualiteDuSol,
                Scope = ArucoActionScope.Globale,
                Exclusive = false,
                ActionType = typeof(CompositeArucoAction),
                AbsorptionActive = true,
                AbsorptionDropsPerSecond = dropsPerSecond,
                Persistant = true,
                SoilTerrainView = view
            };
        }

        [MenuItem("Tools/Sandbox/ArUco/Generer le catalogue et les marqueurs")]
        public static void Generate()
        {
            Directory.CreateDirectory(AssetFolder);

            ArucoActionCatalog catalog = AssetDatabase.LoadAssetAtPath<ArucoActionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArucoActionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            List<ArucoAction> catalogActions = new List<ArucoAction>();
            int created = 0;

            foreach (MarkerDefinition marker in Markers)
            {
                string path = $"{AssetFolder}/Aruco{marker.Id:00}_{Sanitize(marker.ObjectName)}.asset";
                ArucoAction action = AssetDatabase.LoadAssetAtPath<ArucoAction>(path);

                // Le type d'action est decide dans le code : si une action a ete
                // generee avant d'etre implementee (LogArucoAction), elle est
                // remplacee. Les reglages ajustes a la main sur un asset du bon
                // type, eux, ne sont jamais touches.
                if (action != null && action.GetType() != marker.ActionType)
                {
                    Debug.Log($"ArUco {marker.Id} : remplacement de {action.GetType().Name} par {marker.ActionType.Name}");
                    AssetDatabase.DeleteAsset(path);
                    action = null;
                }

                if (action == null)
                {
                    action = (ArucoAction)ScriptableObject.CreateInstance(marker.ActionType);
                    ApplyDefinition(action, marker);
                    AssetDatabase.CreateAsset(action, path);
                    created++;
                }

                catalogActions.Add(action);
            }

            // Le catalogue, lui, est toujours reconstruit : c'est un simple
            // index, rien n'y est saisi a la main.
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty actionsProperty = serializedCatalog.FindProperty("actions");
            actionsProperty.ClearArray();
            for (int i = 0; i < catalogActions.Count; i++)
            {
                actionsProperty.InsertArrayElementAtIndex(i);
                actionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = catalogActions[i];
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"ArUco : {created} action(s) creee(s), catalogue de {catalogActions.Count} marqueurs -> {CatalogPath}", catalog);
            Selection.activeObject = catalog;
        }

        static void ApplyDefinition(ArucoAction action, MarkerDefinition marker)
        {
            SerializedObject serialized = new SerializedObject(action);
            serialized.FindProperty("markerId").intValue = marker.Id;
            serialized.FindProperty("objectName").stringValue = marker.ObjectName;
            serialized.FindProperty("family").enumValueIndex = (int)marker.Family;
            serialized.FindProperty("scope").enumValueIndex = (int)marker.Scope;
            serialized.FindProperty("exclusiveInFamily").boolValue = marker.Exclusive;

            if (marker.ActionType == typeof(SoilQualityArucoAction))
            {
                ApplySoilSettings(serialized, marker);
            }
            else if (marker.ActionType == typeof(CompositeArucoAction))
            {
                ApplyCompositeSettings(serialized, action, marker);
            }
            else if (marker.ActionType == typeof(PrecipitationArucoAction))
            {
                serialized.FindProperty("cloudKind").enumValueIndex = (int)marker.CloudKind;
            }
            else if (marker.ActionType == typeof(ReliefViewArucoAction))
            {
                serialized.FindProperty("terrainView").intValue = (int)marker.TerrainView;
                serialized.FindProperty("labels").enumValueIndex = (int)marker.Labels;

                if (!string.IsNullOrEmpty(marker.CustomShaderPath))
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(marker.CustomShaderPath);
                    if (shader == null)
                    {
                        Debug.LogError($"ArUco {marker.Id} : shader introuvable a {marker.CustomShaderPath}");
                    }
                    serialized.FindProperty("customTerrainShader").objectReferenceValue = shader;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplySoilSettings(SerializedObject serialized, MarkerDefinition marker)
        {
            serialized.FindProperty("absorptionActive").boolValue = marker.AbsorptionActive;
            serialized.FindProperty("absorptionDropsPerSecond").floatValue = marker.AbsorptionDropsPerSecond;
            serialized.FindProperty("persistant").boolValue = marker.Persistant;
        }

        /// <summary>
        /// Cree les deux actions filles (vue du terrain, infiltration) dans un
        /// sous-dossier, et les range dans l'action composee du marqueur.
        /// </summary>
        static void ApplyCompositeSettings(SerializedObject serialized, ArucoAction parent, MarkerDefinition marker)
        {
            string folder = AssetFolder + "/Composants";
            Directory.CreateDirectory(folder);

            string baseName = $"Aruco{marker.Id:00}_{Sanitize(marker.ObjectName)}";

            ReliefViewArucoAction view = ScriptableObject.CreateInstance<ReliefViewArucoAction>();
            SerializedObject viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("markerId").intValue = marker.Id;
            viewSerialized.FindProperty("objectName").stringValue = marker.ObjectName + " (vue)";
            viewSerialized.FindProperty("family").enumValueIndex = (int)marker.Family;
            viewSerialized.FindProperty("terrainView").intValue = (int)marker.SoilTerrainView;
            viewSerialized.FindProperty("labels").enumValueIndex = (int)ReliefViewArucoAction.LabelSetting.NeChangeRien;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(view, $"{folder}/{baseName}_Vue.asset");

            SoilQualityArucoAction soil = ScriptableObject.CreateInstance<SoilQualityArucoAction>();
            SerializedObject soilSerialized = new SerializedObject(soil);
            soilSerialized.FindProperty("markerId").intValue = marker.Id;
            soilSerialized.FindProperty("objectName").stringValue = marker.ObjectName + " (infiltration)";
            soilSerialized.FindProperty("family").enumValueIndex = (int)marker.Family;
            ApplySoilSettings(soilSerialized, marker);
            soilSerialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(soil, $"{folder}/{baseName}_Infiltration.asset");

            SerializedProperty children = serialized.FindProperty("actions");
            children.ClearArray();
            children.InsertArrayElementAtIndex(0);
            children.GetArrayElementAtIndex(0).objectReferenceValue = view;
            children.InsertArrayElementAtIndex(1);
            children.GetArrayElementAtIndex(1).objectReferenceValue = soil;
        }

        static string Sanitize(string value)
        {
            return value.Replace(" ", "").Replace("-", "").Replace("'", "");
        }
    }
}
