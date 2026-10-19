using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restablece trampas comunes al reaparecer.</summary>
public static class StoryReset
{
    /// Vuelve a abrir los cepos que se cerraron (se quita la versión cerrada).
    public static void BearTraps(Scene scene)
    {
        foreach (KillZone kz in Object.FindObjectsByType<KillZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!kz.bearTrap || kz.gameObject.scene != scene) continue;
            if (!kz.gameObject.activeSelf || !kz.armed)
            {
                GameObject closed = GameObject.Find("BearTrap_Closed_" + kz.gameObject.name);
                if (closed)
                {
                    closed.name = "BearTrap_Closed_Quitado";
                    Object.Destroy(closed);
                }
                kz.gameObject.SetActive(true);
                kz.armed = true;
            }
        }
    }
}
