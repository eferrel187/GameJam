using UnityEngine;

/// <summary>
/// Cilindro de púas: gira sobre su eje largo y quita una vida al tocarlo.
/// Cuando se activa (activateWhen) va y vuelve en línea recta entre dos
/// límites. Lo configura LevelSetup en los niveles 2 y 3.
/// </summary>
public class HazardCylinder : MonoBehaviour
{
    [Header("Giro")]
    public float spinSpeed = 220f;

    [Header("Movimiento (ida y vuelta)")]
    public Vector3 moveAxis = Vector3.right;
    public float minOffset;
    public float maxOffset;
    public float moveSpeed = 2.2f;
    public float startDirection = -1f;
    public System.Func<bool> activateWhen;
    public bool playSound = true;
    [Tooltip("Quieto hasta que se activa (luego gira en su sitio o se mueve).")]
    public bool spinOnlyWhenActive;

    public bool Active { get; private set; }

    // Forma (en el mundo, respecto a la posición inicial).
    Vector3 axis;          // eje largo
    MeshFilter shape;
    Vector3 meshCenter;    // centro de la malla (local)
    int longest;
    float halfLength;
    float radius;
    bool segmented;
    Vector3 assemblyLocalCenter;

    public int SegmentCount { get; private set; } = 1;

    Vector3 originPos;
    Vector3 originCenter;
    Quaternion originRot;
    float offset;
    float direction;

    Transform player;
    CharacterController controller;
    GuardHealth health;
    AudioSource loop;

    public Vector3 Center => segmented ? transform.TransformPoint(assemblyLocalCenter)
        : shape != null ? shape.transform.TransformPoint(meshCenter) : transform.position;
    public Vector3 Axis => axis;
    public float Radius => radius;
    public float HalfLength => halfLength;

    void Awake()
    {
        MeasureShape();
        originPos = transform.position;
        originRot = transform.rotation;
        direction = startDirection;
    }

    /// Calcula eje, largo y radio a partir de la malla.
    public void MeasureShape()
    {
        // El volumen peligroso debe cubrir todos los tramos, no solo el primero.
        if (segmented)
        {
            originPos = transform.position;
            originRot = transform.rotation;
            originCenter = Center;
            return;
        }
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        shape = mf;

        Bounds lb = mf.sharedMesh.bounds;
        Vector3 s = mf.transform.lossyScale;
        Vector3 size = new Vector3(Mathf.Abs(lb.size.x * s.x), Mathf.Abs(lb.size.y * s.y), Mathf.Abs(lb.size.z * s.z));

        longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 local = longest == 0 ? Vector3.right : longest == 1 ? Vector3.up : Vector3.forward;
        axis = mf.transform.TransformDirection(local).normalized;
        halfLength = size[longest] * 0.5f;
        float a = size[(longest + 1) % 3], b = size[(longest + 2) % 3];
        radius = (a + b) * 0.25f * 0.92f;   // las púas cuentan, con un pequeño margen

        meshCenter = lb.center;
        originPos = transform.position;
        originRot = transform.rotation;
        originCenter = Center;
    }

    /// Estira el cilindro a lo largo de su eje hasta medir "length" metros.
    public void FitLength(float length, Vector3 newCenter)
    {
        if (shape == null || halfLength <= 0f) return;
        Vector3 ls = shape.transform.localScale;
        ls[longest] *= length / (halfLength * 2f);
        shape.transform.localScale = ls;
        transform.position += newCenter - Center;
        MeasureShape();
    }

    /// Cubre el ancho repitiendo la malla original, sin alargar sus pinchos.
    /// Los tramos comparten giro, movimiento, sonido y un solo volumen de daño.
    public void FitSegmentedLength(float length, Vector3 newCenter)
    {
        if (segmented) { SetRestCenter(newCenter); return; }
        if (!shape || !shape.sharedMesh || halfLength <= 0f || length <= 0f) return;
        MeshRenderer source = shape.GetComponent<MeshRenderer>();
        if (!source) { FitLength(length, newCenter); return; }

        float nativeLength = halfLength * 2f;
        // Se acortan ligeramente si hace falta; nunca se estira una pieza larga.
        float overlap = length > nativeLength ? Mathf.Min(0.025f, nativeLength * 0.02f) : 0f;
        SegmentCount = Mathf.Max(1, Mathf.CeilToInt((length - overlap) / (nativeLength - overlap)));
        float partLength = (length + overlap * (SegmentCount - 1)) / SegmentCount;
        float axialScale = partLength / nativeLength;
        Bounds localBounds = shape.sharedMesh.bounds;
        Vector3 originalCenter = Center;
        assemblyLocalCenter = transform.InverseTransformPoint(originalCenter);

        for (int i = 0; i < SegmentCount; i++)
        {
            GameObject part = new GameObject("TramoRodillo_" + (i + 1));
            part.layer = shape.gameObject.layer;
            part.transform.SetParent(shape.transform, false);
            Vector3 scale = Vector3.one;
            scale[longest] = axialScale;
            part.transform.localScale = scale;
            float along = -length * 0.5f + partLength * 0.5f + i * (partLength - overlap);
            part.transform.localPosition = localBounds.center
                + shape.transform.InverseTransformVector(axis * along)
                - Vector3.Scale(localBounds.center, scale);
            part.AddComponent<MeshFilter>().sharedMesh = shape.sharedMesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
        }

        source.enabled = false;
        segmented = true;
        halfLength = length * 0.5f;
        transform.position += newCenter - Center;
        MeasureShape();
    }

    /// Mueve el cilindro (sin girarlo) a una nueva posición de reposo.
    public void SetRestCenter(Vector3 newCenter)
    {
        transform.position += newCenter - Center;
        MeasureShape();
    }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            player = p.transform;
            controller = p.GetComponent<CharacterController>();
            health = p.GetComponent<GuardHealth>();
        }

        SoundLibrary lib = GameAudio.Library;
        if (playSound && lib != null && lib.rodillo != null)
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.clip = lib.rodillo;
            loop.loop = true;
            loop.spatialBlend = 1f;
            loop.rolloffMode = AudioRolloffMode.Linear;
            loop.minDistance = 1f;
            loop.maxDistance = 11f;
            loop.dopplerLevel = 0f;
            loop.volume = 0.55f;
            loop.pitch = Random.Range(0.9f, 1.1f);
            loop.time = Random.Range(0f, lib.rodillo.length * 0.8f);
            loop.Play();
        }
    }

    /// Vuelve a su sitio y se queda quieto (tras perder una vida).
    public void ResetHazard()
    {
        Active = false;
        offset = 0f;
        direction = startDirection;
        transform.SetPositionAndRotation(originPos, originRot);
    }

    void Update()
    {
        if (GameState.Paused) return;
        float dt = Time.deltaTime;

        if (!Active && activateWhen != null && activateWhen())
            Active = true;

        if (Active && maxOffset > minOffset)
        {
            offset += direction * moveSpeed * dt;
            if (offset <= minOffset) { offset = minOffset; direction = 1f; }
            if (offset >= maxOffset) { offset = maxOffset; direction = -1f; }
            transform.position += originCenter + moveAxis.normalized * offset - Center;
        }

        // Giro sobre su eje (si rueda, en el sentido del avance).
        float sign = 1f;
        if (Active && Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.5f)
        {
            float rolling = Vector3.Dot(Vector3.Cross(axis, Vector3.up), moveAxis.normalized * direction);
            // Si sube y baja no rueda por el suelo: conserva el giro propio.
            if (Mathf.Abs(rolling) > 0.001f) sign = -Mathf.Sign(rolling);
        }
        if (Active || !spinOnlyWhenActive)
            transform.RotateAround(Center, axis, spinSpeed * sign * dt);
        if (loop != null)
        {
            loop.pitch = Active ? 1.15f : 0.95f;
            loop.mute = spinOnlyWhenActive && !Active;
        }

        CheckPlayer();
    }

    void CheckPlayer()
    {
        if (player == null || controller == null || health == null || health.IsKnockedDown) return;

        float scale = player.lossyScale.y;
        float pr = controller.radius * player.lossyScale.x;
        float ph = controller.height * scale * 0.5f - pr;
        Vector3 pc = player.TransformPoint(controller.center);
        Vector3 p0 = pc - Vector3.up * Mathf.Max(0f, ph), p1 = pc + Vector3.up * Mathf.Max(0f, ph);

        Vector3 c = Center;
        float hl = Mathf.Max(0f, halfLength - radius * 0.5f);
        Vector3 h0 = c - axis * hl, h1 = c + axis * hl;

        if (SegmentDistance(p0, p1, h0, h1) < radius + pr - 0.04f)
            health.Hit();
    }

    /// Distancia mínima entre dos segmentos.
    static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-6f && e <= 1e-6f) return r.magnitude;
        if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Center + axis * halfLength, radius);
        Gizmos.DrawWireSphere(Center - axis * halfLength, radius);
    }
}
