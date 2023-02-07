// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Solipsist
{
    public class ConfigurationManager : MonoBehaviour
    {
        public NetworkRole NetworkRole { get; private set; }
        public string ServerAddr { get; private set; }
        public ushort ServerPort { get; private set; }
        public string BaseAddress { get; private set; }
        public string ExperienceID { get; private set; }
        public string AddAnchorApiCode { get; private set; }
        public string ListAnchorsApiCode { get; private set; }

        public static ConfigurationManager Instance { get; private set; }

        /// <summary>
        /// Called when the manager initializes in order to configure the service.
        /// </summary>
        protected virtual void LoadConfiguration()
        {
            // Attempt to load configuration from config resource if present.
            // TODO: Don't hardcode the name here.
            StageConnectionConfig config = Resources.Load<StageConnectionConfig>("StageConnectionConfig");
            if (config != null)
            {
                this.NetworkRole = config.NetworkRole;

                if (!string.IsNullOrEmpty(config.ServerAddr))
                {
                    this.ServerAddr = config.ServerAddr;
                }
                
                this.ServerPort = config.ServerPort;
            }
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Set up defaults
            this.NetworkRole =
#if UNITY_SERVER
                NetworkRole.Server;
#else
                NetworkRole.Client;
#endif

            // Overwrite defaults with Config File
            LoadConfiguration();

            // Overwrite Config File with Command Line
#if !UNITY_EDITOR
            var args = GetCommandlineArgs();
            if (args.TryGetValue("-mlapi", out string mlapiValue))
            {
                if (NetworkRole.TryParse(mlapiValue, ignoreCase: true, out NetworkRole role))
                {
                    this.NetworkRole = role;
                }
            }

            if (args.TryGetValue("-ip", out string ipAddr))
            {
                this.ServerAddr = ipAddr;
            }

            if (args.TryGetValue("-port", out string portValue))
            {
                if (ushort.TryParse(portValue, out ushort port))
                {
                    this.ServerPort = port;
                }
            }
#endif

            // TODO: Make this a general list of settings.
            // TODO: Add a Save button.
        }

#if !UNITY_EDITOR
        private Dictionary<string, string> GetCommandlineArgs()
        {
            Dictionary<string, string> argDictionary = new Dictionary<string, string>();

            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; ++i)
            {
                var arg = args[i].ToLower();
                if (arg.StartsWith("-"))
                {
                    var value = i < args.Length - 1 ? args[i + 1].ToLower() : null;
                    value = (value?.StartsWith("-") ?? false) ? null : value;

                    argDictionary.Add(arg, value);
                }
            }
            return argDictionary;
        }
#endif
    }
}