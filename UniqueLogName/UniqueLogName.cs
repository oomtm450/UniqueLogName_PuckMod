using HarmonyLib;
using oomtm450PuckMod_UniqueLogName.SystemFunc;
using System;
using System.Collections.Generic;
using Unity.Netcode;

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
        #endregion

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                Logging.Log($"Enabling...");

                _harmony.PatchAll();

                Logging.Log($"Enabled.");

                Logging.Log("Subscribing to events.");
                EventManager.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);

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

                Logging.Log("Unsubscribing from events.");
                EventManager.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.");

                _harmonyPatched = false;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}");
                return false;
            }
        }
    }
}
