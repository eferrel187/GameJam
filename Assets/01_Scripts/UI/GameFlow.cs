using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Estado global compartido entre los sistemas del juego.
/// </summary>
public static class GameState
{
    /// Oculta corazones, icono de llave, avisos y textos del tutorial.
    public static bool HudHidden;

    /// Congela el recorrido inicial de la cámara hasta que empiece la partida.
    public static bool HoldIntro;

    /// El menú principal solo aparece la primera vez (o al volver al menú).
    public static bool MenuShownThisSession;

    public static bool Paused;

    /// Una cinemática controla la cámara (la cámara normal no la mueve).
    public static bool CinematicCamera;

    /// Vidas con las que se entra al siguiente nivel (-1 = completas).
    /// El Nivel1 (y el tutorial) siempre empiezan con todas.
    public static int CarriedLives = -1;
}

/// <summary>
/// Instala los sistemas automáticos en CADA escena que se carga
/// (RuntimeInitializeOnLoadMethod solo corre una vez al darle Play,
/// así que al reiniciar o cambiar de nivel se perdían).
/// </summary>
static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        GameState.HudHidden = false;
        GameState.HoldIntro = false;
        GameState.MenuShownThisSession = false;
        GameState.Paused = false;
        GameState.CinematicCamera = false;
        GameState.CarriedLives = -1;
        Time.timeScale = 1f;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Primero las reglas del nivel (agrupan puertas, colisiones...)
        // para que los demás sistemas ya las encuentren.
        LevelSetup.Install(scene);
        GameFlow.Install();
        GuardDoorAndKey.Install();
        GuardCameraCollision.Install();
    }
}

/// <summary>
/// Menú principal (en la primera escena del Build), pausa (Esc),
/// Game Over y cambio de nivel con fundido.
/// Todo se construye por código: no hace falta tocar la escena.
/// </summary>
[DefaultExecutionOrder(-2000)]
public class GameFlow : MonoBehaviour
{
    enum State { Menu, Playing, Paused, Dead, GameOver, Loading }

    [Header("Textos")]
    public string gameTitle = "RESCATA AL GATO";
    public string tagline = "Nada es lo que parece...";

    static readonly Color Gold = new Color(1f, .78f, .32f);
    static readonly Color Ivory = new Color(.96f, .93f, .86f);
    static readonly Color Muted = new Color(.72f, .68f, .62f);
    static readonly Color Danger = new Color(.95f, .3f, .25f);

    static GameFlow instance;

    State state = State.Playing;
    int stateFrame;
    Font font;
    CanvasGroup menuGroup, pauseGroup, deathGroup, gameOverGroup, fadeGroup;
    Button playButton, resumeButton;
    Text titleText, deathTitle, riddleText, livesText;

    // Pantalla de muerte: títulos y acertijos (el primero insiste en la llave).
    static readonly string[] DeathTitles =
    {
        "LA MAZMORRA TE RECLAMA",
        "OTRA ALMA PARA LA PIEDRA",
        "TE CONFIASTE...",
        "EL SUELO TENÍA HAMBRE",
    };

    static readonly string[] Riddles =
    {
        "Lo que abre la puerta quedó a tu espalda.",
        "Primero la llave. Después, la puerta.",
        "Mira atrás antes de correr hacia adelante.",
        "El suelo cuenta tus pasos.",
    };
    GuardHealth health;
    GuardPlayerMovement movement;
    float deadSince = -1f;

    public static void Install()
    {
        if (FindFirstObjectByType<GameFlow>() != null)
            return;

        new GameObject("GameFlow").AddComponent<GameFlow>();
    }

    /// Carga la siguiente escena del Build (o vuelve al menú si no hay más).
    public static void LoadNextScene()
    {
        // Las vidas pasan al siguiente nivel (desde el tutorial, completas).
        GuardHealth h = FindFirstObjectByType<GuardHealth>();
        string current = SceneManager.GetActiveScene().name;
        GameState.CarriedLives = current.StartsWith("Nivel") && h != null && h.Lives > 0 ? h.Lives : -1;

        int next = SceneManager.GetActiveScene().buildIndex + 1;

        if (next <= 0 || next >= SceneManager.sceneCountInBuildSettings)
        {
            GameState.MenuShownThisSession = false;
            next = 0;
        }

        if (instance != null)
            instance.StartCoroutine(instance.FadeAndLoad(next));
        else
            SceneManager.LoadScene(next);
    }

    // ---------------------------------------------------------------- ciclo

    void Awake()
    {
        instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureEventSystem();
        BuildCanvas();

        bool showMenu = !GameState.MenuShownThisSession &&
                        SceneManager.GetActiveScene().buildIndex == 0;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameState.Paused = false;
        GameState.CinematicCamera = false;

        if (showMenu)
        {
            GameState.MenuShownThisSession = true;
            GameState.HoldIntro = true;
            GameState.HudHidden = true;
            SetCursorFree(true);
            Show(menuGroup, true);
            SetState(State.Menu);
            Select(playButton);
        }
        else
        {
            GameState.HoldIntro = false;
            GameState.HudHidden = false;
            SetState(State.Playing);

            // Entrada al nivel con fundido desde negro.
            Show(fadeGroup, true);
            fadeGroup.blocksRaycasts = false;
            StartCoroutine(Fade(fadeGroup, 1f, 0f, .8f));
        }
    }

    void Start()
    {
        health = FindFirstObjectByType<GuardHealth>();
        movement = FindFirstObjectByType<GuardPlayerMovement>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;

        // Ignora la tecla que provocó el cambio de estado.
        if (Time.frameCount == stateFrame || kb == null)
            return;

        switch (state)
        {
            case State.Playing:
                if (health != null && health.IsKnockedDown && health.Lives > 0)
                {
                    ShowDeath();
                }
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    Pause();
                }
                else if (health != null && health.Lives <= 0)
                {
                    // Deja ver la animación de muerte antes de "SIN VIDAS".
                    if (deadSince < 0f) deadSince = Time.time;
                    else if (Time.time - deadSince > 2.2f) GameOver();
                }
                break;

            case State.Paused:
                if (kb.escapeKey.wasPressedThisFrame)
                    Resume();
                break;

            case State.Dead:
                // Revive solo: el mensaje se va cuando el jugador reaparece.
                if (health == null || !health.IsKnockedDown)
                    EndDeath();
                break;
        }
    }

    void LateUpdate()
    {
        // Pulso suave del título.
        if (state == State.Menu && titleText != null)
        {
            float pulse = .85f + .15f * Mathf.Sin(Time.unscaledTime * 2f);
            titleText.color = Color.Lerp(Gold * .8f, Gold, pulse);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void SetState(State next)
    {
        state = next;
        stateFrame = Time.frameCount;
    }

    // ------------------------------------------------------------- acciones

    void OnPlayPressed()
    {
        if (state != State.Menu) return;
        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        SetState(State.Playing);
        Select(null);
        menuGroup.interactable = false;
        yield return Fade(menuGroup, 1f, 0f, .4f);
        Show(menuGroup, false);

        GameState.HudHidden = false;
        GameState.HoldIntro = false;   // arranca la cámara / el jugador
        SetCursorFree(false);
    }

    void SkipTutorial()
    {
        if (state != State.Menu) return;
        menuGroup.interactable = false;
        LoadNextScene();
    }

    IEnumerator FadeAndLoad(int buildIndex)
    {
        SetState(State.Loading);
        Show(fadeGroup, true);
        yield return Fade(fadeGroup, 0f, 1f, .6f);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameState.Paused = false;
        SceneManager.LoadScene(buildIndex);
    }

    void Pause()
    {
        SetState(State.Paused);
        GameState.Paused = true;
        GameState.HudHidden = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetCursorFree(true);
        Show(pauseGroup, true);
        Select(resumeButton);
    }

    void Resume()
    {
        if (state != State.Paused) return;

        SetState(State.Playing);
        GameState.Paused = false;
        GameState.HudHidden = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Show(pauseGroup, false);
        Select(null);

        // Solo vuelve a capturar el ratón si el jugador ya tiene el control.
        if (movement == null || movement.enabled)
            SetCursorFree(false);
    }

    void ShowDeath()
    {
        SetState(State.Dead);
        GameState.HudHidden = true;

        int index = Mathf.Max(0, health.Deaths - 1);
        deathTitle.text = DeathTitles[index % DeathTitles.Length];
        riddleText.text = Riddles[index % Riddles.Length];
        livesText.text = health.Lives == 1
            ? "Te queda 1 vida"
            : "Te quedan " + health.Lives + " vidas";

        Show(deathGroup, true);
        deathGroup.blocksRaycasts = false;
        StartCoroutine(Fade(deathGroup, 0f, 1f, .4f));
    }

    void EndDeath()
    {
        SetState(State.Playing);
        GameState.HudHidden = false;
        StartCoroutine(FadeOutDeath());
    }

    IEnumerator FadeOutDeath()
    {
        yield return Fade(deathGroup, deathGroup.alpha, 0f, .4f);
        if (state != State.Dead) Show(deathGroup, false);
    }

    void GameOver()
    {
        SetState(State.GameOver);
        GameState.HudHidden = true;
        SetCursorFree(true);
        Show(gameOverGroup, true);
        StartCoroutine(Fade(gameOverGroup, 0f, 1f, .6f));
        StartCoroutine(BackToStart());
    }

    /// Tras el GAME OVER vuelve solo al principio: empieza el tutorial
    /// (sin menú) y al terminarlo pasa a Nivel1.
    IEnumerator BackToStart()
    {
        yield return new WaitForSecondsRealtime(3.5f);
        GameState.MenuShownThisSession = true;
        // Sin vidas: se vuelve a empezar desde el Nivel 1 (no el tutorial), con todas las vidas.
        GameState.CarriedLives = -1;
        int level1 = SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/Nivel1.unity");
        yield return FadeAndLoad(level1 >= 0 ? level1 : 0);
    }

    void RestartLevel()
    {
        Scene scene = SceneManager.GetActiveScene();
        StartCoroutine(FadeAndLoad(Mathf.Max(0, scene.buildIndex)));
    }

    void GoToMainMenu()
    {
        GameState.MenuShownThisSession = false;
        StartCoroutine(FadeAndLoad(0));
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ utilidades

    static void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    static void Select(Selectable target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                target ? target.gameObject : null);
    }

    static void Show(CanvasGroup group, bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        group.alpha = to;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null ||
            FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        InputSystemUIInputModule module =
            go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    // ------------------------------------------------------- construcción UI

    void BuildCanvas()
    {
        GameObject root = new GameObject("GameFlowCanvas");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        root.AddComponent<GraphicRaycaster>();

        BuildMenu(root.transform);
        BuildPause(root.transform);
        BuildDeath(root.transform);
        BuildGameOver(root.transform);

        fadeGroup = Layer(root.transform, "Fade");
        Stretch(Img(fadeGroup.transform, "Black", Color.black));
        Show(fadeGroup, false);
    }

    void BuildMenu(Transform parent)
    {
        menuGroup = Layer(parent, "MenuPrincipal");
        Show(menuGroup, false);

        Stretch(Img(menuGroup.transform, "Velo", new Color(0f, 0f, 0f, .55f)));

        Image band = Img(menuGroup.transform, "Banda",
            new Color(0f, 0f, 0f, .45f));
        Place(band.rectTransform, new Vector2(.5f, .5f),
            new Vector2(0, 0), new Vector2(4000, 720));

        titleText = Label(menuGroup.transform, gameTitle, 120, Gold,
            new Vector2(0, 250), new Vector2(1800, 170), FontStyle.Bold);
        AddShadow(titleText.gameObject, 4);

        Text sub = Label(menuGroup.transform, tagline, 38, Muted,
            new Vector2(0, 140), new Vector2(1400, 60), FontStyle.Italic);
        AddShadow(sub.gameObject, 2);

        bool hasTutorial = FindFirstObjectByType<TutorialDirector>() != null;
        float y = 10f;

        playButton = MakeButton(menuGroup.transform,
            hasTutorial ? "JUGAR TUTORIAL" : "JUGAR",
            new Vector2(0, y), OnPlayPressed);
        y -= 110f;

        if (hasTutorial)
        {
            MakeButton(menuGroup.transform, "SALTAR TUTORIAL",
                new Vector2(0, y), SkipTutorial);
            y -= 110f;
        }

        MakeButton(menuGroup.transform, "SALIR", new Vector2(0, y), QuitGame);

        Label(menuGroup.transform, "ENTER para jugar", 26, Muted,
            new Vector2(0, -470), new Vector2(800, 40), FontStyle.Normal);
    }

    void BuildPause(Transform parent)
    {
        pauseGroup = Layer(parent, "Pausa");
        Show(pauseGroup, false);

        Stretch(Img(pauseGroup.transform, "Velo",
            new Color(0f, 0f, 0f, .7f)));

        Label(pauseGroup.transform, "PAUSA", 96, Gold, new Vector2(0, 260),
            new Vector2(1000, 130), FontStyle.Bold);

        resumeButton = MakeButton(pauseGroup.transform, "CONTINUAR",
            new Vector2(0, 90), Resume);
        MakeButton(pauseGroup.transform, "REINICIAR NIVEL",
            new Vector2(0, -20), RestartLevel);
        MakeButton(pauseGroup.transform, "MENÚ PRINCIPAL",
            new Vector2(0, -130), GoToMainMenu);
        MakeButton(pauseGroup.transform, "SALIR",
            new Vector2(0, -240), QuitGame);

        Label(pauseGroup.transform, "ESC para continuar", 26, Muted,
            new Vector2(0, -400), new Vector2(800, 40), FontStyle.Normal);
    }

    void BuildDeath(Transform parent)
    {
        deathGroup = Layer(parent, "Muerte");
        Show(deathGroup, false);

        Stretch(Img(deathGroup.transform, "Velo",
            new Color(.08f, 0f, 0f, .5f)));

        deathTitle = Label(deathGroup.transform, "", 88, Danger,
            new Vector2(0, 250), new Vector2(1800, 130), FontStyle.Bold);
        AddShadow(deathTitle.gameObject, 4);

        riddleText = Label(deathGroup.transform, "", 42, Ivory,
            new Vector2(0, 140), new Vector2(1500, 70), FontStyle.Italic);
        AddShadow(riddleText.gameObject, 2);

        livesText = Label(deathGroup.transform, "", 30, Gold,
            new Vector2(0, 60), new Vector2(800, 50), FontStyle.Bold);
        AddShadow(livesText.gameObject, 2);
    }

    void BuildGameOver(Transform parent)
    {
        gameOverGroup = Layer(parent, "GameOver");
        Show(gameOverGroup, false);

        Stretch(Img(gameOverGroup.transform, "Velo",
            new Color(.12f, 0f, 0f, .78f)));

        Text title = Label(gameOverGroup.transform, "GAME OVER", 130,
            Danger, new Vector2(0, 220), new Vector2(1400, 160),
            FontStyle.Bold);
        AddShadow(title.gameObject, 4);

        Label(gameOverGroup.transform,
            "Te quedaste sin vidas. Tu gato sigue esperando...\nVolviendo al principio...",
            36, Muted, new Vector2(0, 90), new Vector2(1500, 110),
            FontStyle.Italic);

    }

    // --- piezas básicas

    static CanvasGroup Layer(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go.AddComponent<CanvasGroup>();
    }

    static Image Img(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    Text Label(Transform parent, string content, int size, Color color,
        Vector2 position, Vector2 boxSize, FontStyle style)
    {
        GameObject go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Place(text.rectTransform, new Vector2(.5f, .5f), position, boxSize);
        return text;
    }

    Button MakeButton(Transform parent, string label, Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        Image image = Framed(parent, "Boton_" + label, Color.white,
            Gold * .75f, position, new Vector2(480, 84), 2);

        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(.14f, .11f, .09f, 1f);
        colors.highlightedColor = new Color(.42f, .3f, .14f, 1f);
        colors.selectedColor = new Color(.42f, .3f, .14f, 1f);
        colors.pressedColor = new Color(.62f, .45f, .18f, 1f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Text text = Label(image.transform, label, 38, Ivory, Vector2.zero,
            new Vector2(480, 84), FontStyle.Bold);
        AddShadow(text.gameObject, 2);

        return button;
    }

    static void Stretch(Graphic graphic) => Stretch(graphic.rectTransform);

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// Rectángulo con borde: un marco detrás y el relleno dentro.
    static Image Framed(Transform parent, string name, Color fill,
        Color border, Vector2 position, Vector2 size, float px)
    {
        Image frame = Img(parent, name + "_Borde", border);
        frame.raycastTarget = false;
        Place(frame.rectTransform, new Vector2(.5f, .5f), position,
            size + Vector2.one * px * 2f);

        Image inner = Img(frame.transform, name, fill);
        RectTransform rect = inner.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * px;
        rect.offsetMax = -Vector2.one * px;
        return inner;
    }

    static void AddShadow(GameObject go, float px)
    {
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .8f);
        shadow.effectDistance = new Vector2(px, -px);
    }
}
