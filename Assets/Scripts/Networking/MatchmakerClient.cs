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

    public Coroutine JoinQueue(int userId, string username, Action<MatchResponse> onMatched, Action<string> onError) {
        return StartCoroutine(JoinQueueRoutine(userId, username, onMatched, onError));
    }

    public Coroutine PollStatus(int userId, Action<MatchResponse> onMatched, Action<MatchResponse> onQueued, Action<string> onError) {
        return StartCoroutine(PollStatusRoutine(userId, onMatched, onQueued, onError));
    }

    private IEnumerator JoinQueueRoutine(int userId, string username, Action<MatchResponse> onMatched, Action<string> onError) {
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
}
