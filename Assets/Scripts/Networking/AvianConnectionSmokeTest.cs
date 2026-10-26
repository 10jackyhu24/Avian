using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Opt-in integration check for the same transport configuration used by the menu.
public class AvianConnectionSmokeTest : MonoBehaviour {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfRequested() {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, "--avian-smoke-endpoint");
        if (index < 0 || index + 1 >= arguments.Length) {
            return;
        }
        var runner = new GameObject("Avian connection smoke test").AddComponent<AvianConnectionSmokeTest>();
        DontDestroyOnLoad(runner.gameObject);
        int.TryParse(GetArgument(arguments, "--avian-smoke-seconds", "30"), out int holdSeconds);
        int.TryParse(GetArgument(arguments, "--avian-smoke-frame-rate", "60"), out int frameRate);
        runner.StartCoroutine(runner.Run(arguments[index + 1], Mathf.Clamp(holdSeconds, 1, 180),
            Mathf.Clamp(frameRate, 1, 120), GetArgument(arguments, "--avian-smoke-character", "bird_01"),
            Array.IndexOf(arguments, "--avian-smoke-zone-check") >= 0));
    }

    private static string GetArgument(string[] arguments, string name, string fallback) {
        int index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : fallback;
    }

    private IEnumerator Run(string endpoint, int holdSeconds, int frameRate, string characterId, bool checkZoneStart) {
        // Headless players otherwise run uncapped, unlike a normal game window.
        Application.targetFrameRate = frameRate;
        Application.runInBackground = true;
        yield return null;
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.NetworkConfig.NetworkTransport is not UnityTransport transport) {
            Debug.LogError("[ConnectionSmoke] FAIL: NetworkManager/UnityTransport missing.");
            Application.Quit(1);
            yield break;
        }
        MatchmakingMenu menu = FindFirstObjectByType<MatchmakingMenu>();
        if (menu == null) {
            Debug.LogError("[ConnectionSmoke] FAIL: MatchmakingMenu missing.");
            Application.Quit(1);
            yield break;
        }
        GameState.SetPendingMatchWorld("connection-smoke", 100000, endpoint);
        GameState.SetSelectedCharacter(characterId);
        manager.OnTransportFailure += () => Debug.LogError("[ConnectionSmoke] Transport failure.");
        bool singlePlayer = endpoint == "singleplayer";
        if (singlePlayer) {
            GameState.SetPendingSinglePlayerWorld(100000);
            FindFirstObjectByType<CharacterSelectionMenu>(FindObjectsInactive.Include)?.SelectCharacter(characterId, characterId);
        } else if (checkZoneStart) {
            // Join without entering gameplay to distinguish network spawning from world readiness.
            GameConnectionSettings.ConfigureClient(transport, endpoint);
            NetworkUtility.DisableSceneManagement(manager);
            manager.StartClient();
        } else {
            menu.ConnectToPendingMatchWorld();
        }
        Debug.Log($"[ConnectionSmoke] Starting menu join: {endpoint}, websocket={transport.UseWebSockets}, encrypted={transport.UseEncryption}");
        if (!manager.IsListening) {
            Debug.LogError("[ConnectionSmoke] FAIL: Menu did not start the client.");
            Application.Quit(1);
            yield break;
        }
        float deadline = Time.realtimeSinceStartup + 40f;
        if (checkZoneStart && !singlePlayer) {
            while (Time.realtimeSinceStartup < deadline &&
                (!manager.IsConnectedClient || manager.LocalClient?.PlayerObject == null ||
                 !manager.LocalClient.PlayerObject.IsSpawned || ZoneManager.Instance == null)) yield return null;
            if (!manager.IsConnectedClient || ZoneManager.Instance == null) {
                Debug.LogError("[ZoneSmoke] FAIL: Could not join a server with a safe zone.");
                manager.Shutdown();
                Application.Quit(1);
                yield break;
            }
            float waitingUntil = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < waitingUntil) {
                if (ZoneManager.Instance.TryGetSnapshot(out _)) {
                    Debug.LogError("[ZoneSmoke] FAIL: Match clock started before entering gameplay.");
                    manager.Shutdown();
                    Application.Quit(1);
                    yield break;
                }
                yield return null;
            }
            Debug.Log("[ZoneSmoke] Clock stayed stopped for 5s with a connected, spawned player awaiting world entry.");
            GameState.SetWorld("zone-smoke", 100000);
            FindFirstObjectByType<TerrainController>()?.GenerateWorld(100000);
            ZoneSnapshot initialSnapshot = default;
            float readyDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                !ZoneManager.Instance.TryGetSnapshot(out initialSnapshot)) yield return null;
            if (!ZoneManager.Instance.TryGetSnapshot(out initialSnapshot) ||
                initialSnapshot.StageIndex != 0 || initialSnapshot.Phase != ZonePhase.Waiting || initialSnapshot.SecondsRemaining < 85f) {
                Debug.LogError("[ZoneSmoke] FAIL: World entry did not start a fresh initial zone countdown.");
                manager.Shutdown();
                Application.Quit(1);
                yield break;
            }
            Debug.Log($"[ZoneSmoke] Fresh countdown after world entry: {initialSnapshot.SecondsRemaining:0.00}s until first shrink.");
        }
        while (Time.realtimeSinceStartup < deadline) {
            if (GameState.isPlaying && manager.IsConnectedClient && manager.LocalClient?.PlayerObject != null && manager.LocalClient.PlayerObject.IsSpawned) {
                // Hold both test clients concurrently to verify separate player objects.
                yield return new WaitForSecondsRealtime(holdSeconds);
                bool stable = manager.IsConnectedClient && manager.LocalClient?.PlayerObject != null && manager.LocalClient.PlayerObject.IsSpawned;
                SpriteRenderer sprite = stable ? manager.LocalClient.PlayerObject.GetComponentInChildren<SpriteRenderer>() : null;
                bool visiblePlayer = sprite != null && sprite.enabled && sprite.sprite != null && sprite.color.a > 0f;
                stable &= visiblePlayer && GameState.isPlaying;
                if (checkZoneStart) {
                    bool progressing = ZoneManager.Instance != null && ZoneManager.Instance.TryGetSnapshot(out ZoneSnapshot finalSnapshot) &&
                        (finalSnapshot.StageIndex > 0 || finalSnapshot.Phase != ZonePhase.Waiting ||
                         finalSnapshot.SecondsRemaining < 90f - holdSeconds + 2f);
                    stable &= progressing;
                    if (!progressing) Debug.LogError("[ZoneSmoke] FAIL: Match clock did not advance after world entry.");
                }
                if (stable) {
                    int playerCount = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;
                    Debug.Log($"[ConnectionSmoke] PASS: client={manager.LocalClientId}, player={manager.LocalClient.PlayerObject.NetworkObjectId}, visibleSprite={visiblePlayer}, players={playerCount}, held={holdSeconds}s, enteredGame={GameState.isPlaying}, endpoint={endpoint}");
                } else {
                    Debug.LogError("[ConnectionSmoke] FAIL: Connection did not remain stable after entering the game.");
                }
                manager.Shutdown();
                yield return null;
                Application.Quit(stable ? 0 : 1);
                yield break;
            }
            yield return null;
        }
        Debug.LogError($"[ConnectionSmoke] FAIL: timeout, connected={manager.IsConnectedClient}, reason={manager.DisconnectReason}");
        manager.Shutdown();
        Application.Quit(1);
    }
}
