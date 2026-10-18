using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GuardDoorAndKey : MonoBehaviour
{
    class Door
    {
        public Transform root;
        public Quaternion closed;
        public Collider[] colliders;
        public Bounds bounds;
        public bool open;
        public Vector3 hinge;
        public bool hingeReady;
        public float openAngle;
        public ArchDoor arch;       // puerta doble de arco (se abre sola)
    }

    /// Avisos para las trampas de los niveles.
    public static event System.Action KeyPicked;
    public static event System.Action KeyLost;
    public static bool HasKey { get; private set; }

    /// La llave que lleva el jugador es falsa (no abre: activa una trampa).
    public static bool KeyIsFake { get; private set; }
    /// Se intentó abrir la puerta con la llave falsa.
    public static event System.Action FakeDoorTried;

    static GuardDoorAndKey current;

    /// La llave falsa: se ve y se anuncia igual que la verdadera.
    public static void GiveFakeKey()
    {
        if (current == null || current.hasKey) return;
        current.hasKey = true;
        KeyIsFake = true;
        GameAudio.Llave();
    }

    readonly List<Door> doors = new List<Door>();
    Transform player;
    GameObject keyObject;
    Bounds keyBounds;
    bool hasKey;
    string prompt = "";
    string message = "";
    float messageUntil;
    Texture2D circle;

    // Lo llama GameBootstrap en cada escena cargada.
    public static void Install()
    {
        if (FindFirstObjectByType<GuardDoorAndKey>() != null)
            return;

        new GameObject("SistemaPuertaYLlave")
            .AddComponent<GuardDoorAndKey>();
    }

    static bool Matches(string objectName, string expected)
    {
        return objectName.Equals(
            expected, System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith(
                expected + " (",
                System.StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_EDITOR
    /// Solo pruebas en el editor: F4 da la llave.
    void LateUpdate()
    {
        // F1 da la llave falsa.
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && !hasKey)
            GiveFakeKey();
        if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame && !hasKey)
        {
            hasKey = true; HasKey = true;
            if (keyObject) keyObject.SetActive(false);
            KeyPicked?.Invoke();
        }
    }
#endif

    void Start()
    {
        current = this;
        HasKey = false;
        KeyIsFake = false;
        foreach (Transform t in
            FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "Player")
                player = t;

            if (Matches(t.name, "Wall") && t.name != "Wall (35)")
                AddSolidColliders(t.gameObject);

            // Antorchas sólidas: ni el jugador ni la cámara las atraviesan.
            if (Matches(t.name, "Torch"))
                AddSolidColliders(t.gameObject);

            if (Matches(t.name, "Door_Straight") || t.name == "Puerta_Llave")
            {
                AddSolidColliders(t.gameObject);

                doors.Add(new Door
                {
                    root = t,
                    closed = t.localRotation,
                    colliders = t.GetComponentsInChildren<Collider>(),
                    bounds = GetBounds(t.gameObject),
                    arch = t.GetComponent<ArchDoor>()
                });
            }

            if (Matches(t.name, "Key4") && keyObject == null)
            {
                keyObject = t.gameObject;
                keyBounds = GetBounds(keyObject);

                // Flota y gira para llamar la atención.
                if (keyObject.GetComponent<KeyFloat>() == null)
                    keyObject.AddComponent<KeyFloat>();
            }
        }

        circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        circle.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float distance = Vector2.Distance(
                new Vector2(x, y), new Vector2(31.5f, 31.5f));

            circle.SetPixel(x, y,
                distance <= 30f ? Color.white : Color.clear);
        }

        circle.Apply();

        if (!player)
            Debug.LogWarning("No se encontró el objeto Player.");

        if (!keyObject)
            Debug.LogWarning("No se encontró Key4 en la escena.");

        Debug.Log("Sistema de llave preparado. Puertas: " + doors.Count);
    }

    static void AddSolidColliders(GameObject target)
    {
        // Usa los límites locales de cada malla.
        foreach (MeshFilter mesh in
            target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mesh.sharedMesh)
                continue;

            bool hasSolid = false;

            foreach (Collider existing in
                mesh.GetComponents<Collider>())
            {
                if (existing.enabled && !existing.isTrigger)
                    hasSolid = true;
            }

            if (hasSolid)
                continue;

            BoxCollider box =
                mesh.gameObject.AddComponent<BoxCollider>();

            box.center = mesh.sharedMesh.bounds.center;
            box.size = mesh.sharedMesh.bounds.size;
            box.isTrigger = false;
        }
    }

    static Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>();

        Bounds bounds = new Bounds(
            target.transform.position, Vector3.zero);

        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    Vector3 PlayerCenter()
    {
        CharacterController controller =
            player.GetComponent<CharacterController>();

        return controller
            ? controller.bounds.center
            : player.position + Vector3.up;
    }

    void Update()
    {
        prompt = "";

        if (!player)
            return;

        GuardHealth health = player.GetComponent<GuardHealth>();

        if (health != null)
        {
            if (previousLives >= 0 && health.Lives < previousLives)
                ResetKeyAfterDeath();

            previousLives = health.Lives;
        }

        // Respeta el bloqueo de movimiento durante la introducción.
        GuardPlayerMovement movement =
            player.GetComponent<GuardPlayerMovement>();

        if (movement && !movement.enabled)
            return;

        Vector3 position = PlayerCenter();

        if ((!hasKey || KeyIsFake) && keyObject && keyObject.activeInHierarchy)
        {
            Vector3 closest = keyBounds.ClosestPoint(position);

            if (Vector3.Distance(position, closest) <= 1.2f)
            {
                hasKey = true;
                keyObject.SetActive(false);
                // En los niveles 2 y 3 sin mensaje (solo sonido e icono):
                // así no se distingue la llave buena de la falsa.
                string level = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (level == "Tutorial" || level == "Nivel1")
                {
                    message = "¡Recogiste la llave!";
                    messageUntil = Time.time + 2.5f;
                }
                GameAudio.Llave();
                HasKey = true;
                KeyIsFake = false;
                KeyPicked?.Invoke();
                messageUntil = Time.time + 2.5f;
            }
        }

        Door nearest = null;
        float nearestDistance = 2f;

        foreach (Door door in doors)
        {
            if (!door.root || door.open)
                continue;

            float distance = Vector3.Distance(
                position, door.bounds.ClosestPoint(position));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = door;
            }
        }

        if (nearest != null)
        {
            prompt = hasKey
                ? "[E] Abrir puerta"
                : "[E] Puerta cerrada: necesitas la llave";

            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (hasKey && KeyIsFake)
                {
                    // La llave no gira... y el suelo se abre.
                    message = "La llave no gira...";
                    messageUntil = Time.time + 2f;
                    FakeDoorTried?.Invoke();
                }
                else if (hasKey)
                {
                    nearest.open = true;
                    prompt = "";
                    if (nearest.arch != null) nearest.arch.Open();
                    else GameAudio.Puerta(nearest.bounds.center);

                    // El paso queda libre al abrir.
                    foreach (Collider collider in nearest.colliders)
                        if (collider) collider.enabled = false;
                }
                else
                {
                    message = "Primero busca la llave.";
                    messageUntil = Time.time + 2f;
                }
            }
        }

        foreach (Door door in doors)
        {
            if (!door.root || !door.open || door.arch != null)
                continue;

            if (!door.hingeReady)
            {
                Bounds b = door.bounds;
                door.hinge = b.center;

                if (b.size.x >= b.size.z)
                    door.hinge.x = b.min.x;
                else
                    door.hinge.z = b.min.z;

                door.hingeReady = true;
            }

            float step = Mathf.Min(
                100f * Time.deltaTime, 90f - door.openAngle);

            if (step > 0f)
            {
                door.root.RotateAround(
                    door.hinge, Vector3.up, step);

                door.openAngle += step;
            }
        }
    }

    void OnGUI()
    {
        if (GameState.HudHidden)
            return;

        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);

        if (hasKey && circle)
        {
            // Icono de llave dorada, visible solo después de recogerla.
            float x = Screen.width - 100f * scale;
            float y = 24f * scale;
            Color previous = GUI.color;
            GUI.color = new Color(1f, .75f, .12f, 1f);

            GUI.DrawTexture(
                new Rect(x, y, 34f * scale, 34f * scale), circle);

            GUI.DrawTexture(
                new Rect(x + 25f * scale, y + 13f * scale,
                    45f * scale, 9f * scale),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(x + 51f * scale, y + 18f * scale,
                    8f * scale, 17f * scale),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(x + 64f * scale, y + 18f * scale,
                    8f * scale, 13f * scale),
                Texture2D.whiteTexture);

            GUI.color = previous;
        }

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(19f * scale);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        if (!string.IsNullOrEmpty(prompt))
        {
            GUI.Box(
                new Rect(Screen.width / 2f - 230f * scale,
                    Screen.height - 100f * scale,
                    460f * scale, 45f * scale),
                prompt, style);
        }

        if (Time.time < messageUntil)
        {
            GUI.Box(
                new Rect(Screen.width / 2f - 190f * scale,
                    230f * scale, 380f * scale, 45f * scale),
                message, style);
        }
    }

    int previousLives = -1;

    void ResetKeyAfterDeath()
    {
        hasKey = false;
        HasKey = false;
        KeyIsFake = false;
        KeyLost?.Invoke();
        prompt = "";
        message = "";

        if (keyObject != null)
            keyObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (circle)
            Destroy(circle);
    }
}
