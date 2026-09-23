using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build en ligne de commande, pour produire une version sans passer par
/// l'interface de l'editeur (release GitHub, machine de demo...).
///
/// Unity doit etre ferme : il verrouille le projet tant qu'il est ouvert.
///
///   "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe" ^
///     -quit -batchmode -nographics ^
///     -projectPath "C:\Users\sandbox\Documents\dev\AR-GeoSim - Unity6" ^
///     -executeMethod BuildScript.BuildWindows64 ^
///     -logFile build.log
///
/// Les scenes construites sont celles cochees dans File > Build Settings.
/// Sortie : Builds/Windows64/AR-GeoSim.exe (dossier ignore par git).
/// </summary>
public static class BuildScript
{
    const string OutputDirectory = "Builds/Windows64";
    const string ExecutableName = "AR-GeoSim.exe";

    [MenuItem("Tools/Sandbox/Build Windows 64", false, 100)]
    public static void BuildWindows64()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Fail("Aucune scene activee dans les Build Settings : rien a construire.");
            return;
        }

        Debug.Log($"Build : {scenes.Length} scene(s) -> {OutputDirectory}/{ExecutableName}\n" +
                  string.Join("\n", scenes));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"{OutputDirectory}/{ExecutableName}",
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build reussi : {summary.totalSize / (1024 * 1024)} Mo en " +
                      $"{summary.totalTime.TotalMinutes:F1} min -> {summary.outputPath}");

            // En batchmode, -quit suffit ; en interactif, ne pas fermer l'editeur.
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Fail($"Build {summary.result} : {summary.totalErrors} erreur(s).");
        }
    }

    static void Fail(string message)
    {
        Debug.LogError(message);

        // Code de sortie non nul : le script appelant voit l'echec.
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
