﻿// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Firebase.Unity.Editor;

namespace Hamster {

  public class MainGame : MonoBehaviour {

    private States.StateManager stateManager = new States.StateManager();
    private float currentFrameTime, lastFrameTime;

    private const string PlayerPrefabID = "Player";

    // The active player object in the game.
    public GameObject player;
    // The PlayerController component on the active player object.
    public PlayerController PlayerController {
      get {
        return player != null ? player.GetComponent<PlayerController>() : null;
      }
    }

    public DBStruct<UserData> currentUser;

    void Start() {
      InitializeFirebaseAndStart();
    }

    void Update() {
      lastFrameTime = currentFrameTime;
      currentFrameTime = Time.realtimeSinceStartup;
      stateManager.Update();
    }

    // Utility function to check the time since the last update.
    // Needed, since we can't use Time.deltaTime, as we are adjusting the
    // simulation timestep.  (Setting it to 0 to pause the world.)
    public float TimeSinceLastUpdate {
      get { return currentFrameTime - lastFrameTime; }
    }

    // Utility function to check if the game is currently running.  (i.e.
    // not in edit mode.)
    public bool isGameRunning() {
      States.BaseState state = stateManager.CurrentState();
      return (state is States.Gameplay ||
        // While with LevelFinished the game is not technically running, we want
        // to mimic the traditional behavior in the background.
        state is States.LevelFinished);
    }

    // Utility function for spawning the player.
    public GameObject SpawnPlayer() {
      if (player == null) {
        player = (GameObject)Instantiate(CommonData.prefabs.lookup[PlayerPrefabID].prefab);
      }
      return player;
    }

    // Utility function for despawning the player.
    public void DestroyPlayer() {
      if (player != null) {
        Destroy(player);
        player = null;
      }
    }

    // Pass through to allow states to have their own GUI.
    void OnGUI() {
      stateManager.OnGUI();
    }

    void InitializeAnalytics() {
      Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

      // Set the user's sign up method.
      Firebase.Analytics.FirebaseAnalytics.SetUserProperty(
        Firebase.Analytics.FirebaseAnalytics.UserPropertySignUpMethod,
        "Google");

      // TODO(ccornell): replace this with a real user token
      // once Auth gets hooked up.
      // Set the user ID.
      Firebase.Analytics.FirebaseAnalytics.SetUserId("desktop_user");
    }

    // Sets the default values for remote config.  These are the values that will
    // be used if we haven't fetched yet.
    System.Threading.Tasks.Task InitializeRemoteConfig() {
      Dictionary<string, object> defaults = new Dictionary<string, object>();

      // Physics defaults:
      defaults.Add(StringConstants.RemoteConfigPhysicsGravity, -20.0f);

      // Invites defaults:
      defaults.Add(StringConstants.RemoteConfigInviteTitleText,
          StringConstants.DefaultInviteTitleText);
      defaults.Add(StringConstants.RemoteConfigInviteMessageText,
          StringConstants.DefaultInviteMessageText);
      defaults.Add(StringConstants.RemoteConfigInviteCallToActionText,
          StringConstants.DefaultInviteCallToActionText);

      // Defaults for Map Objects:
      // Acceleration Tile
      defaults.Add(StringConstants.RemoteConfigAccelerationTileForce, 24.0f);
      // Drag Tile
      defaults.Add(StringConstants.RemoteConfigSandTileDrag, 5.0f);
      // Jump Tile
      defaults.Add(StringConstants.RemoteConfigJumpTileVelocity, 8.0f);
      // Mine Tile
      defaults.Add(StringConstants.RemoteConfigMineTileForce, 10.0f);
      defaults.Add(StringConstants.RemoteConfigMineTileRadius, 2.0f);
      defaults.Add(StringConstants.RemoteConfigMineTileUpwardsMod, 0.2f);
      // Spikes Tile
      defaults.Add(StringConstants.RemoteConfigSpikesTileForce, 10.0f);
      defaults.Add(StringConstants.RemoteConfigSpikesTileRadius, 1.0f);
      defaults.Add(StringConstants.RemoteConfigSpikesTileUpwardsMod, -0.5f);

      Firebase.RemoteConfig.FirebaseRemoteConfig.SetDefaults(defaults);
      return Firebase.RemoteConfig.FirebaseRemoteConfig.FetchAsync(System.TimeSpan.Zero);
    }

    // When the app starts, check to make sure that we have
    // the required dependencies to use Firebase, and if not,
    // add them if possible.
    void InitializeFirebaseAndStart() {
      Firebase.DependencyStatus dependencyStatus = Firebase.FirebaseApp.CheckDependencies();

      if (dependencyStatus != Firebase.DependencyStatus.Available) {
        Firebase.FirebaseApp.FixDependenciesAsync().ContinueWith(task => {
          dependencyStatus = Firebase.FirebaseApp.CheckDependencies();
          if (dependencyStatus == Firebase.DependencyStatus.Available) {
            InitializeFirebaseComponents();
          } else {
            Debug.LogError(
                "Could not resolve all Firebase dependencies: " + dependencyStatus);
            Application.Quit();
          }
        });
      } else {
        InitializeFirebaseComponents();
      }
    }

    void InitializeFirebaseComponents() {
      InitializeAnalytics();

      System.Threading.Tasks.Task.WhenAll(
          InitializeRemoteConfig()
        ).ContinueWith(task => { StartGame(); });
    }

    // Actually start the game, once we've verified that everything
    // is working and we have the firebase prerequisites ready to go.
    void StartGame() {
      // Remote Config data has been fetched, so this applies it for this play session:
      Firebase.RemoteConfig.FirebaseRemoteConfig.ActivateFetched();

      CommonData.prefabs = FindObjectOfType<PrefabList>();
      CommonData.mainCamera = FindObjectOfType<CameraController>().GetComponentInChildren<Camera>();
      CommonData.mainGame = this;
      Firebase.AppOptions ops = new Firebase.AppOptions();
      CommonData.app = Firebase.FirebaseApp.Create(ops);
      CommonData.app.SetEditorDatabaseUrl("https://hamster-demo.firebaseio.com/");

      Screen.orientation = ScreenOrientation.Landscape;

      CommonData.gameWorld = FindObjectOfType<GameWorld>();
      currentUser = new DBStruct<UserData>("user", CommonData.app);

      stateManager.PushState(new States.Startup());
    }
  }
}
