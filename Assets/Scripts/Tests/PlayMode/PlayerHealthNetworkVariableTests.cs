using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerHealthNetworkVariableTests {
    private GameObject networkManagerObject;
    private GameObject playerObject;
    private GameObject cureObject;
    private NetworkManager networkManager;

    [Test]
    public void CureAsset_IsIncludedInWorldLootWithTenPointHealingAndSprite() {
        ScriptableObject cure = Resources.Load<ScriptableObject>("Items/Cure");
        Assert.That(cure, Is.Not.Null);
        Assert.That((bool)cure.GetType().GetProperty("IsHealthPickup").GetValue(cure), Is.True);
        Assert.That((float)cure.GetType().GetField("healAmount").GetValue(cure), Is.EqualTo(10f));
        Assert.That(cure.GetType().GetField("icon").GetValue(cure), Is.Not.Null);
        ScriptableObject settings = Resources.Load<ScriptableObject>("WorldItemSpawnSettings");
        Assert.That(settings, Is.Not.Null);
        CollectionAssert.Contains((Array)settings.GetType().GetField("items").GetValue(settings), cure);
    }

    [UnityTest]
    public IEnumerator ServerHealing_RestoresTenCapsAtMaximumAndCannotRevive() {
        yield return CreateServerPlayer();
        Component player = playerObject.GetComponent(Type.GetType("PlayerController, Assembly-CSharp"));
        MethodInfo damage = player.GetType().GetMethod("TakeDamageFromEnemy");
        MethodInfo heal = player.GetType().GetMethod("TryHealOnServer");
        PropertyInfo health = player.GetType().GetProperty("CurrentHealth");

        Assert.That((bool)heal.Invoke(player, new object[] { 10f }), Is.False, "Full health should not consume healing.");
        damage.Invoke(player, new object[] { 15f });
        Assert.That((bool)heal.Invoke(player, new object[] { 10f }), Is.True);
        Assert.That((float)health.GetValue(player), Is.EqualTo(95f));
        Assert.That((bool)heal.Invoke(player, new object[] { -10f }), Is.False);
        Assert.That((float)health.GetValue(player), Is.EqualTo(95f));
        Assert.That((bool)heal.Invoke(player, new object[] { 10f }), Is.True);
        Assert.That((float)health.GetValue(player), Is.EqualTo(100f), "Healing must not exceed max health.");
        damage.Invoke(player, new object[] { 100f });
        Assert.That((bool)heal.Invoke(player, new object[] { 10f }), Is.False);
        Assert.That((float)health.GetValue(player), Is.EqualTo(0f), "CURE must not resurrect a dead player.");
    }

    [UnityTest]
    public IEnumerator CurePickup_HealsOnContactOnceAndRemovesItsWorldRecord() {
        yield return CreateServerPlayer();
        Component player = playerObject.GetComponent(Type.GetType("PlayerController, Assembly-CSharp"));
        PropertyInfo health = player.GetType().GetProperty("CurrentHealth");
        Component cure = SpawnCure(Vector3.zero);
        MethodInfo fixedUpdate = cure.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        NetworkObject networkObject = cureObject.GetComponent<NetworkObject>();

        Physics.SyncTransforms();
        fixedUpdate.Invoke(cure, null);
        Assert.That(networkObject.IsSpawned, Is.True, "A full-health player should leave CURE on the ground.");

        player.GetType().GetMethod("TakeDamageFromEnemy").Invoke(player, new object[] { 25f });
        playerObject.transform.position = Vector3.right * 20f;
        Physics.SyncTransforms();
        fixedUpdate.Invoke(cure, null);
        Assert.That((float)health.GetValue(player), Is.EqualTo(75f), "Players outside contact range must not collect CURE.");
        Assert.That(networkObject.IsSpawned, Is.True);

        playerObject.transform.position = Vector3.zero;
        Physics.SyncTransforms();
        fixedUpdate.Invoke(cure, null);
        Assert.That((float)health.GetValue(player), Is.EqualTo(85f));
        Assert.That(networkObject.IsSpawned, Is.False);
        Type registry = Type.GetType("DroppedItemRegistry, Assembly-CSharp");
        Assert.That((int)registry.GetProperty("Count").GetValue(null), Is.EqualTo(0), "A consumed pickup must not stream back in.");
        fixedUpdate.Invoke(cure, null);
        Assert.That((float)health.GetValue(player), Is.EqualTo(85f), "A second overlap must not heal again.");
    }

    [UnityTest]
    public IEnumerator CurePickup_DoesNotCollectForDeadPlayers() {
        yield return CreateServerPlayer();
        Component player = playerObject.GetComponent(Type.GetType("PlayerController, Assembly-CSharp"));
        player.GetType().GetMethod("TakeDamageFromEnemy").Invoke(player, new object[] { 100f });
        Component cure = SpawnCure(Vector3.zero);
        Physics.SyncTransforms();
        cure.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cure, null);
        Assert.That(cureObject.GetComponent<NetworkObject>().IsSpawned, Is.True);
        Assert.That((float)player.GetType().GetProperty("CurrentHealth").GetValue(player), Is.EqualTo(0f));
    }

    private Component SpawnCure(Vector3 position) {
        Type stateType = Type.GetType("InventoryItemState, Assembly-CSharp");
        object state = Activator.CreateInstance(stateType);
        stateType.GetField("InstanceId").SetValue(state, 0xC0FFEE1UL);
        stateType.GetField("Count").SetValue(state, 1);
        FieldInfo itemId = stateType.GetField("ItemId");
        itemId.SetValue(state, Activator.CreateInstance(itemId.FieldType, new object[] { "cure" }));
        Type droppedItemType = Type.GetType("NetworkDroppedItem, Assembly-CSharp");
        Assert.That((bool)droppedItemType.GetMethod("TrySpawnOnServer").Invoke(null, new object[] { state, position }), Is.True);
        cureObject = GameObject.Find("NetworkDroppedItem(Clone)");
        Assert.That(cureObject, Is.Not.Null);
        return cureObject.GetComponent(droppedItemType);
    }

    [UnityTest]
    public IEnumerator ServerSprint_ExhaustsAfterThreeSeconds_RecoversOnlyAfterRelease() {
        yield return CreateServerPlayer();
        Component player = playerObject.GetComponent(Type.GetType("PlayerController, Assembly-CSharp"));
        Type playerType = player.GetType();
        FieldInfo runRequested = playerType.GetField("runRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo updateStamina = playerType.GetMethod("UpdateStaminaOnServer", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo stopMovement = playerType.GetMethod("StopMovementOnServer", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo stamina = playerType.GetProperty("CurrentStamina");
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(100f));

        runRequested.SetValue(player, true);
        for (int frame = 0; frame < 180; frame++) {
            updateStamina.Invoke(player, new object[] { true, 1f / 60f });
        }
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(0f).Within(0.001f), "Three seconds of sprinting should exhaust stamina.");
        float sprintFraction = (float)updateStamina.Invoke(player, new object[] { true, 1f });
        Assert.That(sprintFraction, Is.EqualTo(0f).Within(0.001f), "An exhausted player should walk even while holding sprint.");

        stopMovement.Invoke(player, null);
        updateStamina.Invoke(player, new object[] { false, 8f });
        Assert.That((bool)runRequested.GetValue(player), Is.True, "Stopping for an attack must not act as releasing sprint.");
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(0f).Within(0.001f), "Holding sprint while stationary must not recover stamina.");

        runRequested.SetValue(player, false);
        updateStamina.Invoke(player, new object[] { true, 4f });
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(50f).Within(0.001f), "Walking with sprint released should recover half the bar in four seconds.");
        updateStamina.Invoke(player, new object[] { false, 4f });
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(100f).Within(0.001f));
        updateStamina.Invoke(player, new object[] { false, 8f });
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(100f), "Recovery must not overfill stamina.");

        runRequested.SetValue(player, true);
        sprintFraction = (float)updateStamina.Invoke(player, new object[] { true, 4f });
        Assert.That(sprintFraction, Is.EqualTo(0.75f).Within(0.001f), "A long frame must only fund three seconds of sprinting.");
        Assert.That((float)stamina.GetValue(player), Is.EqualTo(0f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator ServerDamage_ChangesPlayerHealthNetworkVariable() {
        yield return CreateServerPlayer();
        Type playerType = Type.GetType("PlayerController, Assembly-CSharp");
        Component player = playerObject.GetComponent(playerType);

        FieldInfo healthField = playerType.GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(healthField, Is.Not.Null);
        NetworkVariable<float> health = (NetworkVariable<float>)healthField.GetValue(player);
        Assert.That(health.Value, Is.EqualTo(100f), "OnNetworkSpawn should initialize health on the server.");

        MethodInfo takeDamage = playerType.GetMethod("TakeDamageFromEnemy", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo currentHealth = playerType.GetProperty("CurrentHealth", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(takeDamage, Is.Not.Null);
        Assert.That(currentHealth, Is.Not.Null);
        float oldValue = -1f;
        float newValue = -1f;
        health.OnValueChanged += (previous, current) => {
            oldValue = previous;
            newValue = current;
        };

        takeDamage.Invoke(player, new object[] { 25f });
        Assert.That(health.Value, Is.EqualTo(75f));
        Assert.That((float)currentHealth.GetValue(player), Is.EqualTo(75f));
        Assert.That(oldValue, Is.EqualTo(100f));
        Assert.That(newValue, Is.EqualTo(75f));

        takeDamage.Invoke(player, new object[] { 100f });
        Assert.That(health.Value, Is.EqualTo(0f), "Health should not drop below zero.");

        takeDamage.Invoke(player, new object[] { 10f });
        Assert.That(health.Value, Is.EqualTo(0f), "Damage after death should not change health.");
    }

    private IEnumerator CreateServerPlayer() {
        Assert.That(NetworkManager.Singleton, Is.Null, "This test needs an empty PlayMode scene without a running NetworkManager.");

        networkManagerObject = new GameObject("Test NetworkManager");
        UnityTransport transport = networkManagerObject.AddComponent<UnityTransport>();
        networkManager = networkManagerObject.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new NetworkConfig {
            NetworkTransport = transport,
            EnableSceneManagement = false
        };
        transport.SetConnectionData(true, "127.0.0.1", 0, "127.0.0.1");

        Assert.That(networkManager.StartServer(), Is.True);
        yield return null;

        // PlayerController is currently in Assembly-CSharp, which a test asmdef cannot reference.
        Type playerType = Type.GetType("PlayerController, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null);

        playerObject = new GameObject("Test Player");
        NetworkObject networkObject = playerObject.AddComponent<NetworkObject>();
        playerObject.AddComponent<CharacterController>();
        Component player = playerObject.AddComponent(playerType);
        networkObject.Spawn();

        Assert.That(networkObject.IsSpawned, Is.True);
        Assert.That(((NetworkBehaviour)player).IsServer, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown() {
        if (networkManager != null && networkManager.IsListening) {
            networkManager.Shutdown();
        }

        if (playerObject != null) {
            UnityEngine.Object.Destroy(playerObject);
        }

        if (cureObject != null) {
            UnityEngine.Object.Destroy(cureObject);
        }
        Type.GetType("DroppedItemRegistry, Assembly-CSharp").GetMethod("Reset").Invoke(null, null);

        if (networkManagerObject != null) {
            UnityEngine.Object.Destroy(networkManagerObject);
        }

        yield return null;
    }
}
