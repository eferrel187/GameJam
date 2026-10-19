using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta por código lo que necesita cada nivel de la historia al cargarse
/// (así se puede seguir editando la escena sin romper nada):
///  - Nivel4: Level4Story (llaves, emboscada, trampas de muro, salida animada).
///  - Nivel5: personaje + Level5Library (estanterías, pistas y estatuas).
///  - Nivel6: personaje + Level6Rescue (llaves, cuenta atrás, mano, serpientes, rescate).
/// </summary>
public static class StoryLevels
{
    public static void Install(Scene scene)
    {
        switch (scene.name)
        {
            case "Nivel4":
                Add<Level4Story>(scene, "Historia_Nivel4");
                break;
            case "Nivel5":
                EnsurePlayer(scene, new Vector3(0f, 0f, 1.8f), 0f);
                Add<Level5Library>(scene, "Historia_Nivel5");
                break;
            case "Nivel6":
                EnsurePlayer(scene, new Vector3(0f, 0f, 1.6f), 0f);
                Add<Level6Rescue>(scene, "Historia_Nivel6");
                Add<WalkingOverride>(scene, "Mordida_Walking");
                break;
        }
    }

    static T Add<T>(Scene scene, string name) where T : Component
    {
        GameObject go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<T>();
    }

    /// Si la escena no tiene personaje, pone el del Nivel4 (prefab preparado
    /// por el editor) y apaga las cámaras de vista de la escena.
    static void EnsurePlayer(Scene scene, Vector3 spawn, float yaw)
    {
        GameObject existing = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (GuardPlayerMovement m in root.GetComponentsInChildren<GuardPlayerMovement>(true))
                if (m.name == "Player") existing = m.gameObject;

        if (existing == null)
        {
            GameObject rig = StoryAssets.Get.playerRig;
            if (rig == null)
            {
                Debug.LogError("Falta el jugador de la historia. Usa Juego > Preparar historia (niveles 4-6).");
                return;
            }
            spawn.y = StoryKit.GroundY(spawn, spawn.y) + 0.03f;
            GameObject inst = Object.Instantiate(rig, spawn, Quaternion.Euler(0f, yaw, 0f));
            inst.name = rig.name;
            SceneManager.MoveGameObjectToScene(inst, scene);

            // GuardWalkAnimation lee los parámetros del Animator en Awake; en un
            // prefab recién creado el Animator aún no estaba listo y se quedaba
            // en reposo sin caminar. Se vuelve a crear ya con el Animator activo.
            foreach (GuardWalkAnimation w in inst.GetComponentsInChildren<GuardWalkAnimation>(true))
            {
                GameObject owner = w.gameObject;
                Object.DestroyImmediate(w);
                owner.AddComponent<GuardWalkAnimation>();
            }
        }

        // Solo una cámara: la del jugador.
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
                if (!c.GetComponent<CameraIntroFollow>())
                {
                    c.enabled = false;
                    AudioListener l = c.GetComponent<AudioListener>();
                    if (l) l.enabled = false;
                }
    }
}
