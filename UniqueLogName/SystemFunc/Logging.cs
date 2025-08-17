using UnityEngine;

namespace oomtm450PuckMod_UniqueLogName.SystemFunc {
    internal class Logging {
        /// <summary>
        /// Function that logs information to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        internal static void Log(string msg) {
            Debug.Log($"[{Constants.MOD_NAME}] {msg}");
        }

        /// <summary>
        /// Function that logs errors to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        internal static void LogError(string msg) {
            Debug.LogError($"[{Constants.MOD_NAME}] {msg}");
        }

        /// <summary>
        /// Function that logs warnings to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        internal static void LogWarning(string msg) {
            Debug.LogWarning($"[{Constants.MOD_NAME}] {msg}");
        }
    }
}
