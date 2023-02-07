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

        [SerializeField] private GameObject PlayerVisuals = null; // Root of physical/visible object

        private void Start()
        {
            userCamera = Camera.main;

            // Default to this object if a player object isn't explicitly set
            if (PlayerVisuals == null)
            {
                PlayerVisuals = this.gameObject;
            }

            if (IsOwner)
            {
                Color randomColor = Random.ColorHSV(0, 1, 0, 1, 0.5f, 1);
                randomColor.a = 0.77f;
                PlayerColor.Value = randomColor;
                PlayerColor.SetDirty(true);
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
                this.PlayerVisuals.SetActive(false);

                // Apply camera transformation.
                transform.SetPositionAndRotation(userCamera.transform.position, userCamera.transform.rotation);
                HeadPosition.Value = userCamera.transform.position;
                HeadRotation.Value = userCamera.transform.rotation;
                HeadPosition.SetDirty(true);
                HeadRotation.SetDirty(true);
            }
            else // Better mirror whatever we're told!
            {
                transform.SetPositionAndRotation(HeadPosition.Value, HeadRotation.Value);

                var visualRenderer = this.PlayerVisuals.GetComponentInChildren<SkinnedMeshRenderer>();
                if (visualRenderer != null && visualRenderer.material != null)
                {
                    visualRenderer.material.color = PlayerColor.Value;
                }
            }
        }
    }
}