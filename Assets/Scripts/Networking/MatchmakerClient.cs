using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class MatchmakerClient : MonoBehaviour {
    [Serializable]
    public class JoinRequest {
        public int user_id;
        public string username;
    }

    [Serializable]
    public class CharacterSelectionRequest {
        public int user_id;
        public string character_id;
    }

    [Serializable]
    public class CharacterSelectionCancelRequest {
        public int user_id;
    }

    [Serializable]
    public class MatchResponse {
        public string status;
        public string room_id;
        public string world_endpoint;
        public int seed;
        public PlayerEntry[] players;
        public int queue_size;
        public int queue_position;
    }

    [Serializable]
    public class PlayerEntry {
        public int user_id;
        public string username;
    }

    [Serializable]
    public class CharacterSelectionResponse {
        public string status;
        public bool all_ready;
        public string room_id;
        public string world_endpoint;
        public int seed;
        public int selected_count;
        public int player_count;
    }

    public Coroutine JoinQueue(
        int userId,
        string username,
        Action<MatchResponse> onMatched,
        Action<MatchResponse> onQueued,
        Action<string> onError
    ) {
        return StartCoroutine(JoinQueueRoutine(userId, username, onMatched, onQueued, onError));
    }

    public Coroutine PollStatus(int userId, Action<MatchResponse> onMatched, Action<MatchResponse> onQueued, Action<string> onError) {
        return StartCoroutine(PollStatusRoutine(userId, onMatched, onQueued, onError));
    }

    public Coroutine SubmitCharacterSelection(
        int userId,
        string characterId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        return StartCoroutine(SubmitCharacterSelectionRoutine(userId, characterId, onSuccess, onError));
    }

    public Coroutine CancelCharacterSelection(
        int userId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        return StartCoroutine(CancelCharacterSelectionRoutine(userId, onSuccess, onError));
    }

    public Coroutine PollCharacterSelectionStatus(
        int userId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        return StartCoroutine(PollCharacterSelectionStatusRoutine(userId, onSuccess, onError));
    }

    private IEnumerator JoinQueueRoutine(
        int userId,
        string username,
        Action<MatchResponse> onMatched,
        Action<MatchResponse> onQueued,
        Action<string> onError
    ) {
        string url = $"{URL.BaseUrl}{URL.MatchJoinPath}";
        JoinRequest body = new JoinRequest { user_id = userId, username = username };
        string json = JsonUtility.ToJson(body);

        using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(request.error);
            yield break;
        }

        MatchResponse response = JsonUtility.FromJson<MatchResponse>(request.downloadHandler.text);
        if (response.status == "matched") {
            onMatched?.Invoke(response);
        }
        else {
            onQueued?.Invoke(response);
        }
    }

    private IEnumerator PollStatusRoutine(int userId, Action<MatchResponse> onMatched, Action<MatchResponse> onQueued, Action<string> onError) {
        string url = $"{URL.BaseUrl}{URL.MatchStatusPath}/{userId}";
        using UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(request.error);
            yield break;
        }

        MatchResponse response = JsonUtility.FromJson<MatchResponse>(request.downloadHandler.text);
        if (response.status == "matched") {
            onMatched?.Invoke(response);
        }
        else {
            onQueued?.Invoke(response);
        }
    }

    private IEnumerator SubmitCharacterSelectionRoutine(
        int userId,
        string characterId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        CharacterSelectionRequest body = new CharacterSelectionRequest {
            user_id = userId,
            character_id = characterId
        };

        yield return SendCharacterSelectionPost(URL.MatchCharacterSelectionPath, body, onSuccess, onError);
    }

    private IEnumerator CancelCharacterSelectionRoutine(
        int userId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        CharacterSelectionCancelRequest body = new CharacterSelectionCancelRequest { user_id = userId };

        yield return SendCharacterSelectionPost(URL.MatchCharacterSelectionCancelPath, body, onSuccess, onError);
    }

    private IEnumerator PollCharacterSelectionStatusRoutine(
        int userId,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        string url = $"{URL.BaseUrl}{URL.MatchCharacterSelectionStatusPath}/{userId}";
        using UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(request.error);
            yield break;
        }

        CharacterSelectionResponse response = JsonUtility.FromJson<CharacterSelectionResponse>(request.downloadHandler.text);
        onSuccess?.Invoke(response);
    }

    private IEnumerator SendCharacterSelectionPost(
        string path,
        object body,
        Action<CharacterSelectionResponse> onSuccess,
        Action<string> onError
    ) {
        string url = $"{URL.BaseUrl}{path}";
        string json = JsonUtility.ToJson(body);

        using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            onError?.Invoke(request.error);
            yield break;
        }

        CharacterSelectionResponse response = JsonUtility.FromJson<CharacterSelectionResponse>(request.downloadHandler.text);
        onSuccess?.Invoke(response);
    }
}
