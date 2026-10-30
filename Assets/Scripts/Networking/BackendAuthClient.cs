using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackendAuthClient : MonoBehaviour {
    [Serializable]
    public class AuthRequest {
        public string username;
        public string password;
    }

    [Serializable]
    public class AuthResponse {
        public int user_id;
        public string username;
        public string created_at;
        public string session_token;
    }

    [Serializable]
    public class ValidateResponse {
        public string session_token;
        public int user_id;
        public string username;
        public string expires_at;
    }

    [Serializable]
    public class DeleteAccountResponse {
        public bool deleted;
        public int user_id;
        public string username;
    }

    [Serializable]
    public class ErrorResponse {
        public string detail;
    }

    private string baseUrl;
    private bool persistSessionToken = false;

    public string SessionToken { get; private set; }
    public int CachedUserId { get; private set; }
    public string CachedUsername { get; private set; } = string.Empty;
    public bool IsDeletingAccount { get; private set; }

    private const string SessionTokenKey = "";

    private void Awake() {
        if (persistSessionToken) {
            SessionToken = PlayerPrefs.GetString(SessionTokenKey);
        }
    }

    public void ConfigureBaseUrl(string newBaseUrl) {
        if (!string.IsNullOrWhiteSpace(newBaseUrl)) {
            baseUrl = newBaseUrl.TrimEnd('/');
        }
    }

    public Coroutine Register(string username, string password, Action<AuthResponse> onSuccess, Action<string> onError) {
        return StartCoroutine(SendAuthRequest(URL.AuthRegisterPath, username, password, onSuccess, onError));
    }

    public Coroutine Login(string username, string password, Action<AuthResponse> onSuccess, Action<string> onError) {
        return StartCoroutine(SendAuthRequest(URL.AuthLoginPath, username, password, onSuccess, onError));
    }

    public Coroutine ValidateCurrentSession(Action<ValidateResponse> onSuccess, Action<string> onError) {
        return StartCoroutine(ValidateSessionRoutine(onSuccess, onError));
    }

    public Coroutine DeleteCurrentAccount(Action<DeleteAccountResponse> onSuccess, Action<string> onError) {
        return StartCoroutine(DeleteAccountRoutine(onSuccess, onError));
    }

    public bool HasStoredSessionToken() {
        return !string.IsNullOrWhiteSpace(SessionToken);
    }

    public void ClearSession() {
        SessionToken = string.Empty;
        CachedUserId = 0;
        CachedUsername = string.Empty;
        if (persistSessionToken) {
            PlayerPrefs.DeleteKey(SessionTokenKey);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator SendAuthRequest(string path, string username, string password, Action<AuthResponse> onSuccess, Action<string> onError) {
        AuthRequest body = new AuthRequest {
            username = username,
            password = password
        };

        string json = JsonUtility.ToJson(body);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        string url = $"{(string.IsNullOrWhiteSpace(baseUrl) ? URL.BaseUrl : baseUrl)}{path}";

        using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(payload);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(ExtractErrorMessage(request));
            yield break;
        }

        AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
        SessionToken = response.session_token;
        CachedUserId = response.user_id;
        CachedUsername = response.username;

        if (persistSessionToken) {
            PlayerPrefs.SetString(SessionTokenKey, SessionToken);
            PlayerPrefs.Save();
        }

        onSuccess?.Invoke(response);
    }

    private IEnumerator ValidateSessionRoutine(Action<ValidateResponse> onSuccess, Action<string> onError) {
        if (string.IsNullOrWhiteSpace(SessionToken)) {
            onError?.Invoke("No session token found.");
            yield break;
        }

        string url = $"{(string.IsNullOrWhiteSpace(baseUrl) ? URL.BaseUrl : baseUrl)}{URL.AuthValidatePath}?token={UnityWebRequest.EscapeURL(SessionToken)}";
        using UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(ExtractErrorMessage(request));
            yield break;
        }

        ValidateResponse response = JsonUtility.FromJson<ValidateResponse>(request.downloadHandler.text);
        CachedUserId = response.user_id;
        CachedUsername = response.username;
        onSuccess?.Invoke(response);
    }

    private IEnumerator DeleteAccountRoutine(Action<DeleteAccountResponse> onSuccess, Action<string> onError) {
        if (IsDeletingAccount) {
            onError?.Invoke("Account deletion is already in progress.");
            yield break;
        }
        if (!HasStoredSessionToken()) {
            onError?.Invoke("Please log in before deleting your account.");
            yield break;
        }

        string token = SessionToken;
        string url = $"{(string.IsNullOrWhiteSpace(baseUrl) ? URL.BaseUrl : baseUrl)}{URL.AuthDeleteAccountPath}";
        IsDeletingAccount = true;
        try {
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) {
                if (request.responseCode == 401 && SessionToken == token) {
                    ClearSession();
                }
                onError?.Invoke(ExtractErrorMessage(request));
                yield break;
            }

            DeleteAccountResponse response = null;
            try {
                response = JsonUtility.FromJson<DeleteAccountResponse>(request.downloadHandler.text);
            } catch (ArgumentException) {
                // Report a malformed response without treating the account as deleted.
            }
            if (response == null || !response.deleted || response.user_id <= 0) {
                onError?.Invoke("The server returned an invalid account deletion response.");
                yield break;
            }

            if (SessionToken == token) {
                ClearSession();
            }
            onSuccess?.Invoke(response);
        } finally {
            IsDeletingAccount = false;
        }
    }

    public int GetCachedUserId() {
        return CachedUserId;
    }

    public string GetCachedUsername() {
        return CachedUsername;
    }

    private string ExtractErrorMessage(UnityWebRequest request) {
        if (request.downloadHandler != null && !string.IsNullOrWhiteSpace(request.downloadHandler.text)) {
            try {
                ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                if (!string.IsNullOrWhiteSpace(error?.detail)) {
                    return error.detail;
                }
            }
            catch {
            }
        }

        return request.error;
    }
}
