using UnityEngine;

/// <summary>Referencias y reglas propias de las galerias de Nivel4.</summary>
[DefaultExecutionOrder(100)]
public sealed class Level4Rules : MonoBehaviour
{
    public Transform player;
    public GameObject realKey;
    public GameObject fakeKey;
    public Transform fakeCellExit;
    public RisingSpikes[] floorSpikes;
    public RisingSpikes[] fakeKeySpikes;
    public HazardCylinder roller;
    public ArchDoor exitDoor;
    public KidnapSequence catSequence;
    [Tooltip("Anchura del umbral de la celda falsa (eje lateral al salir).")]
    public float fakeCellWidth = 3.5f;

    GuardHealth health;
    GuardPlayerMovement movement;
    int previousLives;
    bool fakePickedInside, exitTriggered;
    float hintUntil;

    void Start()
    {
        if (!player)
        {
            GameObject found = GameObject.Find("Player");
            if (found) player = found.transform;
        }
        if (!player) { Debug.LogError("Nivel4: falta el Player. Usa Juego > Preparar Nivel4.", this); enabled = false; return; }
        health = player.GetComponent<GuardHealth>();
        movement = player.GetComponent<GuardPlayerMovement>();
        previousLives = health ? health.Lives : 5;
        // La funcion no se serializa: se conecta antes del primer Update.
        if (roller) roller.activateWhen = () => true;
        hintUntil = Time.time + 8f;
    }

    void Update()
    {
        if (!health || !movement) return;
        if (health.Lives < previousLives)
        {
            fakePickedInside = exitTriggered = false;
            if (floorSpikes != null)
                foreach (RisingSpikes spikes in floorSpikes) if (spikes) spikes.ResetTrap();
            if (roller) roller.ResetHazard();
        }
        previousLives = health.Lives;
        if (GameState.Paused || GameState.HudHidden || GameState.CinematicCamera ||
            health.IsKnockedDown || !movement.enabled) return;

        // La llave buena puede sustituir a la falsa si logra volver al pasillo.
        if (GuardDoorAndKey.HasKey && !GuardDoorAndKey.KeyIsFake) fakePickedInside = false;
        if (fakeCellExit)
        {
            Vector3 rel = player.position - fakeCellExit.position;
            float outwards = Vector3.Dot(rel, fakeCellExit.forward);
            float lateral = Mathf.Abs(Vector3.Dot(rel, fakeCellExit.right));
            if (GuardDoorAndKey.KeyIsFake && outwards < -0.2f && lateral < fakeCellWidth * 0.5f)
                fakePickedInside = true;
            if (fakePickedInside && !exitTriggered && outwards >= -0.25f && lateral < fakeCellWidth * 0.5f)
            {
                exitTriggered = true;
                if (fakeKeySpikes != null)
                    foreach (RisingSpikes spikes in fakeKeySpikes) if (spikes) spikes.TriggerTrap();
            }
        }
        if (player.position.y < -4f) health.Hit();
    }

    void OnGUI()
    {
        if (GameState.HudHidden || Time.time > hintUntil || !movement || !movement.enabled) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, 0.65f, 2f);
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(18f * scale),
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 290f * scale, 85f * scale, 580f * scale, 44f * scale),
            "Busca la llave en las celdas y vuelve a la salida.", style);
    }
}
