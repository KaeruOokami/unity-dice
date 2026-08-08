namespace DiceGame.Session
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DiceGame.Gameplay;
    using Photon.Client;
    using Photon.Deterministic;
    using Photon.Realtime;
    using Quantum;
    using UnityEngine;

    /// <summary>
    /// Starts / shuts down Quantum local and online sessions (Photon Realtime + QuantumPlugin).
    /// AppId comes from <see cref="PhotonServerSettings"/> only.
    /// </summary>
    public sealed class QuantumSessionLauncher
    {
        public RealtimeClient Client { get; private set; }
        public QuantumRunner Runner { get; private set; }
        public bool IsRunning => Runner != null;
        public bool IsConnectedToRoom =>
            Client != null && Client.IsConnected && Client.InRoom;
        public int ConnectedPlayerCount =>
            Client?.CurrentRoom?.PlayerCount ?? 0;
        public int PendingMatchSeed { get; private set; }

        CancellationTokenSource cancellation;
        ConnectionServiceScope connectionService;
        IDisposable pluginDisconnectSubscription;

        public async Task StartLocalAsync(
            RuntimeConfig runtimeConfig,
            string clientId,
            string playerNickname,
            CancellationToken externalToken = default)
        {
            await ShutdownAsync();
            BeginCancellation(externalToken);

            await QuantumSceneFlow.EnsureLoadedAsync();
            DisableLocalDebugRunner();
            ApplyMapAndSimulation(runtimeConfig);

            var args = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = string.IsNullOrEmpty(clientId) ? Guid.NewGuid().ToString() : clientId,
                RuntimeConfig = runtimeConfig,
                SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = SessionConstants.MaxPlayers,
                GameMode = DeterministicGameMode.Local,
                Communicator = null,
                CancellationToken = cancellation.Token,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            Runner = (QuantumRunner)await SessionRunner.StartAsync(args);
            AddLocalPlayer(playerNickname);
            ClearCancellation();
        }

        /// <summary>
        /// Joins or creates a Photon room with QuantumPlugin. Does not start the simulation yet.
        /// </summary>
        public async Task ConnectOnlineRoomAsync(
            string roomName,
            int matchSeedIfHost,
            bool isHost,
            string clientId,
            CancellationToken externalToken = default)
        {
            await ShutdownAsync();
            BeginCancellation(externalToken);

            if (!PhotonServerSettings.TryGetGlobal(out var serverSettings)
                || serverSettings == null)
            {
                throw new InvalidOperationException(
                    "PhotonServerSettings is missing. Open Quantum Hub and configure AppId.");
            }

            if (string.IsNullOrEmpty(serverSettings.AppSettings.AppIdQuantum))
            {
                throw new InvalidOperationException(
                    "No Quantum AppId set. Open Quantum Hub (Ctrl+H) and follow the AppId setup.");
            }

            PendingMatchSeed = isHost ? matchSeedIfHost : 0;

            PhotonHashtable customProperties = null;
            if (isHost)
            {
                customProperties = new PhotonHashtable
                {
                    { SessionConstants.QuantumRoomSeedProperty, matchSeedIfHost },
                    { SessionConstants.QuantumRoomMatchStartProperty, 0 },
                };
            }

            var matchmaking = new MatchmakingArguments
            {
                PhotonSettings = new AppSettings(serverSettings.AppSettings),
                EmptyRoomTtlInSeconds = serverSettings.EmptyRoomTtlInSeconds,
                EnableCrc = serverSettings.EnableCrc,
                PlayerTtlInSeconds = serverSettings.PlayerTtlInSeconds,
                MaxPlayers = SessionConstants.MaxPlayers,
                RoomName = roomName,
                CanOnlyJoin = !isHost,
                PluginName = SessionConstants.QuantumPluginName,
                AuthValues = new AuthenticationValues(
                    string.IsNullOrEmpty(clientId) ? Guid.NewGuid().ToString() : clientId),
                CustomProperties = customProperties,
                AsyncConfig = new AsyncConfig
                {
                    TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
                    CancellationToken = cancellation.Token,
                },
            };

            Client = await MatchmakingExtensions.ConnectToRoomAsync(matchmaking);
            connectionService = new ConnectionServiceScope(Client);

            pluginDisconnectSubscription = QuantumCallback.SubscribeManual<CallbackPluginDisconnect>(
                c => Debug.LogError($"Quantum plugin disconnect: {c.Reason}"));

            if (!isHost)
            {
                PendingMatchSeed = await WaitForRoomSeedAsync(cancellation.Token);
            }

            ClearCancellation();
        }

        /// <summary>
        /// Host signals clients that the Quantum match may start, then starts local session.
        /// </summary>
        public async Task StartOnlineMatchAsHostAsync(
            RuntimeConfig runtimeConfig,
            string playerNickname,
            CancellationToken externalToken = default)
        {
            if (!IsConnectedToRoom || Client == null)
            {
                throw new InvalidOperationException("Host is not connected to a Quantum room.");
            }

            if (ConnectedPlayerCount < SessionConstants.MaxPlayers)
            {
                throw new InvalidOperationException(
                    $"Waiting for players ({ConnectedPlayerCount}/{SessionConstants.MaxPlayers}).");
            }

            BeginCancellation(externalToken);

            var seed = PendingMatchSeed != 0
                ? PendingMatchSeed
                : MatchRandom.CreateMatchSeed();
            PendingMatchSeed = seed;
            runtimeConfig.Seed = seed;

            var props = new PhotonHashtable
            {
                { SessionConstants.QuantumRoomSeedProperty, seed },
                { SessionConstants.QuantumRoomMatchStartProperty, 1 },
            };
            Client.CurrentRoom.SetCustomProperties(props);

            await StartConnectedSessionAsync(runtimeConfig, playerNickname);
            ClearCancellation();
        }

        /// <summary>
        /// Client starts after host sets the match-start room property.
        /// </summary>
        public async Task StartOnlineMatchAsClientAsync(
            RuntimeConfig runtimeConfig,
            string playerNickname,
            CancellationToken externalToken = default)
        {
            if (!IsConnectedToRoom)
            {
                throw new InvalidOperationException("Client is not connected to a Quantum room.");
            }

            BeginCancellation(externalToken);

            if (!TryReadMatchStart(out var seed) || seed == 0)
            {
                seed = await WaitForMatchStartAsync(cancellation.Token);
            }

            PendingMatchSeed = seed;
            runtimeConfig.Seed = seed;
            await StartConnectedSessionAsync(runtimeConfig, playerNickname);
            ClearCancellation();
        }

        async Task<int> WaitForRoomSeedAsync(CancellationToken token)
        {
            var key = SessionConstants.QuantumRoomSeedProperty;
            var elapsed = 0f;
            while (elapsed < SessionConstants.QuantumRoomSeedWaitSeconds)
            {
                token.ThrowIfCancellationRequested();
                if (TryReadIntProperty(
                        Client?.CurrentRoom?.CustomProperties,
                        key,
                        out var seed)
                    && seed != 0)
                {
                    return seed;
                }

                await Task.Yield();
                elapsed += Time.unscaledDeltaTime;
            }

            throw new TimeoutException(
                "Timed out waiting for Quantum room seed from host.");
        }

        public bool TryReadMatchStart(out int seed)
        {
            seed = 0;
            var props = Client?.CurrentRoom?.CustomProperties;
            if (props == null)
            {
                return false;
            }

            if (!TryReadIntProperty(
                    props,
                    SessionConstants.QuantumRoomMatchStartProperty,
                    out var started)
                || started == 0)
            {
                return false;
            }

            return TryReadIntProperty(
                       props,
                       SessionConstants.QuantumRoomSeedProperty,
                       out seed)
                   && seed != 0;
        }

        static bool TryReadIntProperty(PhotonHashtable props, string key, out int value)
        {
            value = 0;
            if (props == null || !props.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case short s:
                    value = s;
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    value = (int)l;
                    return true;
                default:
                    return int.TryParse(raw.ToString(), out value);
            }
        }

        public async Task ShutdownAsync()
        {
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
            }

            pluginDisconnectSubscription?.Dispose();
            pluginDisconnectSubscription = null;

            connectionService?.Dispose();
            connectionService = null;

            if (Runner != null)
            {
                try
                {
                    await Runner.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"QuantumSessionLauncher: runner shutdown: {ex.Message}");
                }

                Runner = null;
            }

            if (Client != null)
            {
                try
                {
                    await Client.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"QuantumSessionLauncher: client disconnect: {ex.Message}");
                }

                Client = null;
            }

            PendingMatchSeed = 0;
            await QuantumSceneFlow.UnloadAsync();
        }

        async Task StartConnectedSessionAsync(RuntimeConfig runtimeConfig, string playerNickname)
        {
            await QuantumSceneFlow.EnsureLoadedAsync();
            DisableLocalDebugRunner();
            ApplyMapAndSimulation(runtimeConfig);

            // QuantumNetworkCommunicator services the client; stop the lobby service loop.
            connectionService?.Dispose();
            connectionService = null;

            var args = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = Client.UserId,
                RuntimeConfig = runtimeConfig,
                SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = SessionConstants.MaxPlayers,
                GameMode = DeterministicGameMode.Multiplayer,
                Communicator = new QuantumNetworkCommunicator(Client),
                CancellationToken = cancellation.Token,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            using (new ConnectionServiceScope(Client))
            {
                Runner = (QuantumRunner)await SessionRunner.StartAsync(args);
            }

            AddLocalPlayer(playerNickname);
        }

        void AddLocalPlayer(string nickname)
        {
            if (Runner?.Game == null)
            {
                return;
            }

            var player = new RuntimePlayer
            {
                PlayerNickname = string.IsNullOrEmpty(nickname) ? "Player" : nickname,
            };
            Runner.Game.AddPlayer(0, player);
        }

        static void ApplyMapAndSimulation(RuntimeConfig runtimeConfig)
        {
            var mapData = UnityEngine.Object.FindAnyObjectByType<QuantumMapData>();
            if (mapData != null)
            {
                runtimeConfig.Map = mapData.AssetRef;
            }

            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaults))
            {
                runtimeConfig.SimulationConfig = defaults.SimulationConfig;
                if (runtimeConfig.SystemsConfig.Id.IsValid == false)
                {
                    runtimeConfig.SystemsConfig = defaults.SystemsConfig;
                }
            }
        }

        static void DisableLocalDebugRunner()
        {
            var debug = UnityEngine.Object.FindAnyObjectByType<QuantumRunnerLocalDebug>();
            if (debug != null)
            {
                debug.enabled = false;
            }
        }

        async Task<int> WaitForMatchStartAsync(CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < SessionConstants.QuantumMatchStartWaitSeconds)
            {
                token.ThrowIfCancellationRequested();
                if (TryReadMatchStart(out var seed))
                {
                    return seed;
                }

                await Task.Yield();
                elapsed += Time.unscaledDeltaTime;
            }

            throw new TimeoutException(
                "Timed out waiting for Quantum match start from host.");
        }

        void BeginCancellation(CancellationToken externalToken)
        {
            cancellation?.Dispose();
            cancellation = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
                : new CancellationTokenSource();
        }

        void ClearCancellation()
        {
            cancellation?.Dispose();
            cancellation = null;
        }
    }
}
