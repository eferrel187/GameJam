using UnityEngine;

/// <summary>
/// Púas fijas o cepos: quitan una vida al tocarlos (comprobación por volumen,
/// sin depender de colisiones). Lo coloca LevelSetup.
/// </summary>
public class KillZone : MonoBehaviour
{
    public bool bearTrap;
    [Tooltip("Margen hacia dentro (m) para que rozar el borde no mate.")]
    public float inset = 0.12f;
    [Tooltip("Desactivado = púas escondidas (se puede pasar).")]
    public bool armed = true;

    Renderer[] renderers;
    Bounds zone;
    Transform player;
    CharacterController controller;
    GuardHealth health;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { enabled = false; return; }

        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            player = p.transform;
            controller = p.GetComponent<CharacterController>();
            health = p.GetComponent<GuardHealth>();
        }
    }

    void Update()
    {
        if (!armed || health == null || controller == null || health.IsKnockedDown) return;

        // Se mide cada vez: las púas pueden subir y bajar.
        zone = renderers[0].bounds;
        foreach (Renderer r in renderers) zone.Encapsulate(r.bounds);
        // Más estrecha por los lados (rozar el borde no mata) y un poco más
        // alta por arriba (pisar las puntas sí mata).
        Vector3 shrink = new Vector3(inset, 0f, inset) * 2f;
        zone.size = Vector3.Max(zone.size - shrink, Vector3.one * 0.05f);
        zone.max += Vector3.up * 0.15f;

        if (!controller.bounds.Intersects(zone)) return;

        int before = health.Lives;
        if (bearTrap) health.Hit();
        else health.HitBySpikes();
        if (health.Lives < before)
        {
            GameAudio.TrampaCepo(zone.center);
            if (bearTrap) CloseBearTrap();
        }
    }

    void CloseBearTrap()
    {
        armed = false;
        LevelVisualAssets assets = LevelVisualAssets.Load();
        if (assets && assets.closedBearTrap)
        {
            GameObject closed = Instantiate(assets.closedBearTrap);
            closed.name = "BearTrap_Closed_" + gameObject.name;
            closed.transform.SetPositionAndRotation(transform.position, transform.rotation);
            closed.transform.localScale = transform.lossyScale;
            Bounds b = LevelVisualAssets.Measure(closed);
            // Las dos mallas tienen pivotes distintos: alinea centro y base.
            closed.transform.position += new Vector3(zone.center.x - b.center.x,
                LevelVisualAssets.Measure(gameObject).min.y - b.min.y, zone.center.z - b.center.z);
            foreach (Collider c in closed.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
        else Debug.LogWarning("Falta BearTrap_Closed en LevelVisualAssets.", this);
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        gameObject.SetActive(false);
    }
}
