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
    public IEnumerator ServerDamage_ChangesPlayerHealthNetworkVariable() {
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
