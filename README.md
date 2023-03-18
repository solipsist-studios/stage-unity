# stage-unity
Unity package for creating experiences with Solipsist Stage.

NB: This package is in preview! Do not depend on the API for experiences in production!

## About
The goal of the Stage platform is to provide creators with the tools to share their mixed reality creations with the world.  Powered by Azure Spatial Anchors and built to run in the Azure cloud. 

A fully-managed, hosted service is offered for those who are looking to start building without the technical overhead.  See [Solipsist Studios Homepage](https://solipsist.studio).

Self-hosting is always free!

## Features
* Support for Netcode for Game Objects networking package
* Serialize and deserialize objects attached to spatial anchors

## Supported Platforms
* Android devices (API >= 8.0)
* Microsoft HoloLens 2
* WebXR coming soon!

Also probably works on iOS and Magic Leap, but is untested.

## Getting Started

1. Import Dependencies
    ![Using the Mixed Reality Feature Tool to import dependencies](https://user-images.githubusercontent.com/19314267/226135369-11d9a3df-3f89-4e95-9de3-0c111625054d.gif)
    1. Use the [Microsoft Mixed Reality Feature Tool](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/welcome-to-mr-feature-tool#download) to install the following features:
        1. Azure Spatial Anchors SDK Core
        1. Azure Spatial Anchors SDK (Windows, iOS and/or Android)
        1. Mixed Reality OpenXR Plugin
    1. Install Unity packages using [Install from a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html) for the following packages:
        1. WebSocket transport for Netcode for Game Objects 
        `https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.websocket`
        1. Add this package from a Git URL using the same process:
        `https://github.com/solipsist-studios/stage-unity.git`
    1. Set up MRTK or other XR interaction packages according to their instructions
1. Add necessary prefabs to scene
   ![Adding prefabs to the scene](https://user-images.githubusercontent.com/19314267/226136332-89a2c2aa-d5b4-4851-b722-48dcee6efcaf.gif)
    1. Network Manager
    1. Azure Spatial Anchors Manager
![Using the Mixed Reality Feature Tool to import dependencies](./Assets/feature_tool.webm)
1. Add calls to load / save spatial anchors
    1. Call the `AzureSpatialAnchorsManager.AzureSpatialAnchors.AddAnchor` function to persist an object
    1. Subscribe to `AzureSpatialAnchorsManager.AzureSpatialAnchors.AnchorLocatedCallback` to be notified when an anchor is loaded
1. Build the Unity project for the Dedicated Server/Linux platform
1. Add the contents of the build directory, excluding the `..._BurstDebugInformation_DoNotShip` folder, to a .tar.gz archive.
1. Use the solx CLI tool to upload and launch your dedicated server
    1. Follow the steps to [download and use the solx CLI tool](https://github.com/solipsist-studios/MixedRealityStage/blob/main/ExperienceCatalogCLI/README.md).
    1. Open a command prompt
    1. Navigate to the folder where you have extracted the solx.exe file
    1. Run the following command
  ```
  .\solx cat add --file "c:\path\to\your\linuxserver.tar.gz" --name app-name
  ```
  Note that `app-name` should contain only alphanumeric characters, '-' or '_'
    1. When that completes, take note of the `experience-id` that is generated
    1. Run the following command
  ```
  .\solx cat launch --experience-id 00000000-0000-0000-0000-000000000000
  ```
  Replacing the experience-id with the one you copied from the previous step
1. Build and deploy the Unity project to your client devices
Your Azure administrator can provide you with the values for the following steps
    1. In Unity, open the Assets/AzureSpatialAnchors.SDK/Resources/SpatialAnchorConfig.asset file
    1. In the inspector, select "Api Key" for Authentication Mode, and fill out each value under Credentials
    1. Under Assets, create a Resources folder, and in that folder create a Solipsist Stage->Configuration asset
    1. Accept the default name, `StageConnectionConfig.asset`
    1. Open the StageConnectionConfig.asset file, and fill out the values in the Inspector
    1. Build the project for your desired platform and run
  
