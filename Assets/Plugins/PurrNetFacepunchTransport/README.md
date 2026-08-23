## Based on https://github.com/FirstGearGames/FishyFacepunch

Reliable Steam transport for [PurrNet](https://github.com/PurrNet/PurrNet) using [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks).
This transport layer integrates Steam networking (P2P) via Facepunch.Steamworks into the PurrNet multiplayer library. Designed for Unity, supporting reliable communication and NAT traversal through Steam.

#### Dependencies:
- [UniTask](https://github.com/Cysharp/UniTask)
- [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)
- [PurrNet](https://github.com/PurrNet/PurrNet)
-----
#### How to use:
1. Add the `FacepunchTransport` component to the GameObject with the `NetworkManager`.
2. Assign the transport to the `Transport` field of the `NetworkManager`.
3. Before using the transport, initialize Steam with `SteamClient.Init(appId);`, where `appId` is your Steam App ID (you can use the default 480 for testing).
4. Right before using the transport as a host or client, call its `Initialize(clientAddress, port, serverBindAddress)` method:
   * `clientAddress`: use `SteamClient.SteamId.ToString()` for the host’s Steam ID
   * `port` and `serverBindAddress`: your server port and IP if using a client-server model instead of P2P.
5. Call `NetworkManager.StartHost()` to start as host, or `NetworkManager.StartClient()` to connect as client.
6. Enjoy!
