// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using Netcode.Transports.WebSocket;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Solipsist
{
    public class NetworkConnectionHelper : MonoBehaviour
    {
        [SerializeField] private UnityEvent OnConnected;
        [SerializeField] private UnityEvent OnServerStarted;

        //[SerializeField] private GameObject anchoredRootPrefab;
        public GameObject AnchoredRoot;

        //private void Awake()
        //{
        //    SetXRPlugins();
        //}

        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += ((ulong obj) =>
            {
                if (OnConnected != null)
                {
                    OnConnected.Invoke();
                }
            });

#if UNITY_SERVER || UNITY_EDITOR
            NetworkManager.Singleton.OnServerStarted += Singleton_OnServerStarted;
#endif
            WebSocketTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as WebSocketTransport;
            transport.ConnectAddress = NetworkConfigurationManager.Instance.ServerAddr;
            transport.Port = NetworkConfigurationManager.Instance.ServerPort;

            switch (NetworkConfigurationManager.Instance.NetworkRole)
            {
                case NetworkRole.Server:
                    NetworkManager.Singleton.StartServer();
                    break;
                case NetworkRole.Client:
                    NetworkManager.Singleton.StartClient();
                    break;
                case NetworkRole.Host:
                    NetworkManager.Singleton.StartHost();
                    break;
            }
        }

        private void Singleton_OnServerStarted()
        {
            //this.AnchoredRoot = Instantiate(this.anchoredRootPrefab, Vector3.zero, Quaternion.identity);
            //this.AnchoredRoot.GetComponent<NetworkObject>().Spawn();

            if (OnServerStarted != null)
            {
                OnServerStarted.Invoke();
            }
        }

        private void OnServerInitialized()
        {
            if (OnConnected != null)
            {
                OnConnected.Invoke();
            }
        }

//        private void SetXRPlugins()
//        {
//#if UNITY_SERVER
//            Debug.Log("[NetworkConnectionHelper] Disabling XR Subsystem on server.");

//            XRGeneralSettings.Instance.Manager.StopSubsystems();
//            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
//#endif
//        }

        public void StartHost()
        {
            NetworkManager.Singleton.StartHost();
        }

        public void StartServer()
        {
            NetworkManager.Singleton.StartServer();
        }

        public void StartClient()
        {
            NetworkManager.Singleton.StartClient();
        }
    }
}