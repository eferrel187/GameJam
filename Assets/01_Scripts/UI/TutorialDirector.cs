using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial jugable: muestra una indicación según el tramo del pasillo
/// en el que está el jugador (eje X). Al cruzar la puerta final,
/// pasa al siguiente nivel.
/// Los tramos los configura el script de editor "Juego/Crear escena Tutorial".
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Tooltip("El paso empieza cuando el jugador pasa esta X.")]
        public float startX;
        public string title;
        [Tooltip("X del punto de reaparición de este paso (si useCheckpoint).")]
        public bool useCheckpoint;
        public float checkpointX;
        [TextArea] public string hint;
    }

    public Step[] steps =
    {
        new Step { startX = -999f, title = "CAMINAR",
            hint = "Usa  W A S D  para caminar y el  RATÓN  para mirar" },
    };

    [Tooltip("Al pasar esta X el tutorial se completa.")]
    public float finishX = 9999f;
    public string finishTitle = "¡TUTORIAL COMPLETADO!";
    public string finishHint = "Ahora sí... a rescatar a tu gato. Cuidado: nada es lo que parece.";

    static readonly Color Gold = new Color(1f, .78f, .32f);
    static readonly Color Ivory = new Color(.96f, .93f, .86f);

    Transform player;
    int current = -1;
    bool finished;
    CanvasGroup group;
    Text titleText, hintText, counterText;
    Coroutine swap;

    void Start()
    {
        GameObject found = GameObject.Find("Player");
        if (found)
        {
            player = found.transform;
            startPosition = player.position;
            startRotation = player.rotation;
        }
        BuildUI();
    }

    void Update()
    {
        if (!player) return;

        // Se oculta mientras hay menú, pausa o game over.
        float target = GameState.HudHidden ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target,
            Time.unscaledDeltaTime * 4f);

        if (finished) return;

        float x = player.position.x;

        if (x >= finishX)
        {
            finished = true;
            Show(finishTitle, finishHint, "");
            StartCoroutine(Finish());
            return;
        }

        int index = 0;
        for (int i = 0; i < steps.Length; i++)
            if (x >= steps[i].startX) index = i;

        // Nunca retrocede de paso.
        if (index > current)
        {
            current = index;
            SaveCheckpoint();
            Show(steps[index].title, steps[index].hint,
                "PASO " + (index + 1) + " / " + steps.Length);
        }
    }

    /// Punto de reaparición al empezar cada paso (si mueres, vuelves aquí).
    void SaveCheckpoint()
    {
        GuardHealth health = player.GetComponent<GuardHealth>();
        if (health == null || health.IsKnockedDown || current < 0) return;

        Step step = steps[current];
        if (!step.useCheckpoint) return;

        if (checkpoint == null)
            checkpoint = new GameObject("PuntoDeControl").transform;

        // Punto fijo en el centro del pasillo, mirando hacia adelante:
        // nunca encima de una trampa ni con la cámara dentro de un obstáculo.
        checkpoint.SetPositionAndRotation(
            new Vector3(step.checkpointX, startPosition.y, checkpointZ),
            startRotation);
        health.respawnPoint = checkpoint;
    }

    Transform checkpoint;

    [Tooltip("Z del centro del pasillo, para los puntos de reaparición.")]
    public float checkpointZ;
    Vector3 startPosition;
    Quaternion startRotation;

    IEnumerator Finish()
    {
        yield return new WaitForSeconds(3f);
        GameFlow.LoadNextScene();
    }

    void Show(string title, string hint, string counter)
    {
        if (swap != null) StopCoroutine(swap);
        swap = StartCoroutine(Swap(title, hint, counter));
    }

    IEnumerator Swap(string title, string hint, string counter)
    {
        RectTransform box = (RectTransform)titleText.transform.parent;

        // Sale, cambia el texto y entra con un pequeño rebote.
        for (float t = 0f; t < .15f; t += Time.unscaledDeltaTime)
        {
            box.localScale = Vector3.one * Mathf.Lerp(1f, .9f, t / .15f);
            yield return null;
        }

        titleText.text = title;
        hintText.text = hint;
        counterText.text = counter;

        for (float t = 0f; t < .25f; t += Time.unscaledDeltaTime)
        {
            float k = t / .25f;
            box.localScale = Vector3.one *
                (1f + Mathf.Sin(k * Mathf.PI) * .06f);
            yield return null;
        }

        box.localScale = Vector3.one;
    }

    void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("TutorialCanvas");
        root.transform.SetParent(transform, false);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // Marco dorado + caja oscura, arriba al centro.
        GameObject frame = new GameObject("Marco", typeof(RectTransform));
        frame.transform.SetParent(root.transform, false);
        frame.AddComponent<Image>().color = Gold * .7f;
        RectTransform fr = (RectTransform)frame.transform;
        fr.anchorMin = fr.anchorMax = new Vector2(.5f, 1f);
        fr.pivot = new Vector2(.5f, 1f);
        fr.anchoredPosition = new Vector2(0, -40);
        fr.sizeDelta = new Vector2(1104, 164);

        GameObject box = new GameObject("Caja", typeof(RectTransform));
        box.transform.SetParent(frame.transform, false);
        box.AddComponent<Image>().color = new Color(.07f, .065f, .08f, 1f);
        RectTransform br = (RectTransform)box.transform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.one * 2f;
        br.offsetMax = -Vector2.one * 2f;

        counterText = MakeText(box.transform, font, 22, new Color(.72f, .68f, .62f),
            new Vector2(0, 52), FontStyle.Bold);
        titleText = MakeText(box.transform, font, 44, Gold,
            new Vector2(0, 14), FontStyle.Bold);
        hintText = MakeText(box.transform, font, 30, Ivory,
            new Vector2(0, -42), FontStyle.Normal);
    }

    static Text MakeText(Transform parent, Font font, int size, Color color,
        Vector2 position, FontStyle style)
    {
        GameObject go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1060, 60);
        return text;
    }
}
