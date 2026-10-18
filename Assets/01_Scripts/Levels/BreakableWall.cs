using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pared que se derrumba al empujarla (E): primero cae una parte y,
/// un segundo después, se viene abajo por completo y deja el paso libre.
///   intacta  -> parcial (con agujero) -> derrumbada
/// Lo prepara LevelSetup con las piezas que hay en la escena.
/// </summary>
public class BreakableWall : MonoBehaviour
{
    public GameObject intact;
    public GameObject partial;
    public GameObject broken;
    public GameObject[] hideAlways;
    public float secondStageDelay = 1f;
    public float reach = 1.8f;

    public bool IsBroken { get; private set; }
    bool started;
    Transform player;
    CharacterController controller;
    Bounds bounds;

    void Start()
    {
        foreach (GameObject g in hideAlways) if (g) g.SetActive(false);
        if (partial) partial.SetActive(false);
        if (broken) broken.SetActive(false);

        GameObject p = GameObject.Find("Player");
        if (p)
        {
            player = p.transform;
            controller = p.GetComponent<CharacterController>();
        }
        bounds = Measure(intact);
    }

    static Bounds Measure(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one);
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    bool PlayerNear()
    {
        if (!player || started) return false;
        var mv = player.GetComponent<GuardPlayerMovement>();
        if (mv && !mv.enabled) return false;
        Vector3 c = controller ? controller.bounds.center : player.position + Vector3.up;
        return Vector3.Distance(c, bounds.ClosestPoint(c)) <= reach;
    }

    void Update()
    {
        if (PlayerNear() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            StartCoroutine(Collapse());
    }

    IEnumerator Collapse()
    {
        started = true;
        Vector3 at = bounds.center;

        // 1. Se cae una parte.
        GameAudio.TrampaSuelo(at);
        Swap(intact, partial);
        yield return new WaitForSeconds(secondStageDelay);

        // 2. Se derrumba por completo.
        GameAudio.TrampaSuelo(at);
        Swap(partial, broken);
        Debris(18);
        IsBroken = true;
    }

    /// Piedras que caen al derrumbarse (con la textura de la pared).
    void Debris(int count)
    {
        Renderer source = intact ? intact.GetComponentInChildren<Renderer>(true) : null;
        for (int i = 0; i < count; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Escombro";
            float size = Random.Range(0.12f, 0.3f);
            rock.transform.localScale = new Vector3(size * Random.Range(1f, 1.8f), size, size * Random.Range(0.8f, 1.3f));
            rock.transform.SetPositionAndRotation(
                new Vector3(Random.Range(bounds.min.x + 0.2f, bounds.max.x - 0.2f),
                            Random.Range(bounds.min.y + 0.8f, bounds.max.y - 0.5f),
                            Random.Range(bounds.min.z, bounds.max.z)),
                Random.rotation);
            if (source) rock.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
            // Ni la cámara ni el jugador chocan con los escombros.
            rock.layer = 2;   // Ignore Raycast
            if (controller) Physics.IgnoreCollision(rock.GetComponent<Collider>(), controller);
            Rigidbody rb = rock.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.linearVelocity = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1.5f, 1.5f));
            StartCoroutine(Settle(rock, rb));
        }
    }

    static IEnumerator Settle(GameObject rock, Rigidbody rb)
    {
        yield return new WaitForSeconds(3f);
        if (rb) { rb.isKinematic = true; }
        // El jugador y la cámara no tropiezan con los escombros.
        if (rock) rock.GetComponent<Collider>().enabled = false;
    }

    static void Swap(GameObject from, GameObject to)
    {
        if (from) from.SetActive(false);
        if (!to) return;
        to.SetActive(true);
        foreach (MeshFilter mf in to.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh && mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }

    void OnGUI()
    {
        if (GameState.HudHidden || !PlayerNear()) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(19f * scale);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        GUI.Box(new Rect(Screen.width / 2f - 230f * scale, Screen.height - 100f * scale, 460f * scale, 45f * scale),
            "[E] Empujar la pared", style);
    }
}
