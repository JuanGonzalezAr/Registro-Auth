using System.Collections;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class AuthManager : MonoBehaviour
{
    private string Url = "https://sid-restapi.onrender.com";
    string token = "";
    string username = "";
    int currentScore = 0;

    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private TMP_Text profileUsernameText;
    [SerializeField] private TMP_Text statusText;

    // --- Nuevo: puntaje y tabla de posiciones ---
    [SerializeField] private TMP_Text scoreText;        // muestra el puntaje del usuario logueado
    [SerializeField] private TMP_Text leaderboardText;   // lista de "usuario - puntaje" ordenada desc

    void Start()
    {
        token = PlayerPrefs.GetString("token", "");
        username = PlayerPrefs.GetString("username", "");

        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(username))
        {
           StartCoroutine(GetProfile());
        }
        else
        {
            ShowLogin();
        }
    }
    public void RegisterButtonClick()
    {
        StartCoroutine(RegisterUser());
    }
    public void LoginButtonClick()
    {
        StartCoroutine(Login());
    }

    // Nuevo cambio entre pantallas
    public void ShowRegisterScreen()
    {
        ShowRegister();
    }
    public void ShowLoginScreen()
    {
        ShowLogin();
    }
    //Fin cambio entre pantallas

    public void LogoutButtonClick()
    {
        token = "";
        username = "";
        currentScore = 0;
        PlayerPrefs.DeleteKey("token");
        PlayerPrefs.DeleteKey("username");
        ShowLogin();
    }

    // Llamar desde un botón "Actualizar puntaje". Lee el valor de un InputField
    // llamado "ScoreInputField" y lo envía como el nuevo puntaje del usuario.
    public void UpdateScoreButtonClick()
    {
        TMP_InputField scoreField = GameObject.Find("ScoreInputField").GetComponent<TMP_InputField>();
        int nuevoPuntaje;
        if (int.TryParse(scoreField.text, out nuevoPuntaje))
        {
            StartCoroutine(UpdateScore(nuevoPuntaje));
        }
        else
        {
            SetStatus("Ingresa un número válido de puntaje.");
        }
    }

    // Llamar desde un botón "Ver tabla de puntajes".
    public void ShowLeaderboardButtonClick()
    {
        StartCoroutine(GetLeaderboard());
    }

    IEnumerator GetProfile()
    {
        UnityWebRequest www = UnityWebRequest.Get(Url + "/api/usuarios/" + username);
        www.SetRequestHeader("x-token",token);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            LogoutButtonClick();
        }
        else
        {
            Debug.Log(www.downloadHandler.text);
            UserResponse userData = JsonUtility.FromJson<UserResponse>(www.downloadHandler.text);
            Debug.Log("User profile: " + userData.usuario.username);
            currentScore = (userData.usuario.data != null) ? userData.usuario.data.score : 0;
            ShowProfile(userData.usuario.username);
        }
    }
     IEnumerator RegisterLogin()
    {

        AuthData authData = new AuthData();

        authData.username = GameObject.Find("UsuarioRegister").GetComponent<TMP_InputField>().text;
        authData.password = GameObject.Find("ContraseñaRegister").GetComponent<TMP_InputField>().text;

        string jsonData = JsonUtility.ToJson(authData);

        Debug.Log("Sending JSON data: " + jsonData);
        UnityWebRequest www = UnityWebRequest.Post(Url + "/api/auth/login", jsonData, "application/json");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            SetStatus("Usuario o contraseña incorrectos.");
        }
        else
        {
            Debug.Log(www.downloadHandler.text);
            UserResponse userResponse = JsonUtility.FromJson<UserResponse>(www.downloadHandler.text);

            token = userResponse.token;
            username = userResponse.usuario.username;
            currentScore = (userResponse.usuario.data != null) ? userResponse.usuario.data.score : 0;

            PlayerPrefs.SetString("token", token);
            PlayerPrefs.SetString("username", username);

            ShowProfile(username);
        }
    }
    IEnumerator Login()
    {

        AuthData authData = new AuthData();

        authData.username = GameObject.Find("UsuarioLogIn").GetComponent<TMP_InputField>().text;
        authData.password = GameObject.Find("ContraseñaLogIn").GetComponent<TMP_InputField>().text;

        string jsonData = JsonUtility.ToJson(authData);

        Debug.Log("Sending JSON data: " + jsonData);
        UnityWebRequest www = UnityWebRequest.Post(Url + "/api/auth/login", jsonData, "application/json");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            SetStatus("Usuario o contraseña incorrectos.");
        }
        else
        {
            Debug.Log(www.downloadHandler.text);
            UserResponse userResponse = JsonUtility.FromJson<UserResponse>(www.downloadHandler.text);

            token = userResponse.token;
            username = userResponse.usuario.username;
            currentScore = (userResponse.usuario.data != null) ? userResponse.usuario.data.score : 0;

            PlayerPrefs.SetString("token", token);
            PlayerPrefs.SetString("username", username);

            ShowProfile(username);
        }
    }
    IEnumerator RegisterUser()
    {

        AuthData authData = new AuthData();

        authData.username = GameObject.Find("UsuarioRegister").GetComponent<TMP_InputField>().text;
        authData.password = GameObject.Find("ContraseñaRegister").GetComponent<TMP_InputField>().text;

        string jsonData = JsonUtility.ToJson(authData);

        Debug.Log("Sending JSON data: " + jsonData);
        UnityWebRequest www = UnityWebRequest.Post(Url + "/api/usuarios", jsonData,"application/json");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            SetStatus("No se pudo registrar el usuario.");
        }
        else
        {
            Debug.Log(www.downloadHandler.text);
            UserResponse userResponse = JsonUtility.FromJson<UserResponse>(www.downloadHandler.text);
            Debug.Log("User registered: " + userResponse.usuario.username);

            StartCoroutine(RegisterLogin());

        }
    }

    // --- Nuevo: actualizar el puntaje del usuario autenticado ---
    // NOTA: revisa en la consola (Debug.Log del paso de Login/GetProfile) el nombre
    // exacto del campo de puntaje que devuelve tu API (por ejemplo "score" o "puntaje")
    // y ajusta la clase UserData si es distinto.
    IEnumerator UpdateScore(int nuevoPuntaje)
    {
        UpdateScoreRequest req = new UpdateScoreRequest();
        req.username = username;
        req.data = new UserGameData();
        req.data.score = nuevoPuntaje;

        string jsonData = JsonUtility.ToJson(req);

        Debug.Log("Sending score JSON: " + jsonData);

        UnityWebRequest www = new UnityWebRequest(Url + "/api/usuarios", "PATCH");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("x-token", token);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            SetStatus("No se pudo actualizar el puntaje.");
        }
        else
        {
            Debug.Log("Score actualizado: " + www.downloadHandler.text);
            currentScore = nuevoPuntaje;
            if (scoreText != null) scoreText.text = "Puntaje: " + currentScore;
            SetStatus("Puntaje actualizado correctamente.");
        }
    }

    // --- Nuevo: obtener y mostrar la tabla de puntajes ordenada de mayor a menor ---
    IEnumerator GetLeaderboard()
    {
        UnityWebRequest www = UnityWebRequest.Get(Url + "/api/usuarios");
        www.SetRequestHeader("x-token", token);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
            SetStatus("No se pudo cargar la tabla de puntajes.");
            yield break;
        }

        string raw = www.downloadHandler.text;
        Debug.Log("Leaderboard raw response: " + raw);

        UserData[] usuarios = ParseUsersList(raw);

        if (usuarios == null || usuarios.Length == 0)
        {
            SetStatus("No hay usuarios registrados todavía.");
            yield break;
        }

        UserData[] ordenados = usuarios.OrderByDescending(u => (u.data != null) ? u.data.score : 0).ToArray();
        DisplayLeaderboard(ordenados);
    }

    // La API puede responder un arreglo plano "[ {...}, {...} ]" o un objeto
    // "{ "usuarios": [ {...}, {...} ] }". JsonUtility no puede parsear un arreglo
    // en la raíz, así que si detectamos "[" lo envolvemos antes de parsear.
    private UserData[] ParseUsersList(string raw)
    {
        raw = raw.Trim();

        if (raw.StartsWith("["))
        {
            string wrapped = "{\"items\":" + raw + "}";
            UsersArrayWrapper wrapper = JsonUtility.FromJson<UsersArrayWrapper>(wrapped);
            return wrapper.items;
        }

        UsersListResponse response = JsonUtility.FromJson<UsersListResponse>(raw);
        return response.usuarios;
    }

    private void DisplayLeaderboard(UserData[] usuarios)
    {
        if (leaderboardText == null) return;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < usuarios.Length; i++)
        {
            int puntaje = (usuarios[i].data != null) ? usuarios[i].data.score : 0;
            sb.AppendLine((i + 1) + ".  " + usuarios[i].username + "   -   " + puntaje);        
        }
             leaderboardText.text = sb.ToString();
    }

    private void ShowLogin()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
    }

    // Mostrar Pantalla de Registro
    private void ShowRegister()
    {
        if (registerPanel != null) registerPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
    }

    private void ShowProfile(string displayName)
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(true);
        if (profileUsernameText != null) profileUsernameText.text = displayName;
        if (scoreText != null) scoreText.text = "Puntaje: " + currentScore;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
[System.Serializable]

public class AuthData
{
    public string username;
    public string password;

}

[System.Serializable]
public class UserGameData
{
    public int score;
}

[System.Serializable]
public class UpdateScoreRequest
{
    public string username;
    public UserGameData data;
}

[System.Serializable]
public class  UserResponse
{
    public UserData usuario;
    public string token;
}
[System.Serializable]

public class UserData
{
    public string _id;
    public string username;
    public string password;
    public bool estado;
    public UserGameData data;
}

[System.Serializable]
public class UsersListResponse
{
    public UserData[] usuarios;
}

[System.Serializable]
public class UsersArrayWrapper
{
    public UserData[] items;
}