using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.Management;
using System.Linq;
using System;

namespace Solipsist
{
    [Serializable]
    public enum SupportedPlatform
    {
        Android,
        Hololens,
        iOS,
        LinuxServer,
        Oculus,
        WebGL
    }

    [Serializable]
    public class PlatformComponents
    {
        public SupportedPlatform SupportedPlatform;
        public List<GameObject> Components = new List<GameObject>();
    }

    public class PlatformSelector : MonoBehaviour
    {
        public List<PlatformComponents> PlatformComponents = new List<PlatformComponents>();

        private void DisableAllComponents()
        {
            foreach (PlatformComponents platformComponents in this.PlatformComponents)
            {
                Debug.Log("Found objects for platform " + platformComponents.SupportedPlatform.ToString());
                foreach (GameObject component in platformComponents.Components)
                {
                    if (component != null)
                    {
                        Debug.Log("    Disabling " + component.name);
                        component.SetActive(false);
                    }
                }
            }
        }

        private void SetActiveComponents()
        {
            DisableAllComponents();
            PlatformComponents platformComponents = null;

#if UNITY_SERVER
            // Dedicated server
            platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.LinuxServer);

#elif UNITY_WEBGL
            // WebGL
            platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.WebGL);

#elif UNITY_ANDROID
            if (XRGeneralSettings.Instance &&
                XRGeneralSettings.Instance.Manager &&
                XRGeneralSettings.Instance.Manager.activeLoader)
            {
                // Oculus mode
                platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.Oculus);
            }
            else
            {
                // Android handheld device
                platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.Android);
            }
#elif UNITY_WSA
            // Hololens
            platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.Hololens);

#elif UNITY_IOS
            // iOS
            platformComponents = this.PlatformComponents.SingleOrDefault(c => c.SupportedPlatform == SupportedPlatform.iOS);

#endif
            if (platformComponents == null)
            {
                Debug.LogWarning("No platform configuration detected for active platform!");
                return;
            }

            Debug.Log(string.Format("{0} Build detected", platformComponents.SupportedPlatform.ToString()));

            foreach (GameObject obj in platformComponents.Components)
            {
                Debug.Log("Enabling" + obj.name);
                obj.SetActive(true);
            }
        }

        private void Start()
        {
            StartCoroutine(StartXR());
        }

        private IEnumerator StartXR()
        {
            Debug.Log("Initializing XR...");

            if (XRGeneralSettings.Instance == null)
            {
                //XRGeneralSettings.Instance = ScriptableObject.CreateInstance<XRGeneralSettings>();
                //XRGeneralSettings.Instance.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                //var loader = ScriptableObject.CreateInstance<OpenXRLoader>();
                //XRGeneralSettings.Instance.Manager.TryAddLoader(loader);
                //loader.Initialize();
                //Debug.Log("OpenVRLoader created...");
                Debug.Log("XR GeneralSettings is null");
                yield break;
            }

            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

            if (XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                Debug.LogError("Initializing XR Failed. Check Editor or Player log for details.");
            }
            else
            {
                Debug.Log("Starting XR...");
                XRGeneralSettings.Instance.Manager.StartSubsystems();
                //SteamVR.Initialize(false);
            }

            SetActiveComponents();
        }

        private void OnDestroy()
        {
            StopXR();
        }

        private void StopXR()
        {
            Debug.Log("Stopping XR...");
            if (XRGeneralSettings.Instance.Manager.isInitializationComplete)
            {
                XRGeneralSettings.Instance.Manager.StopSubsystems();
                XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            }
            Debug.Log("XR stopped completely.");
        }

        
    }
}
