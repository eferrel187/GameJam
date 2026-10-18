using UnityEngine;

/// <summary>
/// Llave falsa: se ve, flota y se recoge igual que la de verdad, pero no
/// abre la puerta (al intentarlo se abre el suelo). Vuelve a su sitio al
/// perder una vida. Lo coloca LevelSetup en el Nivel 3.
/// </summary>
public class FakeKey : MonoBehaviour
{
    public float reach = 1.2f;

    Transform player;
    CharacterController controller;
    Bounds bounds;
    bool taken;

    void Start()
    {
        if (GetComponent<KeyFloat>() == null) gameObject.AddComponent<KeyFloat>();
        GameObject p = GameObject.Find("Player");
        if (p) { player = p.transform; controller = p.GetComponent<CharacterController>(); }
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        bounds = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one * 0.3f);
        foreach (Renderer r in rs) bounds.Encapsulate(r.bounds);
        GuardDoorAndKey.KeyLost += OnKeyLost;
    }

    void OnDestroy() { GuardDoorAndKey.KeyLost -= OnKeyLost; }

    // Al morir, la llave falsa vuelve a estar en su sitio.
    void OnKeyLost()
    {
        taken = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
    }

    void Update()
    {
        if (taken || player == null) return;
        var mv = player.GetComponent<GuardPlayerMovement>();
        if (mv && !mv.enabled) return;
        if (GuardDoorAndKey.HasKey) return;   // ya tiene la verdadera

        Vector3 c = controller ? controller.bounds.center : player.position + Vector3.up;
        if (Vector3.Distance(c, bounds.ClosestPoint(c)) > reach) return;

        taken = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        GuardDoorAndKey.GiveFakeKey();
    }
}
