using HarmonyLib;
using oomtm450PuckMod_Template.Configs;
using oomtm450PuckMod_Template.SystemFunc;
using SingularityGroup.HotReload;
using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace oomtm450PuckMod_Template {
    /// <summary>
    /// Class containing the main code for the Template patch.
    /// </summary>
    public class Template : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private const string MOD_VERSION = "0.1.0DEV";

        /// <summary>
        /// Const string, last released version of the mod.
        /// </summary>
        private static readonly string OLD_MOD_VERSION = "0.0.0";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        private const string ASK_SERVER_FOR_STARTUP_DATA = Constants.MOD_NAME + "ASKDATA";
        #endregion

        #region Fields
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        private static ServerConfig _serverConfig = new ServerConfig();

        /// <summary>
        /// Bool, true if the mod has been patched in.
        /// </summary>
        private static bool _harmonyPatched = false;

        /// <summary>
        /// Bool, true if the mod has registered with the named message handler for server/client communication.
        /// </summary>
        private static bool _hasRegisteredWithNamedMessageHandler = false;

        #region Client-sided Fields
        /// <summary>
        /// ClientConfig, config set by the client.
        /// </summary>
        private static ClientConfig _clientConfig = new ClientConfig();

        /// <summary>
        /// DateTime, last time client asked the server for startup data.
        /// </summary>
        private static DateTime _lastDateTimeAskStartupData = DateTime.MinValue;

        /// <summary>
        /// Bool, true if the server has responded and sent the startup data.
        /// </summary>
        private static bool _serverHasResponded = false;

        /// <summary>
        /// Bool, true if the client needs to ask to be kicked because of versionning problems.
        /// </summary>
        private static bool _askForKick = false;

        /// <summary>
        /// Bool, true if the client needs to notify the user that the server is running an out of date version of the mod.
        /// </summary>
        private static bool _addServerModVersionOutOfDateMessage = false;
        #endregion
        #endregion

        /// <summary>
        /// Class that patches the UpdatePlayer event from UIScoreboard.
        /// </summary>
        [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.UpdatePlayer))]
        public class UIScoreboard_UpdatePlayer_Patch {
            [HarmonyPostfix]
            public static void Postfix(Player player) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return;

                    if (!_hasRegisteredWithNamedMessageHandler || !_serverHasResponded) {
                        //Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_SERVER}.", _clientConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(1) < now) {
                            _lastDateTimeAskStartupData = now;
                            NetworkCommunication.SendData(ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT, _clientConfig);
                        }
                    }

                    if (_askForKick) {
                        _askForKick = false;
                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT, _clientConfig);
                    }

                    if (_addServerModVersionOutOfDateMessage) {
                        _addServerModVersionOutOfDateMessage = false;
                        UIChat.Instance.AddChatMessage($"{player.Username.Value} : Server's {Constants.WORKSHOP_MOD_NAME} mod is out of date. Some functionalities might not work properly.");
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIScoreboard_UpdateServer_Patch Postfix().\n{ex}");
                }
            }
        }

        /// <summary>
        /// Class that patches the Event_Client_OnPositionSelectClickPosition event from PlayerPositionManagerController.
        /// </summary>
        [HarmonyPatch(typeof(PlayerPositionManagerController), "Event_Client_OnPositionSelectClickPosition")]
        public class PlayerPositionManagerControllerPatch {
            /// <summary>
            /// Prefix patch function to check if the player is authorized to claim the selected position.
            /// </summary>
            /// <param name="message">Dictionary of string and object, content of the event.</param>
            /// <returns>Bool, true if the user is authorized.</returns>
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                // If this is the server or the config was not sent by server (mod not installed on the server ?), do not use the patch.
                if (ServerFunc.IsDedicatedServer() || !_serverConfig.SentByServer)
                    return true;

                Logging.Log("Event_Client_OnPositionSelectClickPosition", _clientConfig);

                /* From this point on to the end of the function, this is custom code that is left for example. */
                PlayerPosition currentPPosition = (PlayerPosition)message["playerPosition"];

                // Goalie bypass.
                if (PlayerFunc.IsGoalie(currentPPosition, false))
                    return true;

                // Admin bypass.
                if (_serverConfig.AdminBypass && PlayerFunc.IsAdmin(_serverConfig, _clientConfig))
                    return true;

                // Get blue team infos.
                bool hasBlueGoalie = false;
                int numberOfBlueSkaters = 0;
                foreach (PlayerPosition pPosition in PlayerPositionManager.Instance.BluePositions) {
                    if (PlayerFunc.IsAttacker(pPosition))
                        numberOfBlueSkaters++;
                    if (PlayerFunc.IsGoalie(pPosition))
                        hasBlueGoalie = true;
                }

                // Get red team infos.
                bool hasRedGoalie = false;
                int numberOfRedSkaters = 0;
                foreach (PlayerPosition pPosition in PlayerPositionManager.Instance.RedPositions) {
                    if (PlayerFunc.IsAttacker(pPosition))
                        numberOfRedSkaters++;
                    if (PlayerFunc.IsGoalie(pPosition))
                        hasRedGoalie = true;
                }

                int maxNumberOfSkaters = _serverConfig.MaxNumberOfSkaters;
                bool teamBalancing = TeamBalancing(hasBlueGoalie, hasRedGoalie);

                // Get certain informations depending the player's team.
                int numberOfSkaters;
                bool goalieAvailable = true;
                switch (currentPPosition.Team) {
                    case PlayerTeam.Blue:
                        numberOfSkaters = numberOfBlueSkaters;

                        if (teamBalancing) {
                            int newMaxNumberOfSkaters = numberOfRedSkaters + _serverConfig.TeamBalanceOffset + 1;
                            if (newMaxNumberOfSkaters < maxNumberOfSkaters)
                                maxNumberOfSkaters = newMaxNumberOfSkaters;
                        }

                        if (hasBlueGoalie)
                            goalieAvailable = false;

                        break;

                    case PlayerTeam.Red:
                        numberOfSkaters = numberOfRedSkaters;

                        if (teamBalancing) {
                            int newMaxNumberOfSkaters = numberOfBlueSkaters + _serverConfig.TeamBalanceOffset + 1;
                            if (newMaxNumberOfSkaters < maxNumberOfSkaters)
                                maxNumberOfSkaters = newMaxNumberOfSkaters;
                        }

                        if (hasRedGoalie)
                            goalieAvailable = false;

                        break;

                    default:
                        Logging.LogError("No team assigned to the current player position ?");
                        return true;
                }

                /* Logging for client debugging */
                if (teamBalancing)
                    Logging.Log("Team balancing is on.", _clientConfig);

                Logging.Log($"Current team : {nameof(currentPPosition.Team)} with {numberOfSkaters} skaters.", _clientConfig);
                Logging.Log($"Current number of skaters on red team : {numberOfRedSkaters}.", _clientConfig);
                Logging.Log($"Current number of skaters on blue team : {numberOfBlueSkaters}.", _clientConfig);
                /*                              */

                if (numberOfSkaters >= maxNumberOfSkaters) {
                    if (teamBalancing) {
                        if (goalieAvailable)
                            UIChat.Instance.AddChatMessage($"Teams are unbalanced ({maxNumberOfSkaters}). Go goalie or switch teams.");
                        else
                            UIChat.Instance.AddChatMessage($"Teams are unbalanced ({maxNumberOfSkaters}). Switch teams.");
                    }
                    else {
                        if (goalieAvailable)
                            UIChat.Instance.AddChatMessage($"Team is full ({maxNumberOfSkaters}). Only {PlayerFunc.GOALIE_POSITION} position is available.");
                        else
                            UIChat.Instance.AddChatMessage($"Team is full ({maxNumberOfSkaters}). Switch teams.");
                    }
                        
                    return false;
                }

                return true;

                /* End of example code. */
            }
        }

        /// <summary>
        /// Method called when a client has connected (joined a server) on the server-side.
        /// Used to set server-sided stuff after the game has loaded.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnClientConnected(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_OnClientConnected", _serverConfig);

            try {
                if (NetworkManager.Singleton != null && !_hasRegisteredWithNamedMessageHandler) {
                    Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT}.", _serverConfig);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);
                    _hasRegisteredWithNamedMessageHandler = true;
                }

                ulong clientId = (ulong)message["clientId"];
                string clientSteamId = PlayerManager.Instance.GetPlayerByClientId(clientId).SteamId.Value.ToString();
                try {
                    PlayerFunc.Players_ClientId_SteamId.Add(clientId, "");
                }
                catch {
                    PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
                    PlayerFunc.Players_ClientId_SteamId.Add(clientId, "");
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientConnected.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when a client has disconnect (left a server) on the server-side.
        /// Used to unset data linked to the player.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnClientDisconnected(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_OnClientDisconnected", _serverConfig);

            try {
                ulong clientId = (ulong)message["clientId"];
                string clientSteamId;
                try {
                    clientSteamId = PlayerFunc.Players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(PlayerFunc.Players_ClientId_SteamId)}.");
                    return;
                }

                //_sentOutOfDateMessage.Remove(clientId);

                PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientDisconnected.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when a player changes their role.
        /// Used to set a link between steamIds and clientIds.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message) {
            Dictionary<ulong, string> players_ClientId_SteamId_ToChange = new Dictionary<ulong, string>();
            foreach (var kvp in PlayerFunc.Players_ClientId_SteamId) {
                if (string.IsNullOrEmpty(kvp.Value))
                    players_ClientId_SteamId_ToChange.Add(kvp.Key, PlayerManager.Instance.GetPlayerByClientId(kvp.Key).SteamId.Value.ToString());
            }

            foreach (var kvp in players_ClientId_SteamId_ToChange) {
                if (!string.IsNullOrEmpty(kvp.Value)) {
                    PlayerFunc.Players_ClientId_SteamId[kvp.Key] = kvp.Value;
                    Logging.Log($"Added clientId {kvp.Key} linked to Steam Id {kvp.Value}.", _serverConfig);
                }
            }

            Player player = (Player)message["player"];

            string playerSteamId = player.SteamId.Value.ToString();

            if (string.IsNullOrEmpty(playerSteamId))
                return;
        }

        /// <summary>
        /// Method called when the client has started on the client-side.
        /// Used to register to the server messaging (config sync and version check).
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStarted(Dictionary<string, object> message) {
            if (NetworkManager.Singleton == null || ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_Client_OnClientStarted", _clientConfig);

            try {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER, ReceiveData);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStarted.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when the client has stopped on the client-side.
        /// Used to reset the config so that it doesn't carry over between servers.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStopped(Dictionary<string, object> message) {
            Logging.Log("Event_Client_OnClientStopped", _clientConfig);

            try {
                _serverConfig = new ServerConfig();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when a client has "spawned" (joined a server) on the server-side.
        /// Used to send data to the new client that has connected (config and mod version).
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnPlayerSpawned(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;
            
            Logging.Log("Event_OnPlayerSpawned", _serverConfig);

            try {
                Player player = (Player)message["player"];

                NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
                NetworkCommunication.SendData(ServerConfig.CONFIG_DATA_NAME, _serverConfig.ToString(), player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnPlayerSpawned.\n{ex}");
            }
        }

        /// <summary>
        /// Method that manages received data from client-server communications.
        /// </summary>
        /// <param name="clientId">Ulong, Id of the client that sent the data. (0 if the server sent the data)</param>
        /// <param name="reader">FastBufferReader, stream containing the received data.</param>
        public static void ReceiveData(ulong clientId, FastBufferReader reader) {
            try {
                string dataName, dataStr;
                if (clientId == 0) { // If client Id is 0, we received data from the server, so we are client-sided.
                    Logging.Log("ReceiveData", _clientConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _clientConfig);
                }
                else {
                    Logging.Log("ReceiveData", _serverConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _serverConfig);
                }

                switch (dataName) {
                    case Constants.MOD_NAME + "_" + nameof(MOD_VERSION): // CLIENT-SIDE : Mod version check, kick if client and server versions are not the same.
                        _serverHasResponded = true;
                        if (MOD_VERSION == dataStr) // TODO : Maybe add a chat message and a 3-5 sec wait.
                            break;
                        else if (OLD_MOD_VERSION == dataStr) {
                            _addServerModVersionOutOfDateMessage = true;
                            break;
                        }

                        _askForKick = true;
                        break;

                    case ServerConfig.CONFIG_DATA_NAME: // CLIENT-SIDE : Set the server config on the client to use later for the Template logic, since the logic happens on the client.
                        _serverConfig = ServerConfig.SetConfig(dataStr);
                        break;

                    case Constants.MOD_NAME + "_kick": // SERVER-SIDE : Kick the client that asked to be kicked.
                        if (dataStr != "1")
                            break;

                        Logging.Log($"Kicking client {clientId}.", _serverConfig);
                        NetworkManager.Singleton.DisconnectClient(clientId,
                            $"Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");

                        /*if (!_sentOutOfDateMessage.TryGetValue(clientId, out DateTime lastCheckTime)) {
                            lastCheckTime = DateTime.MinValue;
                            _sentOutOfDateMessage.Add(clientId, lastCheckTime);
                        }

                        DateTime utcNow = DateTime.UtcNow;
                        if (lastCheckTime + TimeSpan.FromSeconds(900) < utcNow) {
                            if (string.IsNullOrEmpty(PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value.ToString()))
                                break;
                            UIChat.Instance.Server_SendSystemChatMessage($"{PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value} : {Constants.WORKSHOP_MOD_NAME} Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");
                            _sentOutOfDateMessage[clientId] = utcNow;
                        }*/
                        break;

                    case ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1")
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER, _serverConfig);
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}");
            }
        }

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                Logging.Log($"Enabling...", _serverConfig, true);

                _harmony.PatchAll();

                Logging.Log($"Enabled.", _serverConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null) {
                        Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT}.", _serverConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;
                    }

                    Logging.Log("Setting server sided config.", _serverConfig, true);
                    _serverConfig = ServerConfig.ReadConfig(ServerManager.Instance.AdminSteamIds);
                }
                else {
                    Logging.Log("Setting client sided config.", _serverConfig, true);
                    _clientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", _serverConfig, true);
                if (ServerFunc.IsDedicatedServer()) {
                    // Server-side events.
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                }
                else {
                    // Client-side events.
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    EventManager.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
                }

                _harmonyPatched = true;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to enable.\n{ex}");
                return false;
            }
        }

        /// <summary>
        /// Method that launches when the mod is being disabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully disabled.</returns>
        public bool OnDisable() {
            try {
                if (!_harmonyPatched)
                    return true;

                Logging.Log($"Disabling...", _serverConfig, true);

                Logging.Log("Unsubscribing from events.", _serverConfig, true);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _serverHasResponded = false;

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", _serverConfig, true);

                _harmonyPatched = false;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}");
                return false;
            }
        }

        /// <summary>
        /// Function that returns true if team balancing is activated.
        /// </summary>
        /// <param name="hasBlueGoalie">Bool, true if blue team has a goalie.</param>
        /// <param name="hasRedGoalie">Bool, true if red team has a goalie.</param>
        /// <returns>Bool, true if team balancing is activated.</returns>
        private static bool TeamBalancing(bool hasBlueGoalie, bool hasRedGoalie) {
            if (_serverConfig.TeamBalancing)
                return true;

            if (!_serverConfig.TeamBalancingGoalie)
                return false;

            if (hasBlueGoalie && hasRedGoalie)
                return false;

            if (hasBlueGoalie || hasRedGoalie)
                return true;

            return false;
        }
    }
}
