# WSS TLS record regression

This manual integration fixture sends legal unsolicited WebSocket PONG frames totaling 8 KiB
or 16 KiB in one TLS application write after the real Unity client joins a game.
Small local packets do not reliably exercise this case. It catches the TLS record
buffer failure that disconnected the remote player with `AuthenticationFailure`.
Unsolicited PONGs require no replies, so the fixture isolates TLS record processing
without filling the client's control-frame send buffer.
The certificate is mounted read-only inside the fixture container; it is not
copied into the client, repository, or ZIP.

Use the running local Kind game server and Caddy's existing valid certificate.
From the project root, start the temporary fixture (the paths below are for this
host's public IP and existing Caddy volume):

```powershell
docker run -d --rm --name avian-tls-regression --network kind --publish 140.136.151.88:18443:18443 --mount type=volume,source=avian-backend-services_caddy_data,target=/certs,readonly --mount "type=bind,source=$PWD\tests\wss_tls_record_proxy.py,target=/test.py,readonly" --entrypoint python avian-backend-services-avian-matchmaker:latest -u /test.py --cert /certs/caddy/certificates/acme-v02.api.letsencrypt.org-directory/140.136.151.88/140.136.151.88.crt --key /certs/caddy/certificates/acme-v02.api.letsencrypt.org-directory/140.136.151.88/140.136.151.88.key --record-bytes 8192
```

Run the built client against the fixture. The main thread will process network
events slowly at five frames per second, and must retain its player for 30 seconds:

```powershell
.\Builds\WindowsClient\Avian.exe -batchmode -nographics -logFile tls-regression.log --avian-smoke-endpoint wss://140.136.151.88:18443/ --avian-smoke-frame-rate 5
```

The fixture should log `Injected 8192 bytes in one TLS write`; the player should
log `[ConnectionSmoke] PASS` and exit with code 0. The original player joins, then
fails with `AuthenticationFailure` and exits with code 1. Repeat using
`--record-bytes 16384` on the fixture after stopping the previous instance.

Always stop the temporary fixture after the test, releasing test port 18443:

```powershell
docker stop avian-tls-regression
```

Also run two clients against the production Caddy endpoint
`wss://140.136.151.88:8443/game/30080` with different characters. The fixture isolates
large-record handling; it does not replace an external computer's real play test.
