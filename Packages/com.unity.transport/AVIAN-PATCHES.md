# Avian changes to Unity Transport 2.7.2

This is an embedded copy of the Unity Registry package `com.unity.transport` 2.7.2.
Its original license is in `LICENSE.md`. Unity resolves this embedded package before
the registry dependency, so the fix is included when the project moves to another
computer. Do not patch `Library/PackageCache` instead; Unity can regenerate it.

## TLS record receive buffer

`Runtime/Layers/TLSLayer.cs`: `k_DecryptBufferSize` now allows two standard 16 KiB
TLS application records plus two MTU-sized packet tails, rather than just two MTUs.
The buffer is allocated per TLS session and freed on disconnect or driver disposal.
It is stored via a native pointer so Mono does not reject the now larger
`TLSConnectionData`/`ConnectionDataMap` value types when constructing the driver.

A TLS terminator can combine small WebSocket frames into one TLS record larger
than the game's packet MTU. The original buffer can fill before all encrypted
input has been consumed. Remaining encrypted bytes are then discarded when the
packet is replaced with its decrypted contents. The next record fails and the
client disconnects with `AuthenticationFailure`, even after successful joining
and player spawning. Increasing the packet queue does not fix this buffer.

The regression fixture `tests/wss_tls_record_proxy.py` forwards a real game
connection using a valid TLS certificate, then sends legal WebSocket PONG frames
in an 8 KiB application record. The unmodified Windows player disconnects with
`AuthenticationFailure` after entering the world. Rebuild the player with this
patch and repeat the same fixture, including the maximum 16 KiB record case.

No TLS trust checks, certificate validation, or encryption settings are disabled.
When upgrading Transport, verify the same regression before removing this copy.
