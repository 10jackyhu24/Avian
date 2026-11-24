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
    private NetworkManager networkManager;

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

        if (networkManagerObject != null) {
            UnityEngine.Object.Destroy(networkManagerObject);
        }

        yield return null;
    }
}
