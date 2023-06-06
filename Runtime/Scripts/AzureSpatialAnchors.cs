// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.
#if !UNITY_WEBGL
using Solipsist.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

#if UNITY_WSA || UNITY_ANDROID
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
#endif

namespace Solipsist
{
    public class AzureSpatialAnchors : NetworkBehaviour
    {
        [SerializeField] private GameObject anchorObject;
        
        public void AddAnchor(Transform anchorTransform)
        {
            GameObject anchorObj = this.anchorObject != null ? GameObject.Instantiate(this.anchorObject) : new GameObject("SpatialAnchor");
            anchorObj.transform.position = anchorTransform.position;

#if UNITY_WSA || UNITY_ANDROID
            UnityDispatcher.InvokeOnAppThread(async () =>
            { 
                await AnchorObjectAsync(anchorObj, new AnchorObjectModel());
            });
#endif
        }

#if UNITY_WSA || UNITY_ANDROID
        private static readonly string ADD_ANCHOR_QUERY = "{0}/{1}/addanchor?code={2}";
        private static readonly string LIST_ANCHORS_QUERY = "{0}/{1}/getanchors?code={2}";

        private SpatialAnchorManager spatialAnchorManager = null;
        private CloudSpatialAnchorWatcher currentWatcher;
        private List<GameObject> foundOrCreatedAnchorGameObjects = new List<GameObject>();
        private List<AnchorObjectModel> createdAnchors = new List<AnchorObjectModel>();
        private object createdAnchorLock = new object();
        private bool isAnchorLoadNeeded = false;
        private bool isSessionReady = false;

        public event Action<AnchorObjectModel> AnchorStoredCallback;
        public event Action<List<AnchorObjectModel>> AnchorsReceivedCallback;
        public event Action<AnchorObjectModel, AnchorLocatedEventArgs> AnchorLocatedCallback;
        
        //public bool AreAnchorsLoaded() { get { return isSessionReady && 

        public AnchorObjectModel GetAnchorModel(string id)
        {
            foreach (AnchorObjectModel anchor in this.createdAnchors)
            {
                if (anchor.id == id)
                {
                    return anchor;
                }
            }

            return null;
        }

        public override void OnDestroy()
        {
            if (this.spatialAnchorManager != null)
            {
                this.spatialAnchorManager.StopSession();
            }

            if (this.currentWatcher != null)
            {
                this.currentWatcher.Stop();
                this.currentWatcher = null;
            }

            base.OnDestroy();
        }

        private void Start()
        {
            this.spatialAnchorManager = GetComponent<SpatialAnchorManager>();
            this.spatialAnchorManager.LogDebug += (sender, args) => Debug.Log($"ASA - Debug: {args.Message}");
            this.spatialAnchorManager.Error += (sender, args) => Debug.LogError($"ASA - Error: {args.ErrorMessage}");
            this.spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
            this.spatialAnchorManager.SessionStarted += SpatialAnchorManager_SessionStarted;
            this.spatialAnchorManager.SessionCreated += SpatialAnchorManager_SessionCreated;

            this.AnchorsReceivedCallback += OnAnchorsRetrieved;
            this.AnchorStoredCallback += OnAnchorStored;

            // Load Spatial Anchor Object dictionary
            StartCoroutine(RetrieveAnchors());
        }

        private void SpatialAnchorManager_SessionCreated(object sender, EventArgs e)
        {
            UnityDispatcher.InvokeOnAppThread(async () =>
            {
                //Start session and search for all Anchors previously created
                await this.spatialAnchorManager.StartSessionAsync();
            });
        }

        private void SpatialAnchorManager_SessionStarted(object sender, EventArgs e)
        {
            this.isSessionReady = true;
        }

        private void Update()
        {
            if (this.isAnchorLoadNeeded && this.isSessionReady)
            {
                lock(this.createdAnchorLock)
                {
                    this.isAnchorLoadNeeded = false;
                }

                LocateAnchors();
            }
        }

        private void OnAnchorStored(AnchorObjectModel anchorID)
        {
            Debug.Log($"Successfully pushed anchor to the cloud: {anchorID}");

            InvalidateAnchorsServerRpc();
        }

        private IEnumerator RetrieveAnchors()
        {
            // TODO: Restore exception handling like this: 
            // https://www.jacksondunstan.com/articles/3718#:~:text=Throwing%20an%20exception%20from%20a%20coroutine%20terminates%20the,the%20app.%20That%20leads%20to%20an%20interesting%20predicament.

            //try
            //{
            string reqAddr = string.Format(
                LIST_ANCHORS_QUERY,
                NetworkConfigurationManager.Instance.BaseAddress,
                NetworkConfigurationManager.Instance.ExperienceID,
                NetworkConfigurationManager.Instance.GetAnchorsApiCode);
            UnityWebRequest request = UnityWebRequest.Get(reqAddr);
            yield return request.SendWebRequest();

            while (!request.isDone)
            {
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Error retrieving anchors: {request.error}");
                yield break;
            }
            else
            {
                Debug.Log($"Received anchors:\n{request.downloadHandler.text}");
            }

            AnchorResultModel<AnchorObjectModel> anchorInfo = JsonUtility.FromJson<AnchorResultModel<AnchorObjectModel>>(request.downloadHandler.text);
            if (this.AnchorsReceivedCallback != null)
            {
                this.AnchorsReceivedCallback(anchorInfo.value);
            }

            //HttpClient client = new HttpClient();
            //var response = await client.GetFromJsonAsync(baseAddress + "/" + this.experienceID.ToString() + "/getanchors");
            //return new List<string> { "" };

            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //    Debug.LogError($"Failed to retrieve anchor ids for experience: {this.experienceID}.");
            //    return null;
            //}
        }

        protected virtual void OnAnchorsRetrieved(List<AnchorObjectModel> anchors)
        {
            lock (this.createdAnchorLock)
            {
                foreach (AnchorObjectModel anchor in anchors)
                {
                    Debug.Log($"Found anchor id: {anchor.id}");
                    this.createdAnchors.Add(anchor);
                }
            }

            // TODO: Should we skip this section if anchors list is empty?
            UnityDispatcher.InvokeOnAppThread(async () =>
            {
                if (this.spatialAnchorManager.Session == null)
                {
                    await this.spatialAnchorManager.CreateSessionAsync();
                }
            });

            lock (this.createdAnchorLock)
            {
                this.isAnchorLoadNeeded = true;
            }
        }

        private async Task CreateAnchor(GameObject anchorGameObject, AnchorObjectModel anchorData)
        {
            // TODO: Check if anchorData is null!
            // TODO: Also check if local anchor already exists
            //Add and configure ASA components
            CloudNativeAnchor cloudNativeAnchor = anchorGameObject.AddComponent<CloudNativeAnchor>();
            await cloudNativeAnchor.NativeToCloud();

            //Collect Environment Data
            while (!this.spatialAnchorManager.IsReadyForCreate)
            {
                float createProgress = this.spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
                Debug.Log($"ASA - Move your device to capture more environment data: {createProgress:0%}");
            }

            Debug.Log(String.Format($"ASA - Saving cloud anchor at position ({0}, {1}, {2})",
                anchorGameObject.transform.position.x,
                anchorGameObject.transform.position.y,
                anchorGameObject.transform.position.z));

            CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
            cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(365);

            try
            {
                // Now that the cloud spatial anchor has been prepared, we can try the actual save here.
                await this.spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

                bool saveSucceeded = cloudSpatialAnchor != null;
                if (!saveSucceeded)
                {
                    Debug.LogError("ASA - Failed to save, but no exception was thrown.");
                    return;
                }

                Debug.Log($"ASA - Saved cloud anchor with ID: {cloudSpatialAnchor.Identifier}");
                anchorData.id = cloudSpatialAnchor.Identifier;
                this.foundOrCreatedAnchorGameObjects.Add(anchorGameObject);
                lock (this.createdAnchorLock)
                {
                    this.createdAnchors.Add(anchorData);
                }

                // Persist this anchor by calling our cloud API
                StartCoroutine(StoreAnchor(anchorData));
            }
            catch (Exception exception)
            {
                Debug.Log("ASA - Failed to save anchor: " + exception.ToString());
                Debug.LogException(exception);
            }
        }

        private IEnumerator StoreAnchor(AnchorObjectModel anchor)
        {
            if (string.IsNullOrWhiteSpace(anchor.id))
            {
                Debug.Log("Can't store anchor with invalid ID");
                yield break;
            }

            string reqAddr = string.Format(
                ADD_ANCHOR_QUERY,
                NetworkConfigurationManager.Instance.BaseAddress,
                NetworkConfigurationManager.Instance.ExperienceID,
                NetworkConfigurationManager.Instance.AddAnchorApiCode);
            string anchorJson = JsonUtility.ToJson(anchor);
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(anchorJson);
            var request = UnityWebRequest.Put(reqAddr, jsonToSend);

            request.SetRequestHeader("Content-Type", "application/json");

            //Send the request then wait here until it returns
            yield return request.SendWebRequest();

            while (!request.isDone)
            {
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Error While Sending: " + request.result);
                yield break;
            }
            else
            {
                Debug.Log("Received: " + request.downloadHandler.text);
            }

            if (this.AnchorStoredCallback != null)
            {
                this.AnchorStoredCallback(anchor);
            }
        }

        public async Task AnchorObjectAsync(GameObject anchorObject, AnchorObjectModel anchorData)
        {
            if (anchorObject == null)
            {
                Debug.LogError("ASA - Trying to serialize a null anchorObject");
                return;
            }

            if (!this.spatialAnchorManager.IsSessionStarted)
            {
                await this.spatialAnchorManager.StartSessionAsync();
            }

            await CreateAnchor(anchorObject, anchorData);
        }

        public GameObject SpawnAnchoredObject(GameObject objectTemplate, CloudSpatialAnchor cloudSpatialAnchor)
        {
            Pose anchorPose = cloudSpatialAnchor.GetPose();

            // Create the prefab
            GameObject newGameObject = GameObject.Instantiate(objectTemplate, anchorPose.position, anchorPose.rotation);

            AnchorGameObject(newGameObject, cloudSpatialAnchor);

            // Return created object
            return newGameObject;
        }

        public void AnchorGameObject(GameObject obj, CloudSpatialAnchor cloudSpatialAnchor)
        {
            // Attach a cloud-native anchor behavior to help keep cloud
            // and native anchors in sync.
            obj.AddComponent<CloudNativeAnchor>();

            // If a cloud anchor is passed, apply it to the native anchor
            if (cloudSpatialAnchor != null)
            {
                CloudNativeAnchor cloudNativeAnchor = obj.GetComponent<CloudNativeAnchor>();
                cloudNativeAnchor.CloudToNative(cloudSpatialAnchor);
            }
        }

        private void RemoveAllAnchorGameObjects()
        {
            foreach (var anchorGameObject in this.foundOrCreatedAnchorGameObjects)
            {
                Destroy(anchorGameObject);
            }
            this.foundOrCreatedAnchorGameObjects.Clear();
        }

        private CloudSpatialAnchorWatcher CreateWatcher(AnchorLocateCriteria anchorLocateCriteria)
        {
            if ((this.spatialAnchorManager != null) && (this.spatialAnchorManager.Session != null))
            {
                return this.spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
            }
            else
            {
                return null;
            }
        }

        private void LocateAnchors()
        {
            lock (this.createdAnchorLock)
            {
                if (this.createdAnchors.Count > 0)
                {
                    //Create watcher to look for all stored anchor IDs
                    Debug.Log($"ASA - Creating watcher to look for spatial anchors");
                    AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
                    anchorLocateCriteria.Identifiers = this.createdAnchors.Select(a => a.id).ToArray();

                    if (currentWatcher != null)
                    {
                        currentWatcher.Stop();
                        currentWatcher = null;
                    }
                    currentWatcher = CreateWatcher(anchorLocateCriteria);
                    if (currentWatcher == null)
                    {
                        Debug.Log("Either cloudmanager or session is null, should not be here!");
                    }
                    else
                    {
                        Debug.Log($"ASA - Watcher created!");
                    }
                }
            }
        }

        private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
        {
            Debug.Log($"ASA - Anchor recognized as a possible anchor {args.Identifier} {args.Status}");

            if (args.Status == LocateAnchorStatus.Located)
            {
                AnchorObjectModel anchor = GetAnchorModel(args.Identifier);

                if (anchor != null)
                {
                    if (this.anchorObject != null)
                    {
                        SpawnAnchoredObject(this.anchorObject, args.Anchor);
                    }

                    if (this.AnchorLocatedCallback != null)
                    {
                        this.AnchorLocatedCallback(anchor, args);
                    }
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void InvalidateAnchorsServerRpc(ServerRpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            if (NetworkManager.ConnectedClients.ContainsKey(clientId))
            {
                InvalidateAnchorsClientRpc();
            }
        }

        [ClientRpc]
        private void InvalidateAnchorsClientRpc()
        {
            lock (this.createdAnchorLock)
            {
                this.isAnchorLoadNeeded = true;
            }
        }
#endif
    }
}

#endif