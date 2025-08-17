using HarmonyLib;
using oomtm450PuckMod_UniqueLogName.SystemFunc;
using System;
using System.IO;
using System.Reflection;
using System.Text;

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
        /// Method that patches the logs.
        /// </summary>
        private static void Patch(string logName) {
            try {
                string path = Path.Combine(LogManager.Instance.LogsPath, logName);

                StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8) {
                    AutoFlush = true,
                };

                FieldInfo streamWriterFieldInfo = typeof(LogManager).GetField("streamWriter", BindingFlags.NonPublic | BindingFlags.Instance);
                StreamWriter oldSw = (StreamWriter)streamWriterFieldInfo.GetValue(LogManager.Instance);

                if (oldSw != null) {
                    oldSw.Close();
                    oldSw = null;
                }

                streamWriterFieldInfo.SetValue(LogManager.Instance, sw);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Patch().\n{ex}");
            }
        }

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                Logging.Log($"Enabling...");

                _harmony.PatchAll();
                Patch(string.Format("Puck_{0:yyyy-MM-dd_HH-mm-ss}.log", DateTime.Now));

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

                Patch("Puck.log");
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
