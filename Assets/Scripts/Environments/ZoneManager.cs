using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct ZoneSession : INetworkSerializable, IEquatable<ZoneSession> {
    public int Seed;
    public Vector2 Center;
    public float InitialRadius;
    public float FinalRadius;
    public float Duration;
    public bool IsInitialized => InitialRadius > 0f && Duration > 0f;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref Seed);
        serializer.SerializeValue(ref Center);
        serializer.SerializeValue(ref InitialRadius);
        serializer.SerializeValue(ref FinalRadius);
        serializer.SerializeValue(ref Duration);
    }

    public bool Equals(ZoneSession other) {
        return Seed == other.Seed && Center.Equals(other.Center) &&
               InitialRadius.Equals(other.InitialRadius) && FinalRadius.Equals(other.FinalRadius) &&
               Duration.Equals(other.Duration);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class ZoneManager : NetworkBehaviour {
    [Header("Three-stage timeline")]
    [Min(1f)] [SerializeField] private float totalMatchTime = 480f;
    [Min(1f)] [SerializeField] private float finalRadius = 25f;
    [Header("Outside damage per second: stage 1 / 2 / 3")]
    [SerializeField] private Vector3 outsideDamagePerSecond = new Vector3(2f, 5f, 10f);
    [Min(0.1f)] [SerializeField] private float damageTickInterval = 1f;
    [Header("Zone colors (world and map)")]
    [SerializeField] private Color outsideColor = new Color32(112, 112, 212, 100);
    [SerializeField] private Color edgeColor = new Color32(64, 64, 144, 255);
    [Header("World overlay")]
    [SerializeField] private float overlayHeight = 0.3f;

    private readonly NetworkVariable<ZoneSession> session = new NetworkVariable<ZoneSession>();
    private readonly NetworkVariable<float> elapsedTime = new NetworkVariable<float>();
    private ZoneSession cachedSession;
    private ZoneSchedule schedule;
    private float damageAccumulator;
    private readonly HashSet<ulong> readyPlayers = new HashSet<ulong>();
    private bool localWorldReadySent;
    private GameObject overlayObject;
    private Material overlayMaterial;
    private Material lineMaterial;
    private Mesh overlayMesh;
    private LineRenderer boundary;
    public static ZoneManager Instance { get; private set; }
    public Color OutsideColor => outsideColor;
    public Color EdgeColor => edgeColor;

    public static void EnsureServerZone(NetworkManager manager) {
        if (manager == null || !manager.IsServer || (Instance != null && Instance.IsSpawned)) return;
        GameObject prefab = Resources.Load<GameObject>("SafeZone");
        if (prefab == null) {
            Debug.LogError("[ZoneManager] Resources/SafeZone prefab is missing.");
            return;
        }
        Instantiate(prefab).GetComponent<NetworkObject>().Spawn(true);
    }

    public override void OnNetworkSpawn() {
        Instance = this;
        localWorldReadySent = false;
        if (IsServer) NetworkManager.OnClientDisconnectCallback += HandlePlayerDisconnected;
    }

    public override void OnNetworkDespawn() {
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= HandlePlayerDisconnected;
        readyPlayers.Clear();
        localWorldReadySent = false;
        if (Instance == this) Instance = null;
        DestroyVisuals();
        schedule = null;
    }

    public override void OnDestroy() {
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= HandlePlayerDisconnected;
        if (Instance == this) Instance = null;
        DestroyVisuals();
        base.OnDestroy();
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyWorldReadyServerRpc(ServerRpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null || !client.PlayerObject.IsSpawned) return;
        if (readyPlayers.Add(clientId)) {
            Debug.Log($"[ZoneManager] Player {clientId} entered the world. Ready players={readyPlayers.Count}.", this);
        }
    }

    private void HandlePlayerDisconnected(ulong clientId) {
        readyPlayers.Remove(clientId);
        if (readyPlayers.Count == 0) ResetMatchClock();
    }

    private void ResetMatchClock() {
        if (session.Value.IsInitialized) {
            Debug.Log("[ZoneManager] No ready players remain. Match clock reset; waiting for players to enter the world.", this);
        }
        session.Value = default;
        elapsedTime.Value = 0f;
        damageAccumulator = 0f;
        schedule = null;
    }

    public bool TryGetSnapshot(out ZoneSnapshot snapshot) {
        snapshot = default;
        if (!IsSpawned || !session.Value.IsInitialized) return false;
        if (schedule == null || !cachedSession.Equals(session.Value)) {
            cachedSession = session.Value;
            schedule = new ZoneSchedule(cachedSession.Seed, cachedSession.Center,
                cachedSession.InitialRadius, cachedSession.FinalRadius, cachedSession.Duration);
        }
        snapshot = schedule.Evaluate(elapsedTime.Value);
        return true;
    }

    private void Update() {
        if (!IsSpawned || !GameState.isPlaying) {
            localWorldReadySent = false;
            if (overlayObject != null) overlayObject.SetActive(false);
            if (boundary != null) boundary.gameObject.SetActive(false);
            return;
        }

        // SetWorld is called only after the local player spawns; terrain generation
        // then finishes on the same main thread before this Update can run.
        if (IsClient && !localWorldReadySent && NetworkManager.IsConnectedClient &&
            NetworkManager.LocalClient?.PlayerObject != null && NetworkManager.LocalClient.PlayerObject.IsSpawned) {
            localWorldReadySent = true;
            NotifyWorldReadyServerRpc();
        }

        if (IsServer) {
            // A running dedicated server or a newly connected socket is not a player
            // entering gameplay. Wait for a connected player's world-ready RPC.
            if (readyPlayers.Count == 0) return;
            if (!session.Value.IsInitialized) {
                TerrainController terrain = TerrainController.Instance;
                if (terrain == null || !GameState.hasWorldSeed) return;
                Vector3 center = terrain.GetServerWorldCenter();
                session.Value = new ZoneSession {
                    Seed = GameState.currentSeed,
                    Center = new Vector2(center.x, center.z),
                    InitialRadius = terrain.GetServerWorldSize() * 0.5f,
                    FinalRadius = finalRadius,
                    Duration = Mathf.Max(1f, totalMatchTime)
                };
                elapsedTime.Value = 0f;
                damageAccumulator = 0f;
                Debug.Log($"[ZoneManager] Match clock started after world entry. Seed={GameState.currentSeed}, ready players={readyPlayers.Count}.", this);
            } else {
                elapsedTime.Value = Mathf.Min(session.Value.Duration, elapsedTime.Value + Time.deltaTime);
            }
        }

        if (!TryGetSnapshot(out ZoneSnapshot snapshot)) return;
        if (IsServer) {
            damageAccumulator += Time.deltaTime;
            float interval = Mathf.Max(0.1f, damageTickInterval);
            while (damageAccumulator >= interval) {
                damageAccumulator -= interval;
                ApplyOutsideDamage(snapshot, interval);
            }
        }
        if (IsClient && !Application.isBatchMode) UpdateVisuals(snapshot);
    }

    private void ApplyOutsideDamage(ZoneSnapshot snapshot, float interval) {
        float damagePerSecond = snapshot.StageIndex == 0 ? outsideDamagePerSecond.x
            : snapshot.StageIndex == 1 ? outsideDamagePerSecond.y : outsideDamagePerSecond.z;
        float damage = Mathf.Max(0f, damagePerSecond) * interval;
        if (damage <= 0f) return;
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) {
            if (player.IsSpawned && player.IsAlive && snapshot.IsOutside(player.transform.position)) {
                player.TakeZoneDamageOnServer(damage);
            }
        }
        foreach (EnemyHealthDrop enemy in FindObjectsByType<EnemyHealthDrop>(FindObjectsSortMode.None)) {
            if (enemy.IsAlive && snapshot.IsOutside(enemy.transform.position)) {
                enemy.TakeZoneDamageOnServer(damage);
            }
        }
    }

    private void UpdateVisuals(ZoneSnapshot snapshot) {
        if (overlayObject == null) CreateVisuals();
        if (overlayObject == null) return;
        overlayObject.SetActive(true);
        overlayMaterial.color = outsideColor;
        overlayMaterial.SetVector("_SafeZoneCenter", new Vector4(snapshot.Center.x, 0f, snapshot.Center.y, 0f));
        overlayMaterial.SetFloat("_SafeZoneRadius", snapshot.Radius);
        if (boundary == null) return;
        lineMaterial.color = edgeColor;
        boundary.gameObject.SetActive(true);
        float y = overlayObject.transform.position.y + 0.05f;
        for (int i = 0; i < boundary.positionCount; i++) {
            float angle = i * Mathf.PI * 2f / boundary.positionCount;
            boundary.SetPosition(i, new Vector3(snapshot.Center.x + Mathf.Cos(angle) * snapshot.Radius,
                y, snapshot.Center.y + Mathf.Sin(angle) * snapshot.Radius));
        }
    }

    private void CreateVisuals() {
        Shader shader = Resources.Load<Shader>("SafeZoneOverlay");
        if (shader == null) return;
        float extent = cachedSession.InitialRadius * 2f;
        overlayObject = new GameObject("Outside Safe Zone") { layer = 2 };
        overlayObject.transform.SetParent(transform, false);
        float y = TerrainController.Instance != null ? TerrainController.Instance.GetServerWorldCenter().y : 0f;
        overlayObject.transform.position = new Vector3(cachedSession.Center.x, y + overlayHeight, cachedSession.Center.y);
        overlayMesh = new Mesh { name = "Safe Zone Overlay" };
        overlayMesh.vertices = new[] {
            new Vector3(-extent, 0f, -extent), new Vector3(-extent, 0f, extent),
            new Vector3(extent, 0f, extent), new Vector3(extent, 0f, -extent)
        };
        overlayMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        overlayMesh.RecalculateBounds();
        overlayObject.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
        overlayMaterial = new Material(shader) { color = outsideColor };
        overlayObject.AddComponent<MeshRenderer>().sharedMaterial = overlayMaterial;

        Shader lineShader = Resources.Load<Shader>("AttackPreview");
        if (lineShader == null) return;
        lineMaterial = new Material(lineShader) { color = edgeColor };
        GameObject boundaryObject = new GameObject("Safe Zone Boundary") { layer = 2 };
        boundaryObject.transform.SetParent(transform, false);
        boundary = boundaryObject.AddComponent<LineRenderer>();
        boundary.sharedMaterial = lineMaterial;
        boundary.loop = true;
        boundary.useWorldSpace = true;
        boundary.positionCount = 192;
        boundary.widthMultiplier = 0.4f;
        boundary.startColor = Color.white;
        boundary.endColor = Color.white;
    }

    private void DestroyVisuals() {
        if (overlayObject != null) Destroy(overlayObject);
        if (boundary != null) Destroy(boundary.gameObject);
        if (overlayMesh != null) Destroy(overlayMesh);
        if (overlayMaterial != null) Destroy(overlayMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
