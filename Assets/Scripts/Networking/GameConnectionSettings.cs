using System;
using Unity.Netcode.Transports.UTP;

public static class GameConnectionSettings {
    public static void ConfigureClient(UnityTransport transport, string endpoint) {
        string address = endpoint?.Trim();
        if (string.IsNullOrEmpty(address)) {
            throw new ArgumentException("Game endpoint is empty.");
        }

        if (!address.Contains("://")) {
            address = "udp://" + address;
        }
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) ||
            string.IsNullOrEmpty(uri.Host) || uri.Port < 1 || uri.Port > ushort.MaxValue ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment)) {
            throw new ArgumentException($"Invalid game endpoint: {endpoint}");
        }

        bool websocket = uri.Scheme == "ws" || uri.Scheme == "wss";
        if (!websocket && uri.Scheme != "udp") {
            throw new ArgumentException("Game endpoints must use wss://, ws://, or host:port for UDP.");
        }

        transport.UseWebSockets = websocket;
        // Multiplayer heartbeats and stream reads must continue when the game loses focus.
        UnityEngine.Application.runInBackground = true;
        if (websocket) {
            transport.MaxPacketQueueSize = Math.Max(transport.MaxPacketQueueSize, 1024);
        }
        transport.UseEncryption = uri.Scheme == "wss";
        transport.SetConnectionData(uri.DnsSafeHost, (ushort)uri.Port);
        transport.ConnectionData.WebSocketPath = websocket ? uri.AbsolutePath : "/";
        if (transport.UseEncryption) {
            // Validate the public certificate against the platform's trusted CAs.
            transport.SetClientSecrets(uri.DnsSafeHost);
        }
    }

    public static void ConfigureServer(UnityTransport transport, string address, ushort port, string mode) {
        if (mode != "websocket" && mode != "udp") {
            throw new ArgumentException("GAME_TRANSPORT must be websocket or udp.");
        }
        transport.UseWebSockets = mode == "websocket";
        if (transport.UseWebSockets) {
            transport.MaxPacketQueueSize = Math.Max(transport.MaxPacketQueueSize, 1024);
        }
        transport.UseEncryption = false; // Caddy terminates public TLS on the private proxy connection.
        transport.SetConnectionData(address, port, address);
        transport.ConnectionData.WebSocketPath = "/";
    }

    public static void ConfigureSinglePlayer(UnityTransport transport) {
        ConfigureServer(transport, "127.0.0.1", 7778, "udp");
    }
}
