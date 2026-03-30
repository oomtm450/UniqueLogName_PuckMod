using HarmonyLib;
using oomtm450PuckMod_UniqueLogName.SystemFunc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace oomtm450PuckMod_UniqueLogName {
    /// <summary>
    /// Class containing the main code for the UniqueLogName patch.
    /// </summary>
    public class UniqueLogName : IPuckMod {
        #region Fields
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// Bool, true if the mod has been patched in.
        /// </summary>
        private static bool _harmonyPatched = false;

        /// <summary>
        /// String, path of the chat log.
        /// </summary>
        private static string _chatLogPath = "";
        #endregion

        /// <summary>
        /// Class that patches the Client_SendChatMessageRpc event from ChatManager.
        /// </summary>
        [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.Client_SendChatMessageRpc))]
        public class ChatManager_Client_SendChatMessageRpc_Patch {
            [HarmonyPrefix]
            private static bool Prefix(string content, bool isQuickChat, bool isTeamChat, RpcParams rpcParams) {
                Player player = PlayerManager.Instance.GetPlayerByClientId(rpcParams.Receive.SenderClientId);
                if (player == null || !player)
                    return false;

                LogChat(player, content);

                return true;
            }
        }

        /// <summary>
        /// Method that patches the logs.
        /// </summary>
        private static void Patch(string logName, string chatLogName = "") {
            try {
                if (string.IsNullOrEmpty(chatLogName))
                    _chatLogPath = "";
                else
                    _chatLogPath = Path.Combine(LogManager.Instance.LogsPath, chatLogName);

                StreamWriter sw = new StreamWriter(Path.Combine(GetPrivateField<string>(typeof(LogManager), null, "logDirectoryPath"), logName), false, Encoding.UTF8) {
                    AutoFlush = true,
                };

                FieldInfo streamWriterFieldInfo = typeof(LogManager).GetField("streamWriter", BindingFlags.NonPublic | BindingFlags.Static);
                StreamWriter oldSw = (StreamWriter)streamWriterFieldInfo.GetValue(null);

                if (oldSw != null) {
                    oldSw.Close();
                    oldSw = null;
                }

                streamWriterFieldInfo.SetValue(null, sw);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Patch)}().\n{ex}");
                throw ex;
            }
        }

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                Logging.Log($"Enabling...");

                if (IsDedicatedServer())
                    _harmony.PatchAll();

                Patch(string.Format("Puck_{0:yyyy-MM-dd_HH-mm-ss}.log", DateTime.Now), string.Format("PuckChat_{0:yyyy-MM-dd_HH-mm-ss}.log", DateTime.Now));

                Logging.Log($"Enabled.");

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

                Logging.Log($"Disabling...");

                if (IsDedicatedServer())
                    _harmony.UnpatchSelf();

                Patch("Puck.log");

                Logging.Log($"Disabled.");

                _harmonyPatched = false;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}");
                return false;
            }
        }

        private static void LogChat(Player player, string message) {
            File.AppendAllText(_chatLogPath, $"{DateTime.UtcNow} #{player.Number.Value} {player.Username.Value} ({player.OwnerClientId}) [{player.SteamId.Value}] sent message \"{message}\"\n");
        }

        public static T GetPrivateField<T>(Type typeContainingField, object instanceOfType, string fieldName) {
            if (instanceOfType == null)
                return (T)typeContainingField.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(instanceOfType);
            else
                return (T)typeContainingField.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instanceOfType);
        }

        /// <summary>
        /// Function that returns true if the instance is a dedicated server.
        /// </summary>
        /// <returns>Bool, true if this is a dedicated server.</returns>
        public static bool IsDedicatedServer() {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }
    }
}
