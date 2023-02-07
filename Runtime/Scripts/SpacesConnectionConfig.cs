// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using UnityEngine;

namespace Solipsist
{
    public enum NetworkRole
    {
        Server,
        Client,
        Host
    }

    [CreateAssetMenu(fileName = "SpacesConnectionConfig", menuName = "Solipsist Spaces/Configuration")]
    public class SpacesConnectionConfig : ScriptableObject
    {
        [Header("Client Info")]
        [SerializeField]
        [Tooltip("The role this node will play on the network.")]
        protected NetworkRole networkRole = NetworkRole.Client;
        public NetworkRole NetworkRole => networkRole;

        [Header("Server Info")]
        [SerializeField]
        [Tooltip("IP address of the server endpoint.")]
        protected string serverAddr = "127.0.0.1";
        public string ServerAddr => serverAddr;

        [SerializeField]
        [Tooltip("Port of the server endpoint.")]
        protected ushort serverPort = 7777;
        public ushort ServerPort => serverPort;

        [Header("Azure Info")]
        [SerializeField]
        [Tooltip("Base address of API (ending in /api)")]
        protected string baseAPIAddress = "";
        public string BaseAPIAddress => baseAPIAddress;

        [SerializeField]
        [Tooltip("Experience ID obtained when uploading to Azure")]
        protected string experienceID = "";
        public string ExperienceID => experienceID;
    }
}