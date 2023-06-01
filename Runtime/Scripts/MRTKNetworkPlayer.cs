// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using Unity.Netcode;
using UnityEngine;

namespace Solipsist
{
    public class MRTKNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<Vector3> HeadPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<Quaternion> HeadRotation = new NetworkVariable<Quaternion>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        private Camera userCamera;

        [SerializeField] private GameObject playerVisuals = null; // Root of physical/visible object

        private void Start()
        {
            userCamera = Camera.main;

            // Default to this object if a player object isn't explicitly set
            if (playerVisuals == null)
            {
                playerVisuals = this.gameObject;
            }

            if (IsOwner)
            {
                Color randomColor = Random.ColorHSV(0, 1, 0, 1, 0.5f, 1);
                randomColor.a = 0.77f;
                PlayerColor.Value = randomColor;
                PlayerColor.SetDirty(true);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Server subscribes to the NetworkSceneManager.OnSceneEvent event
                NetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;

                // Server player is parented under this NetworkObject
                SetPlayerParent(NetworkManager.LocalClientId);
            }
        }

        private void SetPlayerParent(ulong clientId)
        {
            var connectionHelper = FindObjectOfType<NetworkConnectionHelper>();
            if (connectionHelper == null ||
                connectionHelper.AnchoredRoot == null)
            {
                return;
            }

            if (IsSpawned && IsServer)
            {
                // As long as the client (player) is in the connected clients list
                if (NetworkManager.ConnectedClients.ContainsKey(clientId))
                {
                    // Set the player as a child of this in-scene placed NetworkObject 
                    NetworkObject netObj = NetworkManager.ConnectedClients[clientId].PlayerObject;
                    netObj.TrySetParent(connectionHelper.AnchoredRoot, false);

                }
            }
        }

        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            // OnSceneEvent is useful for many things
            switch (sceneEvent.SceneEventType)
            {
                // The C2S_SyncComplete event tells the server that a client-player has:
                // 1.) Connected and Spawned
                // 2.) Loaded all scenes that were loaded on the server at the time of connecting
                // 3.) Synchronized (instantiated and spawned) all NetworkObjects in the network session
                case SceneEventType.SynchronizeComplete:
                {
                    // As long as we aren't the server-player
                    if (sceneEvent.ClientId != NetworkManager.LocalClientId)
                    {
                        // Set the newly joined and synchronized client-player as a child of this in-scene placed NetworkObject
                        SetPlayerParent(sceneEvent.ClientId);
                    }
                    break;
                }
            }
        }
            
        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                // Don't show our own avatar!
                this.playerVisuals.SetActive(false);

                // Apply camera transformation.
                //transform.SetLocalPositionAndRotation(userCamera.transform.position, userCamera.transform.rotation);
                transform.SetPositionAndRotation(userCamera.transform.position, userCamera.transform.rotation);
                HeadPosition.Value = userCamera.transform.position;
                HeadRotation.Value = userCamera.transform.rotation;
                HeadPosition.SetDirty(true);
                HeadRotation.SetDirty(true);
            }
            //else // Better mirror whatever we're told!
            //{
            //    transform.SetPositionAndRotation(HeadPosition.Value, HeadRotation.Value);

            //    var visualRenderer = this.playerVisuals.GetComponentInChildren<SkinnedMeshRenderer>();
            //    if (visualRenderer != null && visualRenderer.material != null)
            //    {
            //        visualRenderer.material.color = PlayerColor.Value;
            //    }
            //}
        }
    }
}