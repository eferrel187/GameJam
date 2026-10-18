using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ControlNivel2 : MonoBehaviour
{
    [Header("Jugador")]
    public Transform player;

    [Header("Acceso al cuarto de la llave")]
    public Transform puntoInteraccion;
    public GameObject paredTemporizada;
    public GameObject puertaBloqueadoraOpcional;
    public BoxCollider zonaCuarto;
    [Min(0.5f)] public float distanciaInteraccion = 2.5f;
    // CAMBIA SOLO ESTE VALOR PARA PROBAR OTROS TIEMPOS (ej. 6f, 10f, 15f).
    // Es una constante para que Unity no conserve un valor viejo del Inspector.
    private const float TIEMPO_LIMITE_SEGUNDOS = 5f;

    [Header("Cilindros")]
    public HazardCylinderNivel2 cilindroPasillo;
    public HazardCylinderNivel2 cilindroCuarto;

    private enum Estado { Listo, Contando, Fallido, Dentro }
    private Estado estado = Estado.Listo;
    private GuardHealth salud;
    private CharacterController controlador;
    private int vidasAnteriores;
    private float restante;

    private void Start()
    {
        if (player == null)
        {
            GameObject encontrado = GameObject.Find("Player");
            if (encontrado != null)
                player = encontrado.transform;
        }

        if (player == null)
        {
            Debug.LogError("ControlNivel2: asigna Player.", this);
            enabled = false;
            return;
        }

        salud = player.GetComponent<GuardHealth>();
        controlador = player.GetComponent<CharacterController>();

        if (salud == null)
        {
            Debug.LogError("ControlNivel2: Player necesita GuardHealth.", this);
            enabled = false;
            return;
        }

        if (puntoInteraccion == null || paredTemporizada == null || zonaCuarto == null)
        {
            Debug.LogError("ControlNivel2: faltan PuntoInteraccion, ParedTemporizada o ZonaCuarto.", this);
            enabled = false;
            return;
        }

        zonaCuarto.isTrigger = true;
        vidasAnteriores = salud.Lives;
        ReiniciarReto();
    }

    private Vector3 CentroJugador()
    {
        return controlador != null && controlador.enabled
            ? controlador.bounds.center
            : player.position + Vector3.up;
    }

    private bool JugadorDentroDelCuarto()
    {
        if (zonaCuarto == null || !zonaCuarto.enabled ||
            !zonaCuarto.gameObject.activeInHierarchy)
            return false;

        // En lugar de exigir que el CENTRO del Player entre al BoxCollider,
        // detectamos si el volumen del CharacterController toca el cuarto.
        if (controlador != null && controlador.enabled)
            return zonaCuarto.bounds.Intersects(controlador.bounds);

        return zonaCuarto.bounds.Contains(CentroJugador());
    }

    private void Update()
    {
        if (salud.Lives < vidasAnteriores)
            ReiniciarReto();

        vidasAnteriores = salud.Lives;

        if (salud.Lives <= 0)
            return;

        if (estado == Estado.Listo)
        {
            float distancia = Vector3.Distance(
                CentroJugador(), puntoInteraccion.position);

            if (distancia <= distanciaInteraccion &&
                Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                estado = Estado.Contando;
                restante = TIEMPO_LIMITE_SEGUNDOS;
                CambiarPaso(true);
            }
        }
        else if (estado == Estado.Contando)
        {
            if (JugadorDentroDelCuarto())
            {
                estado = Estado.Dentro;
                if (cilindroCuarto != null)
                    cilindroCuarto.ActivarAlEntrarCuarto();
                else
                    Debug.LogWarning("ControlNivel2: falta asignar Cilindro Cuarto.", this);
                return;
            }

            restante -= Time.deltaTime;

            if (restante <= 0f)
            {
                restante = 0f;
                estado = Estado.Fallido;
                CambiarPaso(false);
            }
        }
    }

    private void CambiarPaso(bool abierto)
    {
        if (paredTemporizada != null)
            paredTemporizada.SetActive(!abierto);

        if (puertaBloqueadoraOpcional != null &&
            puertaBloqueadoraOpcional != paredTemporizada)
        {
            puertaBloqueadoraOpcional.SetActive(!abierto);
        }
    }

    private void ReiniciarReto()
    {
        estado = Estado.Listo;
        restante = TIEMPO_LIMITE_SEGUNDOS;
        CambiarPaso(false);

        if (cilindroPasillo != null)
            cilindroPasillo.Reiniciar();

        if (cilindroCuarto != null)
            cilindroCuarto.Reiniciar();
    }

    private static void Rectangulo(Rect rect, Color color)
    {
        Color anterior = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = anterior;
    }

    private static GUIStyle Estilo(int tamano, Color color, TextAnchor alineacion)
    {
        GUIStyle estilo = new GUIStyle(GUI.skin.label);
        estilo.fontSize = tamano;
        estilo.fontStyle = FontStyle.Bold;
        estilo.alignment = alineacion;
        estilo.normal.textColor = color;
        estilo.wordWrap = true;
        return estilo;
    }

    private void OnGUI()
    {
        if (salud == null || salud.Lives <= 0)
            return;

        // GUI.depth bajo hace que el contador se dibuje delante de otros OnGUI.
        int profundidadAnterior = GUI.depth;
        GUI.depth = -1000;

        float escala = Mathf.Clamp(Screen.width / 1280f, 0.65f, 1.4f);
        float ancho = Mathf.Min(Screen.width - 16f, 440f * escala);
        float x = (Screen.width - ancho) * 0.5f;
        float y = 15f * escala;
        Color fondo = new Color(0.045f, 0.065f, 0.11f, 0.95f);

        if (estado == Estado.Contando)
        {
            float alto = 130f * escala;
            Rectangulo(new Rect(x, y, ancho, alto), fondo);

            Color acento = restante <= 3f
                ? new Color(1f, 0.31f, 0.29f)
                : new Color(1f, 0.78f, 0.26f);

            Rectangulo(new Rect(x, y, ancho, 5f * escala), acento);

            GUI.Label(
                new Rect(x + 8f, y + 12f * escala, ancho - 16f, 30f * escala),
                "ENTRA AL CUARTO",
                Estilo(Mathf.RoundToInt(22f * escala), Color.white, TextAnchor.MiddleCenter));

            GUI.Label(
                new Rect(x + 8f, y + 42f * escala, ancho - 16f, 52f * escala),
                Mathf.CeilToInt(restante) + " s",
                Estilo(Mathf.RoundToInt(46f * escala), acento, TextAnchor.MiddleCenter));

            float margen = 22f * escala;
            float barraY = y + 106f * escala;
            float barraAncho = Mathf.Max(0f, ancho - margen * 2f);
            Rectangulo(new Rect(x + margen, barraY, barraAncho, 10f * escala),
                new Color(0.28f, 0.32f, 0.40f));
            Rectangulo(new Rect(x + margen, barraY,
                barraAncho * Mathf.Clamp01(restante / TIEMPO_LIMITE_SEGUNDOS), 10f * escala), acento);
        }
        else if (estado == Estado.Fallido)
        {
            float alto = 90f * escala;
            Rectangulo(new Rect(x, y, ancho, alto), fondo);
            Rectangulo(new Rect(x, y, ancho, 5f * escala), new Color(1f, 0.31f, 0.29f));
            GUI.Label(
                new Rect(x + 14f, y + 12f * escala, ancho - 28f, alto - 18f * escala),
                "SE ACABO EL TIEMPO",
                Estilo(Mathf.RoundToInt(19f * escala), Color.white, TextAnchor.MiddleCenter));
        }
        else if (estado == Estado.Listo && puntoInteraccion != null && player != null &&
                 Vector3.Distance(CentroJugador(), puntoInteraccion.position) <= distanciaInteraccion)
        {
            float alto = 52f * escala;
            float abajo = Screen.height - 85f * escala;
            Rectangulo(new Rect(x, abajo, ancho, alto), fondo);
            GUI.Label(
                new Rect(x + 8f, abajo + 5f * escala, ancho - 16f, alto - 10f * escala),
                "[E] Abrir paso por " + TIEMPO_LIMITE_SEGUNDOS.ToString("0.#") + " segundos",
                Estilo(Mathf.RoundToInt(19f * escala), Color.white, TextAnchor.MiddleCenter));
        }

        GUI.depth = profundidadAnterior;
    }
}
