# Safe-zone clock integration check

Build the server and Windows client together, deploy the server, and use an empty
game server. The opt-in check connects and spawns a player while keeping gameplay
unready for five seconds. It fails if the server starts the zone during that wait.
It then enters the world, requires a fresh first-stage countdown, and checks that
the countdown advances while the player remains connected.

```powershell
.\Builds\WindowsClient\Avian.exe -batchmode -nographics -logFile zone-first.log --avian-smoke-endpoint wss://140.136.151.88:8443/game/30080 --avian-smoke-zone-check --avian-smoke-seconds 8
```

Expected: `[ZoneSmoke] Clock stayed stopped`, then a fresh countdown near 90 seconds,
then `[ConnectionSmoke] PASS` and exit code 0. The server logs a clock reset after
the last ready player disconnects. Repeat the same command after an empty interval,
without restarting the server, to check that the next match gets a fresh countdown.

Run the same readiness/progression check through the real singleplayer menu path:

```powershell
.\Builds\WindowsClient\Avian.exe -batchmode -nographics -logFile zone-singleplayer.log --avian-smoke-endpoint singleplayer --avian-smoke-zone-check --avian-smoke-seconds 8
```

Also run two ordinary connection checks concurrently to verify that both receive
the same zone and that joining/leaving one client does not restart the remaining
player's clock. Use updated clients: they send the world-ready RPC after loading.
