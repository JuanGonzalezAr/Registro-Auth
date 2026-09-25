using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using UnityEngine;

/// <summary>
/// Autenticación (registro, login, recuperación de contraseña) con Firebase Auth
/// y guardado de datos de usuario + puntajes en Firebase Realtime Database.
///
/// Estructura en la base de datos:
///   users/{uid}  -> { username, email, pais, edad, createdAt }
///   scores/{uid} -> { username, score, updatedAt }
/// </summary>
public class AuthManager : MonoBehaviour
{
    [Header("Firebase")]
    [Tooltip("Déjalo vacío si tu google-services.json ya trae 'firebase_url'. " +
             "Si no, pega la URL de tu Realtime Database, ej: https://mi-proyecto-default-rtdb.firebaseio.com/")]
    [SerializeField] private string databaseUrl = "";

    [Header("Paneles")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject profilePanel;

    [Header("Textos")]
    [SerializeField] private TMP_Text profileUsernameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text scoreText;        // puntaje del usuario logueado
    [SerializeField] private TMP_Text leaderboardText;  // tabla "usuario - puntaje"

    [Header("Autor (visible en la interfaz)")]
    [SerializeField] private TMP_Text authorText;
    [SerializeField] private string authorFullName = "Tu Nombre Completo";

    [Header("Puntajes")]
    [Tooltip("Si está activo, solo se guarda el puntaje cuando supera al anterior (récord).")]
    [SerializeField] private bool saveOnlyIfHigher = true;
    [SerializeField] private int leaderboardSize = 10;

    private FirebaseAuth auth;
    private DatabaseReference db;
    private bool firebaseReady = false;

    private string username = "";
    private int currentScore = 0;

    // ------------------------------------------------------------------
    // Inicialización
    // ------------------------------------------------------------------
    private async void Start()
    {
        if (authorText != null) authorText.text = authorFullName;
        ShowLogin();
        SetStatus("Conectando con Firebase...");

        DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError("No se pudieron resolver las dependencias de Firebase: " + status);
            SetStatus("Error al iniciar Firebase: " + status);
            return;
        }

        FirebaseApp app = FirebaseApp.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        FirebaseDatabase database = string.IsNullOrEmpty(databaseUrl)
            ? FirebaseDatabase.DefaultInstance
            : FirebaseDatabase.GetInstance(app, databaseUrl);
        db = database.RootReference;

        firebaseReady = true;
        SetStatus("");

        // Sesión persistente: Firebase recuerda al usuario entre ejecuciones.
        if (auth.CurrentUser != null)
        {
            await LoadProfile();
        }
    }

    // ------------------------------------------------------------------
    // Botones (conéctalos en el OnClick de cada Button)
    // ------------------------------------------------------------------
    public void RegisterButtonClick() { _ = RegisterUser(); }
    public void LoginButtonClick() { _ = Login(); }
    public void ForgotPasswordButtonClick() { _ = SendPasswordReset(); }
    public void ShowLeaderboardButtonClick() { _ = GetLeaderboard(); }

    public void ShowRegisterScreen() { ShowRegister(); }
    public void ShowLoginScreen() { ShowLogin(); }

    public void LogoutButtonClick()
    {
        if (auth != null) auth.SignOut();
        username = "";
        currentScore = 0;
        if (leaderboardText != null) leaderboardText.text = "";
        SetStatus("Sesión cerrada.");
        ShowLogin();
    }

    // Lee "ScoreInputField" y guarda el puntaje (para pruebas mientras haces el juego).
    public void UpdateScoreButtonClick()
    {
        string raw = GetInputText("ScoreInputField");
        if (int.TryParse(raw, out int nuevoPuntaje))
            _ = SaveScore(nuevoPuntaje);
        else
            SetStatus("Ingresa un número válido de puntaje.");
    }

    // Llama esto desde tu juego cuando termine una partida:
    //   FindObjectOfType<AuthManager>().SubmitScore(puntos);
    public void SubmitScore(int puntaje)
    {
        _ = SaveScore(puntaje);
    }

    // ------------------------------------------------------------------
    // Registro
    // ------------------------------------------------------------------
    private async Task RegisterUser()
    {
        if (!CheckReady()) return;

        string email = GetInputText("CorreoRegister").Trim();
        string user = GetInputText("UsuarioRegister").Trim();
        string password = GetInputText("ContraseñaRegister");
        string pais = GetInputText("PaisRegister").Trim();   // opcional
        string edadRaw = GetInputText("EdadRegister").Trim();   // opcional

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
        {
            SetStatus("Completa correo, usuario y contraseña.");
            return;
        }

        SetStatus("Registrando...");
        try
        {
            await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser fbUser = auth.CurrentUser;

            // Guarda el nombre en el perfil de Auth
            await fbUser.UpdateUserProfileAsync(new UserProfile { DisplayName = user });

            // Datos del usuario en la base de datos
            var userData = new Dictionary<string, object>
            {
                { "username",  user },
                { "email",     email },
                { "pais",      pais },
                { "createdAt", ServerValue.Timestamp }
            };
            if (int.TryParse(edadRaw, out int edad)) userData["edad"] = edad;

            await db.Child("users").Child(fbUser.UserId).SetValueAsync(userData);

            // Puntaje inicial
            var scoreData = new Dictionary<string, object>
            {
                { "username",  user },
                { "score",     0 },
                { "updatedAt", ServerValue.Timestamp }
            };
            await db.Child("scores").Child(fbUser.UserId).SetValueAsync(scoreData);

            Debug.Log("Usuario registrado: " + fbUser.UserId);
            await LoadProfile();
            SetStatus("¡Registro exitoso!");
        }
        catch (Exception e)
        {
            HandleError(e, "No se pudo registrar el usuario.");
        }
    }

    // ------------------------------------------------------------------
    // Login
    // ------------------------------------------------------------------
    private async Task Login()
    {
        if (!CheckReady()) return;

        // Acepta un campo "CorreoLogIn"; si no existe usa "UsuarioLogIn" (debe contener el correo).
        string email = GetInputText("CorreoLogIn", "UsuarioLogIn").Trim();
        string password = GetInputText("ContraseñaLogIn");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SetStatus("Ingresa correo y contraseña.");
            return;
        }

        SetStatus("Iniciando sesión...");
        try
        {
            await auth.SignInWithEmailAndPasswordAsync(email, password);
            Debug.Log("Login OK: " + auth.CurrentUser.UserId);
            await LoadProfile();
            SetStatus("");
        }
        catch (Exception e)
        {
            HandleError(e, "Correo o contraseña incorrectos.");
        }
    }

    // ------------------------------------------------------------------
    // Recuperar contraseña
    // ------------------------------------------------------------------
    private async Task SendPasswordReset()
    {
        if (!CheckReady()) return;

        string email = GetInputText("CorreoLogIn", "UsuarioLogIn", "CorreoRecuperar").Trim();
        if (string.IsNullOrEmpty(email))
        {
            SetStatus("Escribe tu correo para recuperar la contraseña.");
            return;
        }

        try
        {
            await auth.SendPasswordResetEmailAsync(email);
            SetStatus("Te enviamos un correo para restablecer la contraseña (revisa spam).");
        }
        catch (Exception e)
        {
            HandleError(e, "No se pudo enviar el correo de recuperación.");
        }
    }

    // ------------------------------------------------------------------
    // Perfil
    // ------------------------------------------------------------------
    private async Task LoadProfile()
    {
        FirebaseUser fbUser = auth.CurrentUser;
        if (fbUser == null) { ShowLogin(); return; }

        try
        {
            DataSnapshot userSnap = await db.Child("users").Child(fbUser.UserId).GetValueAsync();
            username = userSnap.Exists && userSnap.Child("username").Value != null
                ? userSnap.Child("username").Value.ToString()
                : (string.IsNullOrEmpty(fbUser.DisplayName) ? fbUser.Email : fbUser.DisplayName);

            DataSnapshot scoreSnap = await db.Child("scores").Child(fbUser.UserId).Child("score").GetValueAsync();
            currentScore = scoreSnap.Exists ? Convert.ToInt32(scoreSnap.Value) : 0;

            ShowProfile(username);
        }
        catch (Exception e)
        {
            HandleError(e, "No se pudo cargar el perfil.");
            LogoutButtonClick();
        }
    }

    // ------------------------------------------------------------------
    // Puntajes
    // ------------------------------------------------------------------
    private async Task SaveScore(int nuevoPuntaje)
    {
        if (!CheckReady()) return;
        FirebaseUser fbUser = auth.CurrentUser;
        if (fbUser == null) { SetStatus("Debes iniciar sesión."); return; }

        if (saveOnlyIfHigher && nuevoPuntaje <= currentScore)
        {
            SetStatus("Puntaje " + nuevoPuntaje + " no supera tu récord (" + currentScore + ").");
            return;
        }

        try
        {
            var scoreData = new Dictionary<string, object>
            {
                { "username",  username },
                { "score",     nuevoPuntaje },
                { "updatedAt", ServerValue.Timestamp }
            };
            await db.Child("scores").Child(fbUser.UserId).UpdateChildrenAsync(scoreData);

            currentScore = nuevoPuntaje;
            if (scoreText != null) scoreText.text = "Puntaje: " + currentScore;
            SetStatus("Puntaje guardado.");
        }
        catch (Exception e)
        {
            HandleError(e, "No se pudo guardar el puntaje.");
        }
    }

    private async Task GetLeaderboard()
    {
        if (!CheckReady()) return;

        try
        {
            // Firebase ordena ascendente: tomamos los últimos N y los invertimos.
            DataSnapshot snap = await db.Child("scores")
                                        .OrderByChild("score")
                                        .LimitToLast(leaderboardSize)
                                        .GetValueAsync();

            var filas = new List<KeyValuePair<string, int>>();
            foreach (DataSnapshot child in snap.Children)
            {
                string user = child.Child("username").Value?.ToString() ?? "(sin nombre)";
                int score = child.Child("score").Value != null ? Convert.ToInt32(child.Child("score").Value) : 0;
                filas.Add(new KeyValuePair<string, int>(user, score));
            }
            filas.Reverse();

            if (filas.Count == 0)
            {
                SetStatus("No hay puntajes todavía.");
                if (leaderboardText != null) leaderboardText.text = "";
                return;
            }

            DisplayLeaderboard(filas);
        }
        catch (Exception e)
        {
            HandleError(e, "No se pudo cargar la tabla de puntajes.");
        }
    }

    private void DisplayLeaderboard(List<KeyValuePair<string, int>> filas)
    {
        if (leaderboardText == null) return;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < filas.Count; i++)
            sb.AppendLine((i + 1) + ".  " + filas[i].Key + "   -   " + filas[i].Value);

        leaderboardText.text = sb.ToString();
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------
    private void ShowLogin()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
    }

    private void ShowRegister()
    {
        if (registerPanel != null) registerPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
    }

    private void ShowProfile(string displayName)
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(true);
        if (profileUsernameText != null) profileUsernameText.text = displayName;
        if (scoreText != null) scoreText.text = "Puntaje: " + currentScore;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        if (!string.IsNullOrEmpty(message)) Debug.Log("[AuthManager] " + message);
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------
    private bool CheckReady()
    {
        if (!firebaseReady) SetStatus("Firebase aún no está listo, espera un momento...");
        return firebaseReady;
    }

    // Devuelve el texto del primer TMP_InputField activo encontrado con alguno de esos nombres.
    private string GetInputText(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) continue;
            TMP_InputField field = go.GetComponent<TMP_InputField>();
            if (field != null) return field.text;
        }
        return "";
    }

    private void HandleError(Exception e, string fallback)
    {
        Debug.LogError(e);

        // await entrega la excepción directa; ContinueWith la envuelve en AggregateException.
        Exception ex = e is AggregateException agg ? agg.GetBaseException() : e;

        if (ex is FirebaseException fe)
        {
            switch ((AuthError)fe.ErrorCode)
            {
                case AuthError.MissingEmail: SetStatus("Falta el correo."); return;
                case AuthError.MissingPassword: SetStatus("Falta la contraseña."); return;
                case AuthError.InvalidEmail: SetStatus("El correo no es válido."); return;
                case AuthError.WeakPassword: SetStatus("La contraseña debe tener al menos 6 caracteres."); return;
                case AuthError.EmailAlreadyInUse: SetStatus("Ese correo ya está registrado."); return;
                case AuthError.WrongPassword:
                case AuthError.UserNotFound:
                case AuthError.InvalidCredential: SetStatus("Correo o contraseña incorrectos."); return;
                case AuthError.TooManyRequests: SetStatus("Demasiados intentos, espera un momento."); return;
                case AuthError.NetworkRequestFailed: SetStatus("Sin conexión a internet."); return;
            }
        }

        SetStatus(fallback);
    }
}