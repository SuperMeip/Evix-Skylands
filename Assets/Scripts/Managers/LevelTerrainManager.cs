using System;
using Evix.Controllers;
using Evix.Events;
using Evix.Terrain.Collections;
using Evix.Terrain.Resolution;
using UnityEngine;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;

namespace Evix.Managers {

  /// <summary>
  /// A manager that deals with moving chunks around to represent the level around foci
  /// </summary>
  public class LevelTerrainManager : MonoBehaviour, IObserver {

    /// <summary>
    /// The scene to use as the basis for a chunk
    /// </summary>
    public GameObject ChunkPrefab;

    /// <summary>
    /// The level this apeture works for
    /// </summary>
    public Level level {
      get;
      private set;
    }

    /// <summary>
    /// The thread running for the apertureJobQueue
    /// </summary>
    System.Threading.Thread chunkApertureManagerThread;

    /// <summary>
    /// IF the chunk manager queue should be running for this level
    /// </summary>
    bool runChunkManager = false;

    /// <summary>
    /// If the manager is loaded yet
    /// </summary>
    bool isLoaded = false;

    #region Chunk Queues

    /// <summary>
    /// The chunk node pool
    /// <summary>
    readonly ConcurrentQueue<ChunkController> freeChunkControllerPool
      = new ConcurrentQueue<ChunkController>();

    /// <summary>
    /// The chunk node pool
    /// <summary>
    readonly ConcurrentDictionary<Coordinate, ChunkController> usedChunkControllers
      = new ConcurrentDictionary<Coordinate, ChunkController>();

    /// <summary>
    /// Chunks waiting for assignement and activation
    /// </summary>
    readonly ConcurrentPriorityQueue<float, ChunkResolutionAperture.Adjustment> chunksWaitingForAFreeController
      = new ConcurrentPriorityQueue<float, ChunkResolutionAperture.Adjustment>();

    /// <summary>
    /// Chunk controllers waiting meshing
    /// </summary>
    readonly ConcurrentPriorityQueue<float, ChunkController> chunksToMesh
      = new ConcurrentPriorityQueue<float, ChunkController>();

    /// <summary>
    /// Chunk controllers waiting for assignement and activation
    /// </summary>
    readonly ConcurrentPriorityQueue<float, Coordinate> chunksToActivate
      = new ConcurrentPriorityQueue<float, Coordinate>();

    /// <summary>
    /// Chunk controllers waiting for assignement and activation
    /// </summary>
    readonly ConcurrentPriorityQueue<float, Coordinate> chunksToDeactivate
      = new ConcurrentPriorityQueue<float, Coordinate>();

    /// <summary>
    /// Chunk controllers waiting for assignement and activation
    /// </summary>
    readonly ConcurrentPriorityQueue<float, ChunkController> chunksToDemesh
      = new ConcurrentPriorityQueue<float, ChunkController>();

    #endregion

    /// <summary>
    /// The current max chunk object ID. Used to name chunks
    /// </summary>
    int currentMaxChunkObjectID = 0;

    #region Initialization

    /// <summary>
    /// Initilize the level queue manager to follow the foci and appetures of the level
    /// </summary>
    /// <param name="level"></param>
    public void initializeFor(Level level, ILevelFocus initialFocus) {
      if (ChunkPrefab == null) {
        throw new System.MissingMemberException("LevelManager Missing ChunkNode, can't work");
      } else if (level == null) {
        throw new System.MissingMemberException("LevelManager Missing a Level, can't work");
      } else {
        this.level = level;
        runChunkManager = true;

        /// subscribe to async chunk updates
        World.EventSystem.subscribe(
          this,
          EventSystems.WorldEventSystem.Channels.ChunkActivationUpdates
        );

        /// start the manager job in a seperate thread
        chunkApertureManagerThread = new System.Threading.Thread(() => ManageLoadedChunks()) {
          Name = "Level Aperture Queue Manager"
        };
        initilizePlayerFocus(initialFocus);
        chunkApertureManagerThread.Start();
        isLoaded = true;
      }
    }

    /// <summary>
    /// Spawn a new player in and initialize their level focus
    /// </summary>
    /// <param name="newFocus"></param>
    void initilizePlayerFocus(ILevelFocus newFocus) {
      IFocusLens newLens = level.addPlayerFocus(newFocus);
      int requiredControllerCount = newLens.initialize();

      // create the nodes the new lens will need to render
      for (int i = 0; i < requiredControllerCount; i++) {
        ChunkController chunkController = Instantiate(ChunkPrefab).GetComponent<ChunkController>();
        chunkController.gameObject.SetActive(false);
        chunkController.gameObject.name = $"Chunk #{++currentMaxChunkObjectID}#";
        chunkController.initalize(this);
        chunkController.transform.SetParent(transform);
        freeChunkControllerPool.Enqueue(chunkController);
      }

      // activate the new focus so it's being tracked
      newFocus.activate();
    }

    #endregion

    #region Game Loop

    void Update() {
      if (!isLoaded) {
        return;
      }

      /// try to assign newly mehsed chunks that are waiting on controllers, if we run out.
      if (chunksWaitingForAFreeController.tryDequeue(out KeyValuePair<float, ChunkResolutionAperture.Adjustment> chunkMeshWaitingForController)) {
        if (tryToAssignMeshedChunkToController(chunkMeshWaitingForController.Value.chunkID, out ChunkController assingedController) && assingedController != null) {
          chunksToMesh.enqueue(level.getPriorityForAdjustment(chunkMeshWaitingForController.Value), assingedController);
#if DEBUG
          level.getChunk(chunkMeshWaitingForController.Value.chunkID).recordEvent($"added to chunksToMesh Level Manager queue");
#endif
        } else {
          chunksWaitingForAFreeController.enqueue(level.getPriorityForAdjustment(chunkMeshWaitingForController.Value), chunkMeshWaitingForController.Value);
        }
      }

      /// mesh the chunk that has a controller waiting
      if (chunksToMesh.tryDequeue(out KeyValuePair<float, ChunkController> meshedChunkLocation)) {
        if (meshedChunkLocation.Value.isActive) {
          meshedChunkLocation.Value.meshChunkWithCurrentData();
#if DEBUG
        } else {
          meshedChunkLocation.Value.recordEvent($"dropped from chunksToMesh queue, no longer set to any chunk");
#endif
        }
      }

      /// go through the chunk activation queue and activate chunks
      if (chunksToActivate.tryDequeue(out KeyValuePair<float, Coordinate> activatedChunkLocation)) {
        // if the chunk doesn't have a meshed and baked controller yet, we can't activate it, so wait.
        if (tryToGetAssignedChunkController(activatedChunkLocation.Value, out ChunkController assignedController)) {
          // is active and the mesh is baked
          if (assignedController.isActive && assignedController.isMeshed && assignedController.checkColliderIsBaked()) {
            assignedController.setVisible();
          } else {
            chunksToActivate.enqueue(activatedChunkLocation);
          }
        } else if (chunksWaitingForAFreeController.Count == 0) {
          Chunk chunk = level.getChunk(activatedChunkLocation.Value);
          if (!chunk.meshIsEmpty) {
            chunksToActivate.enqueue(activatedChunkLocation);
          } else {
            // set the chunk visible as, it is visible it just has no mesh
#if DEBUG
            chunk.recordEvent($"Chunk dropped from chunksToActivate queue for having an empty mesh");
#endif
            chunk.setVisible();
            chunk.unlock((Chunk.Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.InFocus));
          }
        } else {
          chunksToActivate.enqueue(activatedChunkLocation);
        }
      }

      /// go through the de-activation queue
      if (chunksToDeactivate.tryDequeue(out KeyValuePair<float, Coordinate> deactivatedChunkLocation)) {
        if (tryToGetAssignedChunkController(deactivatedChunkLocation.Value, out ChunkController assignedController)) {
          assignedController.setVisible(false);
          // if there's no controller found then it was never set active and we can just drop it too
        } else {
          Chunk chunkToSetInvisible = level.getChunk(deactivatedChunkLocation.Value);
#if DEBUG
          chunkToSetInvisible.recordEvent($"chunk dropped from chunksToDeactivate for never making it to active");
#endif
          chunkToSetInvisible.setVisible(false);
          chunkToSetInvisible.unlock((Chunk.Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.OutOfFocus));
        }
      }

      /// try to remove meshes for the given chunk and reset it's mesh data
      if (chunksToDemesh.tryDequeue(out KeyValuePair<float, ChunkController> chunkNodeToDemesh)) {
        chunkNodeToDemesh.Value.clearAssignedChunkData();
        usedChunkControllers.TryRemove(chunkNodeToDemesh.Value.chunkLocation, out _);
        freeChunkControllerPool.Enqueue(chunkNodeToDemesh.Value);
      }
    }

    #endregion

    #region Level Management Loop

    /// <summary>
    /// A loop to be run seperately to manage the lenses for this level.
    /// </summary>
    void ManageLoadedChunks() {
      while (runChunkManager) {
        level.forEachFocalLens((lens, focus) => {
          if (focus.isActive && focus.previousChunkID != focus.getUpdatedChunkID()) {
            lens.updateAdjustmentsForFocusMovement();
          }
        });
      }
    }

    /// <summary>
    /// Get notifications from other observers, EX:
    ///   block breaking and placing
    ///   player chunk location changes
    /// </summary>
    /// <param name="event">The event to notify this observer of</param>
    /// <param name="origin">(optional) the source of the event</param>
    public void notifyOf(IEvent @event) {
      // ignore events if we have no level to control
      if (level == null) {
        return;
      }

      switch (@event) {
        // when a chunk mesh comes into focus, or loads, set the mesh to a chunkManager
        case MeshGenerationAperture.ChunkMeshLoadingFinishedEvent cmfle:
          if (cmfle.adjustment.type == ChunkResolutionAperture.FocusAdjustmentType.Dirty) {
            if (tryToGetAssignedChunkController(cmfle.adjustment.chunkID, out ChunkController dirtyController)) {
              dirtyController.setChunkToMesh(cmfle.adjustment.chunkID, level.getChunk(cmfle.adjustment.chunkID));
              chunksToMesh.enqueue(0, dirtyController);
            }
          }
          if (tryToAssignMeshedChunkToController(cmfle.adjustment.chunkID, out ChunkController assignedController)) {
            if (assignedController != null) {
              chunksToMesh.enqueue(level.getPriorityForAdjustment(cmfle.adjustment), assignedController);
#if DEBUG
              level.getChunk(cmfle.adjustment.chunkID).recordEvent($"added to chunksToMesh Level Manager queue");
            } else {
              level.getChunk(cmfle.adjustment.chunkID).recordEvent($"NOT added to chunksToMesh Level Manager queue. No Controller was assigned.");
#endif
            }
          } else {
            chunksWaitingForAFreeController.enqueue(level.getPriorityForAdjustment(cmfle.adjustment), cmfle.adjustment);
#if DEBUG
            level.getChunk(cmfle.adjustment.chunkID).recordEvent($"added to chunksWaitingForAFreeController Level Manager queue");
#endif
          }
          break;
        // when the level finishes loading a chunk's mesh. Render it in world
        case ChunkVisibilityAperture.SetChunkVisibleEvent scae:
          chunksToActivate.enqueue(level.getPriorityForAdjustment(scae.adjustment), scae.adjustment.chunkID);
#if DEBUG
          level.getChunk(scae.adjustment.chunkID).recordEvent($"added to chunksToActivate Level Manager queue");
#endif
          break;
        case ChunkVisibilityAperture.SetChunkInvisibleEvent scie:
          chunksToDeactivate.enqueue(level.getPriorityForAdjustment(scie.adjustment), scie.adjustment.chunkID);
#if DEBUG
          level.getChunk(scie.adjustment.chunkID).recordEvent($"added to chunksToDeactivate Level Manager queue");
#endif
          break;
        case MeshGenerationAperture.RemoveChunkMeshEvent rcme:
          if (tryToGetAssignedChunkController(rcme.adjustment.chunkID, out ChunkController assignedChunkController)) {
            chunksToDemesh.enqueue(level.getPriorityForAdjustment(rcme.adjustment), assignedChunkController);
#if DEBUG
            level.getChunk(rcme.adjustment.chunkID).recordEvent($"added to chunksToDemesh Level Manager queue");
          } else {
            level.getChunk(rcme.adjustment.chunkID).recordEvent($"no chunk controller to demesh, dropping from queue");
#endif
          }
          break;
        default:
          return;
      }
    }

    /// <summary>
    /// stop the thread on game close
    /// </summary>
    void OnDestroy() {
      runChunkManager = false;
      if (chunkApertureManagerThread != null) {
        chunkApertureManagerThread.Abort();
      }
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Try to get a free controller and assign this chunk mesh to it
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    bool tryToAssignMeshedChunkToController(Coordinate chunkID, out ChunkController assignedController) {
      assignedController = null;
      Chunk chunk = level.getChunk(chunkID);
      if (!chunk.meshIsEmpty && chunk.currentResolution == Chunk.Resolution.Meshed) {
        if (freeChunkControllerPool.TryDequeue(out ChunkController freeController)) {
          freeController.setChunkToMesh(chunkID, chunk);
          usedChunkControllers.TryAdd(chunkID, freeController);
          assignedController = freeController;
          return true;
          // if there's no free controllers yet, return false
        } else {
          return false;
        }
      } else {
        /// if the mesh is empty, we can just mark it as visible without assigning it to a controller
        if (chunk.tryToLock((Chunk.Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.InFocus))) {
#if DEBUG
          chunk.recordEvent($"Generated mesh is empty, LevelManager dropping chunk waiting on controller.");
#endif
          chunk.setVisible(true);
          chunk.unlock((Chunk.Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.InFocus));
          return true;
        }

        // if we couldn't lock it to set it as visible, try again
        return false;
      }
    }

    /// <summary>
    /// Try to get the controller assigned to this chunk for meshing
    /// </summary>
    /// <param name="chunkID"></param>
    /// <param name="assignedController"></param>
    /// <returns></returns>
    bool tryToGetAssignedChunkController(Coordinate chunkID, out ChunkController assignedController) {
      return usedChunkControllers.TryGetValue(chunkID, out assignedController);
    }

    /// <summary>
    /// Try to get a chunk controller named with the given ID
    /// </summary>
    /// <param name="chunkControllerID"></param>
    /// <param name="chunkController"></param>
    /// <returns></returns>
    public bool tryToGetChunkControllerByID(int chunkControllerID, out ChunkController chunkController) {
      chunkController = null;

      /// check used chunks
      foreach (ChunkController controller in usedChunkControllers.Values) {
        if (controller.gameObject.name.Contains($"#{chunkControllerID}#")) {
          chunkController = controller;

          return true;
        }
      }

      /// check free chunks
      foreach (ChunkController controller in freeChunkControllerPool) {
        if (controller.gameObject.name.Contains($"#{chunkControllerID}#")) {
          chunkController = controller;

          return true;
        }
      }

      return false;
    }

    #endregion

#if UNITY_EDITOR
    #region Unity Custom Inspector
    [CustomEditor(typeof(LevelTerrainManager))]
    public class LevelDataTesterGUI : Editor {
      public override void OnInspectorGUI() {
        DrawDefaultInspector();

        LevelTerrainManager terrainManager = target as LevelTerrainManager;
        EditorGUILayout.LabelField("Lens Info:", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        if (terrainManager.level != null) {
          terrainManager.level.forEachFocalLens((lens, focus) => {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lens:", $"{lens.GetType().Name} focused on #{focus.id}");
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Running Jobs Per Aperture:");
            foreach ((int runningJobCount, string apertureName) in lens.getRunningJobCountPerAperture()) {
              EditorGUILayout.IntField(apertureName, runningJobCount);
            }
          });
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField("Manager Info:", EditorStyles.boldLabel);
        if (GUILayout.Button("Print Current Queue Counts")) {
          PrintQueueCounts(terrainManager);
        }
      }

      /// <summary>
      /// Print the queue counts for a terrain manager
      /// </summary>
      /// <param name="terrainManager"></param>
      public static void PrintQueueCounts(LevelTerrainManager terrainManager) {
        World.Debug.log($"Current Queue Counts:\n ===== \n"
          + $"\tChunks Waiting For A Free Controller: {terrainManager.chunksWaitingForAFreeController.Count}"
          + $"\tChunks To Mesh: {terrainManager.chunksToMesh.Count}"
          + $"\tChunks To Activate: {terrainManager.chunksToActivate.Count}"
          + $"\tChunks To Deactivate: {terrainManager.chunksToDeactivate.Count}"
          + $"\tChunks To Demesh: {terrainManager.chunksToDemesh.Count}"
        );
      }
    }
    #endregion
#endif
  }
}