using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Textos de la historia en pantalla:
///  - Introducción (después del tutorial) y contexto/misión de cada nivel,
///    al empezar y después de cada muerte.
///  - Papeles con pistas, diario para releerlas, textos del epílogo y la
///    pantalla final de victoria.
/// Mientras hay un texto el tiempo se congela (trampas, temporizadores y
/// enemigos se detienen) y el jugador no puede recibir daño.
/// </summary>
[DefaultExecutionOrder(-1500)]
public class StoryNarrator : MonoBehaviour
{
    public static StoryNarrator Instance { get; private set; }
    public static bool Active => GameState.Narrating;

    /// Se lanza cuando termina el contexto del nivel (al empezar o tras morir)
    /// y el jugador recupera el control.
    public static event Action ContextFinished;

    /// Escena de entrada del nivel (puerta que se abre), antes del contexto.
    public static Func<IEnumerator> EntranceCutscene;

    /// Si un nivel quiere controlar él mismo cuándo empieza (no se usa por defecto).
    public static bool LevelStarted { get; private set; }

    static readonly Color Gold = new Color(1f, .78f, .32f);
    static readonly Color Ivory = new Color(.96f, .93f, .86f);
    static readonly Color Muted = new Color(.72f, .68f, .62f);
    static readonly Color Ink = new Color(.24f, .14f, .06f);
    static readonly Color Parchment = new Color(.89f, .82f, .64f);

    Font font;
    CanvasGroup narrGroup, paperGroup, captionGroup, blackGroup, victoryGroup;
    Text titleText, bodyText, missionText, hintText;
    Text paperTitle, paperBody, paperHint;
    Text captionText;
    Button againButton;
    bool skipRequested;

    public static void Install(Scene scene)
    {
        EntranceCutscene = null;
        LevelStarted = false;
        GameState.Narrating = false;
        if (FindFirstObjectByType<StoryNarrator>() != null) return;
        GameObject go = new GameObject("Narrador");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<StoryNarrator>();
    }

    void Awake()
    {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameState.Narrating)
        {
            GameState.Narrating = false;
            Time.timeScale = 1f;
        }
    }

    IEnumerator Start()
    {
        // Deja que las reglas de cada nivel se preparen (registran la entrada).
        yield return null;
        string scene = gameObject.scene.name;
        if (!StoryText.TryGet(scene, out _)) { LevelStarted = true; yield break; }

        if (EntranceCutscene != null) yield return EntranceCutscene();
        yield return LevelStartTexts(scene);
        LevelStarted = true;
        ContextFinished?.Invoke();

        if (scene == "Nivel1") StartCoroutine(MeowDuringIntro());
    }

    IEnumerator LevelStartTexts(string scene)
    {
        StoryText.TryGet(scene, out StoryText.Level lv);
        if (scene == "Nivel1" && !GameState.IntroSeen)
        {
            GameState.IntroSeen = true;
            yield return ShowPages(new[]
            {
                new Page { title = StoryText.IntroTitle, body = StoryText.Intro, mission = "" },
                new Page { title = lv.title, body = lv.context, mission = lv.mission },
            });
        }
        else
        {
            yield return ShowPages(new[] { new Page { title = lv.title, body = lv.context, mission = lv.mission } });
        }
    }

    /// Lo llama GuardHealth al reaparecer: vuelve a mostrar el contexto del nivel.
    public static IEnumerator AfterDeath()
    {
        if (!Instance) yield break;
        string scene = Instance.gameObject.scene.name;
        if (!StoryText.TryGet(scene, out StoryText.Level lv)) yield break;
        yield return Instance.ShowPages(new[] { new Page { title = lv.title, body = lv.context, mission = lv.mission } });
        ContextFinished?.Invoke();
    }

    IEnumerator MeowDuringIntro()
    {
        // El recorrido inicial de la cámara del Nivel1 empieza ahora.
        yield return new WaitForSeconds(1.4f);
        GameObject cage = GameObject.Find("torture_device_01");
        StoryAssets a = StoryAssets.Get;
        if (cage) StoryKit.One(a.maullido, cage.transform.position + Vector3.up, 1f, 0.02f, 40f);
        else StoryKit.One(a.maullido, null, 0.8f);
        yield return new WaitForSeconds(3.2f);
        if (cage) StoryKit.One(a.maullidoSuave, cage.transform.position + Vector3.up, 0.8f, 0.03f, 40f);
    }

    // ------------------------------------------------------------ páginas

    public struct Page
    {
        public string title, body, mission;
    }

    static void Freeze(bool on)
    {
        GameState.Narrating = on;
        GameState.HudHidden = on;
        Time.timeScale = on ? 0f : 1f;
    }

    public IEnumerator ShowPages(Page[] pages)
    {
        GameFlow.HideDeathPanel();
        Freeze(true);
        Show(narrGroup, true);
        yield return Fade(narrGroup, 0f, 1f, .3f);

        foreach (Page page in pages)
        {
            titleText.text = page.title ?? "";
            missionText.text = "";
            bodyText.text = "";
            yield return Typewriter(bodyText, page.body ?? "");
            missionText.text = page.mission ?? "";
            yield return WaitContinue(hintText, "E · Enter · Espacio  para continuar");
        }

        yield return Fade(narrGroup, 1f, 0f, .25f);
        Show(narrGroup, false);
        Freeze(false);
        // Un instante sin daño al recuperar el control.
        GuardHealth h = FindFirstObjectByType<GuardHealth>();
        if (h) h.Shield(1.2f);
    }

    IEnumerator Typewriter(Text target, string full)
    {
        skipRequested = false;
        float shown = 0f;
        float start = Time.unscaledTime;
        while (shown < full.Length)
        {
            if (Time.unscaledTime - start > 0.25f && Pressed()) break;
            shown += Time.unscaledDeltaTime * 60f;
            target.text = full.Substring(0, Mathf.Min(full.Length, Mathf.FloorToInt(shown)));
            yield return null;
        }
        target.text = full;
        yield return null;
    }

    IEnumerator WaitContinue(Text hint, string label)
    {
        float start = Time.unscaledTime;
        hint.text = label;
        while (true)
        {
            hint.color = Color.Lerp(Muted * .6f, Muted, .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f));
            if (Time.unscaledTime - start > 0.3f && Pressed()) break;
            yield return null;
        }
        hint.text = "";
        yield return null;
    }

    static bool Pressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse m = Mouse.current;
        return (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                               kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
               (m != null && m.leftButton.wasPressedThisFrame);
    }

    // ------------------------------------------------------------ papel / diario

    public IEnumerator ShowPaper(string heading, string text, string footer = "E · Enter  para guardar la pista")
    {
        Freeze(true);
        StoryKit.One(StoryAssets.Get.papel, null, 0.8f);
        paperTitle.text = heading;
        paperBody.text = text;
        Show(paperGroup, true);
        yield return Fade(paperGroup, 0f, 1f, .2f);
        yield return WaitContinue(paperHint, footer);
        yield return Fade(paperGroup, 1f, 0f, .15f);
        Show(paperGroup, false);
        Freeze(false);
    }

    // ------------------------------------------------------------ epílogo

    public IEnumerator FadeToBlack(float seconds) { Show(blackGroup, true); yield return FadeScaled(blackGroup, blackGroup.alpha, 1f, seconds); }
    public IEnumerator FadeFromBlack(float seconds) { yield return FadeScaled(blackGroup, blackGroup.alpha, 0f, seconds); Show(blackGroup, false); }

    /// Texto que aparece despacio sobre la escena (sin congelar el tiempo).
    public IEnumerator Caption(string text, int size, Color color, float fadeIn, float hold, float fadeOut, bool italic = true)
    {
        captionText.text = text;
        captionText.fontSize = size;
        captionText.color = color;
        captionText.fontStyle = italic ? FontStyle.Italic : FontStyle.Bold;
        Show(captionGroup, true);
        captionGroup.interactable = captionGroup.blocksRaycasts = false;
        yield return FadeScaled(captionGroup, 0f, 1f, fadeIn);
        yield return new WaitForSeconds(hold);
        if (fadeOut > 0f)
        {
            yield return FadeScaled(captionGroup, 1f, 0f, fadeOut);
            Show(captionGroup, false);
        }
    }

    public void HideCaption() { Show(captionGroup, false); }

    public void ShowVictory()
    {
        GameState.HudHidden = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Show(victoryGroup, true);
        StartCoroutine(FadeScaled(victoryGroup, 0f, 1f, .8f));
        if (UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(againButton.gameObject);
    }

    void PlayAgain()
    {
        victoryGroup.interactable = false;
        GameFlow.RestartAdventure();
    }

    void BackToMenu()
    {
        victoryGroup.interactable = false;
        GameFlow.GoToMenu();
    }

    // ------------------------------------------------------------ UI

    static void Show(CanvasGroup g, bool v)
    {
        g.alpha = v ? 1f : 0f;
        g.interactable = v;
        g.blocksRaycasts = v;
    }

    static IEnumerator Fade(CanvasGroup g, float from, float to, float s)
    {
        for (float t = 0f; t < s; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / s);
            yield return null;
        }
        g.alpha = to;
    }

    static IEnumerator FadeScaled(CanvasGroup g, float from, float to, float s)
    {
        for (float t = 0f; t < s; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / s));
            yield return null;
        }
        g.alpha = to;
    }

    void BuildCanvas()
    {
        GameObject root = new GameObject("NarradorCanvas");
        root.transform.SetParent(transform, false);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();

        // Texto de la historia
        narrGroup = Layer(root.transform, "Narracion");
        Stretch(Img(narrGroup.transform, "Velo", new Color(0f, 0f, 0f, .84f)).rectTransform);
        Image band = Img(narrGroup.transform, "Banda", new Color(.06f, .04f, .03f, .9f));
        Place(band.rectTransform, new Vector2(0, 0), new Vector2(4000, 640));
        Image line1 = Img(narrGroup.transform, "Linea", Gold * .6f);
        Place(line1.rectTransform, new Vector2(0, 322), new Vector2(1500, 2));
        Image line2 = Img(narrGroup.transform, "Linea", Gold * .6f);
        Place(line2.rectTransform, new Vector2(0, -322), new Vector2(1500, 2));
        titleText = Label(narrGroup.transform, "", 50, Gold, new Vector2(0, 240), new Vector2(1600, 80), FontStyle.Bold);
        bodyText = Label(narrGroup.transform, "", 40, Ivory, new Vector2(0, 20), new Vector2(1400, 340), FontStyle.Italic, true);
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.lineSpacing = 1.15f;
        missionText = Label(narrGroup.transform, "", 30, Gold * .95f, new Vector2(0, -215), new Vector2(1500, 90), FontStyle.Bold, true);
        hintText = Label(narrGroup.transform, "", 26, Muted, new Vector2(0, -400), new Vector2(900, 40), FontStyle.Normal);
        Show(narrGroup, false);

        // Papel con pista
        paperGroup = Layer(root.transform, "Papel");
        Stretch(Img(paperGroup.transform, "Velo", new Color(0f, 0f, 0f, .6f)).rectTransform);
        Image border = Img(paperGroup.transform, "Borde", new Color(.38f, .24f, .1f));
        Place(border.rectTransform, new Vector2(0, 10), new Vector2(1040, 660));
        Image paper = Img(paperGroup.transform, "Papel", Parchment);
        Place(paper.rectTransform, new Vector2(0, 10), new Vector2(1020, 640));
        paperTitle = Label(paperGroup.transform, "", 40, Ink, new Vector2(0, 250), new Vector2(950, 70), FontStyle.Bold);
        paperBody = Label(paperGroup.transform, "", 40, Ink, new Vector2(0, 10), new Vector2(880, 400), FontStyle.Italic, true);
        paperBody.lineSpacing = 1.2f;
        paperHint = Label(paperGroup.transform, "", 26, Muted, new Vector2(0, -390), new Vector2(1000, 40), FontStyle.Normal);
        Show(paperGroup, false);

        // Negro para fundidos del epílogo
        blackGroup = Layer(root.transform, "Negro");
        Stretch(Img(blackGroup.transform, "Negro", Color.black).rectTransform);
        Show(blackGroup, false);

        // Texto del epílogo
        captionGroup = Layer(root.transform, "Leyenda");
        captionText = Label(captionGroup.transform, "", 40, Ivory, new Vector2(0, -330), new Vector2(1500, 260), FontStyle.Italic, true);
        captionText.lineSpacing = 1.15f;
        Shadow(captionText.gameObject, 3);
        Show(captionGroup, false);

        // Pantalla final de victoria
        victoryGroup = Layer(root.transform, "Victoria");
        Stretch(Img(victoryGroup.transform, "Velo", new Color(0f, 0f, 0f, .7f)).rectTransform);
        Text vt = Label(victoryGroup.transform, "¡VICTORIA!", 120, Gold, new Vector2(0, 250), new Vector2(1800, 170), FontStyle.Bold);
        Shadow(vt.gameObject, 4);
        Label(victoryGroup.transform, "Rescataste a tu gatito y volvieron a casa.", 40, Ivory,
            new Vector2(0, 130), new Vector2(1600, 60), FontStyle.Italic);
        againButton = MakeButton(victoryGroup.transform, "JUGAR DE NUEVO", new Vector2(0, -20), PlayAgain);
        MakeButton(victoryGroup.transform, "VOLVER AL MENÚ", new Vector2(0, -130), BackToMenu);
        Show(victoryGroup, false);
    }

    static CanvasGroup Layer(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go.AddComponent<CanvasGroup>();
    }

    static Image Img(Transform parent, string name, Color c)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image i = go.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i;
    }

    Text Label(Transform parent, string content, int size, Color color, Vector2 pos, Vector2 box, FontStyle style,
        bool wrap = false)
    {
        GameObject go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        Place(t.rectTransform, pos, box);
        return t;
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Image frame = Img(parent, "Boton_" + label + "_Borde", Gold * .75f);
        Place(frame.rectTransform, pos, new Vector2(484, 88));
        Image inner = Img(frame.transform, "Boton_" + label, Color.white);
        inner.raycastTarget = true;
        RectTransform r = inner.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * 2; r.offsetMax = -Vector2.one * 2;
        Button b = inner.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(.14f, .11f, .09f, 1f);
        cb.highlightedColor = new Color(.42f, .3f, .14f, 1f);
        cb.selectedColor = new Color(.42f, .3f, .14f, 1f);
        cb.pressedColor = new Color(.62f, .45f, .18f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        Text t = Label(inner.transform, label, 38, Ivory, Vector2.zero, new Vector2(480, 84), FontStyle.Bold);
        Shadow(t.gameObject, 2);
        return b;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void Shadow(GameObject go, float px)
    {
        Shadow s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, .85f);
        s.effectDistance = new Vector2(px, -px);
    }
}
