using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Solo las antorchas más cercanas a la cámara proyectan sombras (con
/// muchas antorchas las sombras perdían resolución y el juego iba más lento).
/// Lo instala LevelSetup en cada escena.
/// </summary>
public class TorchShadowBudget : MonoBehaviour
{
    public int maxShadowed = 6;
    public float interval = 0.4f;

    readonly List<Light> lights = new List<Light>();
    float next;

    void Start()
    {
        foreach (TorchFlicker t in FindObjectsByType<TorchFlicker>(FindObjectsSortMode.None))
        {
            Light l = t.GetComponent<Light>();
            if (l != null && l.shadows != LightShadows.None) lights.Add(l);
        }
        if (lights.Count <= maxShadowed) enabled = false;
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + interval;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;

        int i = 0;
        foreach (Light l in lights.Where(l => l != null).OrderBy(l => (l.transform.position - p).sqrMagnitude))
            l.shadows = i++ < maxShadowed ? LightShadows.Soft : LightShadows.None;
    }
}
