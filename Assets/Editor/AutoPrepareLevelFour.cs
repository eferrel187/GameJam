using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Al abrir Nivel4 o pulsar Play, asegura que exista su personaje y camara.</summary>
[InitializeOnLoad]
public static class AutoPrepareLevelFour
{
    const string LevelPath = "Assets/00_Scenes/Nivel4.unity";
    static bool queued;
    static bool resumePlay;
    static bool attempted;
    static double queuedAt;

    static AutoPrepareLevelFour()
    {
        // Solo registra callbacks; espera al editor antes de cargar ningun recurso.
        EditorSceneManager.sceneOpened -= SceneOpened;
        EditorSceneManager.sceneOpened += SceneOpened;
        EditorApplication.playModeStateChanged -= PlayStateChanged;
        EditorApplication.playModeStateChanged += PlayStateChanged;
        EditorApplication.delayCall += CheckCurrentScene;
    }

    static bool IsLevelFour(Scene scene) => scene.IsValid() && scene.isLoaded && scene.path == LevelPath;

    static void CheckCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || queued) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CheckCurrentScene;
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (IsLevelFour(scene) && !LevelFourSetup.IsReadyForPlay(scene) && !attempted) Queue(false);
    }

    static void SceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!IsLevelFour(scene) || queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        attempted = false;
        EditorApplication.delayCall += CheckCurrentScene;
    }

    static void PlayStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!IsLevelFour(scene) || LevelFourSetup.IsReadyForPlay(scene)) return;
        // Cancela esta entrada vacia, prepara en modo edicion y reanuda Play al terminar.
        EditorApplication.isPlaying = false;
        Queue(true);
    }

    static void Queue(bool playAfter)
    {
        resumePlay |= playAfter;
        if (queued) return;
        queued = true;
        queuedAt = EditorApplication.timeSinceStartup;
        EditorApplication.delayCall += Prepare;
    }

    static void Prepare()
    {
        if (!queued) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (EditorApplication.timeSinceStartup - queuedAt < 30d) EditorApplication.delayCall += Prepare;
            else
            {
                queued = resumePlay = false;
                Debug.LogWarning("Nivel4: espera que Unity termine de importar y vuelve a pulsar Play.");
            }
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!IsLevelFour(scene)) { queued = resumePlay = false; return; }
        bool shouldPlay = resumePlay;
        attempted = true;
        try
        {
            if (!LevelFourSetup.IsReadyForPlay(scene)) LevelFourSetup.Run();
            if (!LevelFourSetup.IsReadyForPlay(SceneManager.GetActiveScene()))
            {
                Debug.LogError("Nivel4 no quedo preparado. Revisa el primer error rojo de Console o usa Juego > Preparar Nivel4.");
                return;
            }
            Debug.Log("Nivel4 listo: Player con animaciones, vidas, camara de tercera persona y colision de camara.");
            if (shouldPlay) EditorApplication.delayCall += EnterPreparedLevel;
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
        finally { queued = resumePlay = false; }
    }

    static void EnterPreparedLevel()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            LevelFourSetup.IsReadyForPlay(SceneManager.GetActiveScene())) EditorApplication.isPlaying = true;
    }
}
