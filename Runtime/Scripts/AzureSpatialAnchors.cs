// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using Solipsist.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

#if UNITY_WSA || UNITY_ANDROID
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
#endif

namespace Solipsist
{
    public class AzureSpatialAnchors : MonoBehaviour
    {
#if UNITY_WSA || UNITY_ANDROID
        private static readonly string ADD_ANCHOR_QUERY = "{0}/{1}/addanchor?code={2}";
        private static readonly string LIST_ANCHORS_QUERY = "{0}/{1}/getanchors?code={2}";

        private SpatialAnchorManager spatialAnchorManager = null;
        private CloudSpatialAnchorWatcher currentWatcher;
        private List<GameObject> foundOrCreatedAnchorGameObjects = new List<GameObject>();
        private List<AnchorObjectModel> createdAnchors = new List<AnchorObjectModel>();

        public event Action<AnchorObjectModel> AnchorStoredCallback;
        public event Action<List<AnchorObjectModel>> AnchorsReceivedCallback;
        public event Action<AnchorObjectModel, AnchorLocatedEventArgs> AnchorLocatedCallback;

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

        private void OnDestroy()
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
        }

        void Start()
        {
            this.spatialAnchorManager = GetComponent<SpatialAnchorManager>();
            this.spatialAnchorManager.LogDebug += (sender, args) => Debug.Log($"ASA - Debug: {args.Message}");
            this.spatialAnchorManager.Error += (sender, args) => Debug.LogError($"ASA - Error: {args.ErrorMessage}");
            this.spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

            this.AnchorsReceivedCallback += OnAnchorsRetrieved;
            this.AnchorStoredCallback += OnAnchorStored;

            // Load Spatial Anchor Object dictionary
            StartCoroutine(RetrieveAnchors());
        }

        private void OnAnchorStored(AnchorObjectModel anchorID)
        {
            Debug.Log($"Successfully pushed anchor to the cloud: {anchorID}");
        }

        private IEnumerator RetrieveAnchors()
        {
            // TODO: Restore exception handling like this: 
            // https://www.jacksondunstan.com/articles/3718#:~:text=Throwing%20an%20exception%20from%20a%20coroutine%20terminates%20the,the%20app.%20That%20leads%20to%20an%20interesting%20predicament.

            //try
            //{
            string reqAddr = string.Format(
                LIST_ANCHORS_QUERY,
                ConfigurationManager.Instance.BaseAddress,
                ConfigurationManager.Instance.ExperienceID,
                ConfigurationManager.Instance.ListAnchorsApiCode);
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
            foreach (AnchorObjectModel anchor in anchors)
            {
                Debug.Log($"Found anchor id: {anchor.id}");
                this.createdAnchors.Add(anchor);
            }

            UnityDispatcher.InvokeOnAppThread(async () =>
            {
                if (this.spatialAnchorManager.Session == null)
                {
                    await this.spatialAnchorManager.CreateSessionAsync();
                }

                //Start session and search for all Anchors previously created
                await this.spatialAnchorManager.StartSessionAsync();
                LocateAnchors();
            });
        }

        private async Task CreateAnchor(GameObject anchorGameObject, AnchorObjectModel anchorData)
        {
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
                this.createdAnchors.Add(anchorData);

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
                ConfigurationManager.Instance.BaseAddress,
                ConfigurationManager.Instance.ExperienceID,
                ConfigurationManager.Instance.AddAnchorApiCode);
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

        public async void AddAnchor(GameObject anchorObject, AnchorObjectModel anchorData)
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

            // Attach a cloud-native anchor behavior to help keep cloud
            // and native anchors in sync.
            newGameObject.AddComponent<CloudNativeAnchor>();

            // If a cloud anchor is passed, apply it to the native anchor
            if (cloudSpatialAnchor != null)
            {
                CloudNativeAnchor cloudNativeAnchor = newGameObject.GetComponent<CloudNativeAnchor>();
                cloudNativeAnchor.CloudToNative(cloudSpatialAnchor);
            }

            // Return created object
            return newGameObject;
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

        private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
        {
            Debug.Log($"ASA - Anchor recognized as a possible anchor {args.Identifier} {args.Status}");

            if (args.Status == LocateAnchorStatus.Located && this.AnchorLocatedCallback != null)
            {
                AnchorObjectModel anchor = GetAnchorModel(args.Identifier);

                if (anchor != null)
                {
                    this.AnchorLocatedCallback(anchor, args);
                }
            }
        }
#endif
    }
}