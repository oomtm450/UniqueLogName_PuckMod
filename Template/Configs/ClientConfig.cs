﻿using Newtonsoft.Json;
using oomtm450PuckMod_Template.SystemFunc;
using System;
using System.IO;

namespace oomtm450PuckMod_Template.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_template_clientconfig.json used for this mod.
    /// </summary>
    public class ClientConfig : IConfig {
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// Function that serialize the ClientConfig object.
        /// </summary>
        /// <returns>String, serialized ClientConfig.</returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Function that unserialize a ClientConfig.
        /// </summary>
        /// <param name="json">String, JSON that is the serialized ClientConfig.</param>
        /// <returns>ClientConfig, unserialized ClientConfig.</returns>
        internal static ClientConfig SetConfig(string json) {
            return JsonConvert.DeserializeObject<ClientConfig>(json);
        }

        /// <summary>
        /// Function that reads the config file for the mod and create a ClientConfig object with it.
        /// Also creates the file with the default values, if it doesn't exists.
        /// </summary>
        /// <returns>ClientConfig, parsed config.</returns>
        internal static ClientConfig ReadConfig() {
            ClientConfig config = new ClientConfig();

            try {
                string rootPath = Path.GetFullPath(".");
                string configPath = Path.Combine(rootPath, Constants.MOD_NAME + "_clientconfig.json");
                if (File.Exists(configPath)) {
                    string configFileContent = File.ReadAllText(configPath);
                    config = SetConfig(configFileContent);
                    Logging.Log($"Client config read.", config, true);
                }

                try {
                    File.WriteAllText(configPath, config.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Can't write the client config file. (Permission error ?)\n{ex}");
                }

                Logging.Log($"Wrote client config : {config}", config);
            }
            catch (Exception ex) {
                Logging.LogError($"Can't read the client config file/folder. (Permission error ?)\n{ex}");
            }

            return config;
        }
    }
}
