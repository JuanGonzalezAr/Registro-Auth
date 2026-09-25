using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Juego "Atrapa el objetivo": tienes X segundos para hacer clic en un objetivo
/// que cambia de posición (y se hace más pequeño) cada vez que lo tocas.
/// Al terminar, el puntaje se guarda en Firebase con AuthManager y se recarga la tabla.
///
/// Toda la interfaz del juego se crea por código, así que no hay que armar nada en la escena.
/// Si no agregas este componente a mano, se agrega solo al objeto que tiene AuthManager.
/// </summary>
public class ClickGame : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float gameDuration = 15f;
    [SerializeField] private float startTargetSize = 170f;
    [SerializeField] private float minTargetSize = 70f;
    [SerializeField] private float shrinkPerHit = 6f;

    [Header("Opcional: si lo dejas vacío se crea un botón 'Jugar' en el ProfilePanel")]
    [SerializeField] private Button playButton;

    private AuthManager auth;
    private Canvas canvas;

    private GameObject gamePanel;
    private RectTransform playArea;
    private RectTransform target;
    private TMP_Text timerText;
    private TMP_Text hitsText;
    private TMP_Text resultText;
    private GameObject backButton;
    private GameObject startButton;

    private bool playing;
    private float timeLeft;
    private int hits;

    private static readonly Color Navy   = new Color32(0x10, 0x10, 0x5A, 0xFF);
    private static readonly Color Accent = new Color32(0xFF, 0x7A, 0x1A, 0xFF);
    private static readonly Color BgColor  = new Color32(0xF4, 0xF8, 0xFF, 0xFF);

    // Se agrega solo si nadie lo puso en la escena.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (FindAnyObjectByType<ClickGame>() != null) return;
        AuthManager am = FindAnyObjectByType<AuthManager>();
        if (am != null) am.gameObject.AddComponent<ClickGame>();
    }

    private void Start()
    {
        auth = GetComponent<AuthManager>();
        if (auth == null) auth = FindAnyObjectByType<AuthManager>();

        canvas = FindAnyObjectByType<Canvas>();
        if (auth == null || canvas == null)
        {
            Debug.LogError("[ClickGame] Falta AuthManager o Canvas en la escena.");
            enabled = false;
            return;
        }

        BuildGameUI();
        SetupPlayButton();
        gamePanel.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Flujo del juego
    // ------------------------------------------------------------------
    public void OpenGame()
    {
        if (!auth.IsLoggedIn) return;

        if (auth.ProfilePanelObject != null) auth.ProfilePanelObject.SetActive(false);
        gamePanel.SetActive(true);
        gamePanel.transform.SetAsLastSibling();

        playing = false;
        target.gameObject.SetActive(false);
        backButton.SetActive(true);
        startButton.SetActive(true);
        timerText.text = "Tiempo: " + gameDuration.ToString("0.0");
        hitsText.text = "Aciertos: 0";
        resultText.text = "Haz clic en el objetivo naranja todas las veces que puedas.\nTu récord: " + auth.CurrentScore;
    }

    private void StartRound()
    {
        hits = 0;
        timeLeft = gameDuration;
        playing = true;

        startButton.SetActive(false);
        backButton.SetActive(false);
        resultText.text = "";
        hitsText.text = "Aciertos: 0";

        target.sizeDelta = Vector2.one * startTargetSize;
        target.gameObject.SetActive(true);
        MoveTarget();
    }

    private void Update()
    {
        if (!playing) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            timerText.text = "Tiempo: 0.0";
            _ = EndRound();
            return;
        }
        timerText.text = "Tiempo: " + timeLeft.ToString("0.0");
    }

    private void OnTargetClicked()
    {
        if (!playing) return;

        hits++;
        hitsText.text = "Aciertos: " + hits;

        float size = Mathf.Max(minTargetSize, target.sizeDelta.x - shrinkPerHit);
        target.sizeDelta = Vector2.one * size;
        MoveTarget();
    }

    private async Task EndRound()
    {
        playing = false;
        target.gameObject.SetActive(false);

        int record = auth.CurrentScore;
        resultText.text = hits > record
            ? "¡Nuevo récord! " + hits + " aciertos\nGuardando en Firebase..."
            : "Hiciste " + hits + " aciertos (récord: " + record + ")";

        await auth.SubmitScoreAndRefreshAsync(hits);

        if (hits > record) resultText.text = "¡Nuevo récord guardado: " + hits + "!";
        startButton.SetActive(true);
        backButton.SetActive(true);
        startButton.GetComponentInChildren<TMP_Text>().text = "Jugar otra vez";
    }

    private void CloseGame()
    {
        playing = false;
        gamePanel.SetActive(false);
        if (auth.ProfilePanelObject != null) auth.ProfilePanelObject.SetActive(true);
        auth.ShowLeaderboardButtonClick();
    }

    private void MoveTarget()
    {
        Rect area = playArea.rect;
        float half = target.sizeDelta.x / 2f;
        float x = Random.Range(area.xMin + half, area.xMax - half);
        float y = Random.Range(area.yMin + half, area.yMax - half);
        target.anchoredPosition = new Vector2(x, y);
    }

    // ------------------------------------------------------------------
    // Construcción de la interfaz por código
    // ------------------------------------------------------------------
    private void SetupPlayButton()
    {
        if (playButton == null && auth.ProfilePanelObject != null)
        {
            RectTransform parent = auth.ProfilePanelObject.GetComponent<RectTransform>();
            playButton = CreateButton(parent, "PlayGameButton", "Jugar", Accent, Color.white, 30);
            RectTransform rt = playButton.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(220f, 70f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
        }

        if (playButton != null) playButton.onClick.AddListener(OpenGame);
    }

    private void BuildGameUI()
    {
        // Panel a pantalla completa
        gamePanel = new GameObject("GamePanel", typeof(RectTransform), typeof(Image));
        gamePanel.transform.SetParent(canvas.transform, false);
        Stretch(gamePanel.GetComponent<RectTransform>(), 0, 0, 0, 0);
        gamePanel.GetComponent<Image>().color = BgColor;

        RectTransform root = gamePanel.GetComponent<RectTransform>();

        TMP_Text title = CreateText(root, "Title", "Atrapa el objetivo", 54, Navy, TextAlignmentOptions.Center);
        AnchorTop(title.rectTransform, -30f, 80f);

        timerText = CreateText(root, "Timer", "", 38, Navy, TextAlignmentOptions.Left);
        AnchorTop(timerText.rectTransform, -115f, 55f);
        timerText.rectTransform.offsetMin = new Vector2(40f, timerText.rectTransform.offsetMin.y);

        hitsText = CreateText(root, "Hits", "", 38, Navy, TextAlignmentOptions.Right);
        AnchorTop(hitsText.rectTransform, -115f, 55f);
        hitsText.rectTransform.offsetMax = new Vector2(-40f, hitsText.rectTransform.offsetMax.y);

        // Zona de juego
        GameObject areaGO = new GameObject("PlayArea", typeof(RectTransform), typeof(Image));
        areaGO.transform.SetParent(root, false);
        playArea = areaGO.GetComponent<RectTransform>();
        Stretch(playArea, 40f, 40f, 190f, 150f);
        areaGO.GetComponent<Image>().color = new Color32(0xE2, 0xE8, 0xF5, 0xFF);

        // Objetivo
        Button targetBtn = CreateButton(playArea, "Target", "", Accent, Color.white, 1);
        target = targetBtn.GetComponent<RectTransform>();
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0.5f, 0.5f);
        targetBtn.onClick.AddListener(OnTargetClicked);

        // Texto de resultado (centro)
        resultText = CreateText(playArea, "Result", "", 40, Navy, TextAlignmentOptions.Center);
        Stretch(resultText.rectTransform, 30f, 30f, 30f, 30f);
        resultText.raycastTarget = false;

        // Botones inferiores
        startButton = CreateButton(root, "StartButton", "Empezar", Navy, Color.white, 34).gameObject;
        PlaceBottom(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(150f, 30f));
        startButton.GetComponent<Button>().onClick.AddListener(StartRound);

        backButton = CreateButton(root, "BackButton", "Volver", Color.white, Navy, 30).gameObject;
        PlaceBottom(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-150f, 30f));
        backButton.GetComponent<Button>().onClick.AddListener(CloseGame);
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Color bg, Color fg, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;

        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text t = CreateText(go.GetComponent<RectTransform>(), "Label", label, fontSize, fg, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 0, 0, 0, 0);
            t.raycastTarget = false;
        }
        return go.GetComponent<Button>();
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        return t;
    }

    private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorTop(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private static void PlaceBottom(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260f, 80f);
        rt.anchoredPosition = pos;
    }
}
