using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secuencia de Nivel1 y Nivel2:
///  1. Con la primera puerta abierta, el jugador se acerca a la segunda.
///  2. Se le quita el control, la segunda puerta se abre y solo hay oscuridad.
///  3. Una mano sale de la oscuridad y se lleva "torture_device_01".
///  4. Se devuelve el control. Al cruzar la segunda puerta -> Nivel 2.
/// Lo prepara el menú  Juego > Preparar Nivel1 y Nivel2.
/// </summary>
public class KidnapSequence : MonoBehaviour
{
    [Header("Objetos")]
    public ArchDoor firstDoor;
    public ArchDoor darkDoor;
    public string targetName = "torture_device_01";
    public Transform targetOverride;
    public Transform hand;

    [Header("Activación")]
    public float triggerDistance = 3.2f;

    [Header("Salida (cruzar la segunda puerta)")]
    public Vector3 exitCenter;
    public Vector3 exitSize = new Vector3(2.2f, 2.6f, 1.2f);

    [Header("Tiempos (s)")]
    public float cameraMove = 1.0f;
    public float darkPause = 0.9f;
    public float reachTime = 2.1f;
    public float grabTime = 0.8f;
    public float holdTime = 0.6f;
    public float retreatTime = 1.9f;

    [HideInInspector] public string[] hiddenBySetup;   // paredes que ocultó el editor

    enum Phase { Waiting, Playing, Done, Leaving }
    Phase phase = Phase.Waiting;

    /// La mano ya se llevó la jaula.
    public bool Finished => phase == Phase.Done || phase == Phase.Leaving;

    [Tooltip("Otra regla controla la salida por la puerta oscura (escena de salida).")]
    public bool externalExit;

    Transform player;
    Transform target;
    GuardPlayerMovement movement;
    GuardHealth health;
    Camera cam;
    CameraIntroFollow follow;

    readonly List<Transform> joints = new List<Transform>();
    readonly List<Quaternion> jointRest = new List<Quaternion>();
    Transform palm;

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p) player = p.transform;
        if (player) movement = player.GetComponent<GuardPlayerMovement>();
        if (player) health = player.GetComponent<GuardHealth>();
        cam = Camera.main;
        if (cam) follow = cam.GetComponent<CameraIntroFollow>();

        GameObject t = GameObject.Find(targetName);
        if (targetOverride) target = targetOverride;
        else if (t) target = t.transform;
        else Debug.LogWarning("KidnapSequence: no se encontró " + targetName);

        if (hand)
        {
            foreach (Transform tr in hand.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name.StartsWith("Dedo"))
                {
                    joints.Add(tr);
                    jointRest.Add(tr.localRotation);
                }
                if (tr.name == "Palma") palm = tr;
            }
            hand.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!player || GameState.Paused || (health && health.IsKnockedDown)) return;

        switch (phase)
        {
            case Phase.Waiting:
                bool firstOpen = firstDoor == null || firstDoor.IsOpen;
                if (firstOpen && darkDoor != null && movement != null && movement.enabled && cam && hand && target &&
                    !GameState.HudHidden && !GameState.CinematicCamera &&
                    Flat(player.position - darkDoor.Center) < triggerDistance)
                {
                    phase = Phase.Playing;
                    StartCoroutine(Play());
                }
                break;

            case Phase.Done:
                if (externalExit) break;
                // Cruza el plano de la puerta (a cualquier altura) -> Nivel 2.
                Vector3 into = darkDoor.openTowards.normalized;
                Vector3 rel = player.position - exitCenter;
                Vector3 lateral = Vector3.ProjectOnPlane(rel, into);
                lateral.y = 0f;
                bool crossed = Vector3.Dot(rel, into) > -exitSize.z * 0.5f;
                bool inside = lateral.magnitude < exitSize.x * 0.5f;
                if (crossed && inside)
                {
                    phase = Phase.Leaving;
                    GameFlow.LoadNextScene();
                }
                break;
        }
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // Tambien funciona si Start ya se ejecuto antes del montaje de LevelRules.
    public void SetTarget(Transform value)
    {
        targetOverride = target = value;
        if (value) targetName = value.name;
    }

    IEnumerator Play()
    {
        // --- 1. Sin control: el jugador se queda mirando la puerta.
        movement.enabled = false;
        GameState.CinematicCamera = true;
        GameState.HudHidden = true;

        Vector3 doorCenter = darkDoor.Center;
        Vector3 into = darkDoor.openTowards.normalized;           // hacia la oscuridad
        Vector3 side = Vector3.Cross(Vector3.up, into).normalized;
        Vector3 floor = new Vector3(doorCenter.x, player.position.y, doorCenter.z);

        Quaternion playerFrom = player.rotation;
        Quaternion playerTo = Quaternion.LookRotation(Vector3.ProjectOnPlane(doorCenter - player.position, Vector3.up));

        // Plano de cámara: dentro de la sala, de lado, mirando a la puerta.
        Vector3 shotPos = floor - into * 3.4f + side * 1.3f + Vector3.up * 1.9f;
        Vector3 lookAt = floor - into * 0.5f + Vector3.up * 0.9f;
        Vector3 camFrom = cam.transform.position;
        Quaternion camFromRot = cam.transform.rotation;
        Quaternion camToRot = Quaternion.LookRotation(lookAt - shotPos);

        for (float t = 0f; t < cameraMove; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / cameraMove);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(camFrom, shotPos, k), Quaternion.Slerp(camFromRot, camToRot, k));
            player.rotation = Quaternion.Slerp(playerFrom, playerTo, k);
            yield return null;
        }

        // --- 2. Se abre la segunda puerta: oscuridad total.
        darkDoor.Open();
        GameAudio.Terror();
        yield return new WaitForSeconds(darkDoor.openSeconds + darkPause);

        // --- 3. La mano sale de la oscuridad.
        if (hand && target)
        {
            Vector3 grabPoint = TargetTop();
            Vector3 hidden = floor + into * 2.6f + Vector3.up * 1.1f;
            Vector3 above = grabPoint + Vector3.up * 0.45f + into * 0.25f;
            Vector3 low = grabPoint + Vector3.up * 0.12f + into * 0.12f;

            // Dedos apuntando hacia la sala; al alcanzar, se inclina hacia abajo.
            Quaternion faceIn = Quaternion.LookRotation(-into);
            Quaternion reachRot = faceIn * Quaternion.Euler(32f, 0f, 0f);

            hand.SetPositionAndRotation(hidden, faceIn);
            SetCurl(-0.2f);
            hand.gameObject.SetActive(true);
            GameAudio.ManoSale(hidden);

            // Avanza lento, como si tanteara, con los dedos abriéndose.
            for (float t = 0f; t < reachTime; t += Time.deltaTime)
            {
                float k = t / reachTime;
                float e = 1f - Mathf.Pow(1f - k, 3f);       // sale rápido, frena
                Vector3 wobble = side * Mathf.Sin(t * 7f) * 0.03f * (1f - k) +
                                 Vector3.up * Mathf.Sin(t * 5f) * 0.02f;
                hand.SetPositionAndRotation(Vector3.Lerp(hidden, above, e) + wobble,
                    Quaternion.Slerp(faceIn, reachRot, e));
                SetCurl(Mathf.Lerp(-0.2f, -0.45f, k) + Mathf.Sin(t * 9f) * 0.05f);
                yield return null;
            }

            // Baja sobre el objeto y cierra los dedos.
            for (float t = 0f; t < grabTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / grabTime);
                hand.position = Vector3.Lerp(above, low, k);
                SetCurl(Mathf.Lerp(-0.45f, 1f, k));
                yield return null;
            }
            SetCurl(1f);

            // Lo agarra (ya no bloquea el paso): golpe de garras contra la celda.
            GameAudio.ManoAgarra(grabPoint);
            foreach (Collider c in target.GetComponentsInChildren<Collider>()) c.enabled = false;
            target.SetParent(palm != null ? palm : hand, true);
            yield return new WaitForSeconds(holdTime);

            // Tirón hacia la oscuridad: la celda se arrastra por el suelo.
            GameAudio.ManoArrastra(grabPoint);
            Vector3 start = hand.position;
            Quaternion startRot = hand.rotation;
            for (float t = 0f; t < retreatTime; t += Time.deltaTime)
            {
                float k = t / retreatTime;
                float e = k * k * k;                          // empieza lento, acelera
                Vector3 shake = Random.insideUnitSphere * 0.015f * (1f - k);
                hand.SetPositionAndRotation(Vector3.Lerp(start, hidden + Vector3.up * 0.2f, e) + shake,
                    Quaternion.Slerp(startRot, faceIn, e));
                yield return null;
            }

            target.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.6f);

        // --- 4. Vuelve el control, de frente a la puerta oscura (W = entrar).
        CharacterController pcc = player.GetComponent<CharacterController>();
        if (pcc != null) pcc.enabled = false;
        player.SetPositionAndRotation(floor - into * 1.8f, Quaternion.LookRotation(into));
        Physics.SyncTransforms();
        if (pcc != null) pcc.enabled = true;

        GameState.CinematicCamera = false;
        GameState.HudHidden = false;
        if (follow != null) follow.SnapBehindPlayer();
        movement.enabled = true;
        phase = Phase.Done;
    }

    Vector3 TargetTop()
    {
        Renderer[] rs = target.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return target.position;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    /// 0 = dedos rectos, 1 = puño cerrado, negativo = abiertos hacia atrás.
    void SetCurl(float amount)
    {
        for (int i = 0; i < joints.Count; i++)
        {
            bool thumb = joints[i].name.StartsWith("Dedo0");
            float angle = amount * (thumb ? 45f : 62f);
            joints[i].localRotation = jointRest[i] * Quaternion.Euler(angle, 0f, 0f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(exitCenter, exitSize);
    }
}
