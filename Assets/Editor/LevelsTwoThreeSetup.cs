using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú  Juego > Preparar Nivel2 y Nivel3
///  - Orden del juego: Tutorial, Nivel1, Nivel2, Nivel3.
///  - Luces de antorchas y trampas también en Nivel3.
///  - Sonidos (incluye el del rodillo de púas).
/// Las trampas y puertas de los niveles se montan solas al jugar (LevelSetup).
/// </summary>
public static class LevelsTwoThreeSetup
{
    [MenuItem("Juego/Preparar Nivel2 y Nivel3")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string current = EditorSceneManager.GetActiveScene().path;

        string[] order =
        {
            "Assets/00_Scenes/Tutorial.unity",
            "Assets/00_Scenes/Nivel1.unity",
            "Assets/00_Scenes/Nivel2.unity",
            "Assets/00_Scenes/Nivel3.unity",
        };
        var list = order.Where(System.IO.File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        list.AddRange(EditorBuildSettings.scenes.Where(s => !order.Contains(s.path)));
        EditorBuildSettings.scenes = list.ToArray();

        SoundSetup.Run();
        DungeonLightingSetup.Apply();

        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        System.IO.File.WriteAllText("informe_niveles.txt",
            string.Join("\n", EditorBuildSettings.scenes.Select(s => s.path + (s.enabled ? "" : " (off)"))));
        Debug.Log("Nivel2 y Nivel3 preparados");
    }
}
