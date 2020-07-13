using System;
using Evix.Controllers;
using Evix.Events;
using Evix.Terrain.Collections;
using Evix.Terrain.Resolution;
using UnityEngine;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Evix.Managers {

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
        /// subscribe to async chunk updates
        World.EventSystem.subscribe(
          this,
          EventSystems.WorldEventSystem.Channels.ChunkActivationUpdates
        );

        ///  init the level
        this.level = level;
        initilizePlayerFocus(initialFocus);

        /// start the manager job in a seperate thread
        chunkApertureManagerThread = new System.Threading.Thread(() => ManageLoadedChunks()) {
          Name = "Level Aperture Queue Manager"
        };
        chunkApertureManagerThread.Start();
        isLoaded = true;
        runChunkManager = true;
      }
    }

    /// <summary>
    /// Spawn a new player in and initialize their level focus
    /// </summary>
    /// <param name="newFocus"></param>
    void initilizePlayerFocus(ILevelFocus newFocus) {
      IFocusLens newLens = level.addPlayerFocus(newFocus);
      int requiredNewNodeCount = newLens.initialize();

      // create the nodes the new lens will need to render
      for (int i = 0; i < requiredNewNodeCount; i++) {
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
          level.getChunk(chunkMeshWaitingForController.Value.chunkID).recordEvent($"added to chunksToMesh Level Manager queue");
        } else {
          chunksWaitingForAFreeController.enqueue(level.getPriorityForAdjustment(chunkMeshWaitingForController.Value), chunkMeshWaitingForController.Value);
        }
      }

      /// mesh the chunk that has a controller waiting
      if (chunksToMesh.tryDequeue(out KeyValuePair<float, ChunkController> meshedChunkLocation)) {
        if (meshedChunkLocation.Value.isActive) {
          meshedChunkLocation.Value.meshChunkWithCurrentData();
        } else {
          meshedChunkLocation.Value.recordEvent($"dropped from chunksToMesh queue, no longer set to any chunk");
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
          }
          chunk.recordEvent($"Chunk dropped from chunksToActivate queue for having an empty mesh");
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
          level.getChunk(deactivatedChunkLocation.Value).recordEvent($"chunk dropped from chunksToDeactivate for never making it to active");
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
          lens.scheduleNextChunkAdjustment();
          lens.handleFinishedJobs();
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
      if (!isLoaded || level == null) {
        return;
      }

      switch (@event) {
        // when a chunk mesh comes into focus, or loads, set the mesh to a chunkManager
        case MeshGenerationAperture.ChunkMeshLoadingFinishedEvent cmfle:
          if (tryToAssignMeshedChunkToController(cmfle.adjustment.chunkID, out ChunkController assignedController) && assignedController != null) {
            chunksToMesh.enqueue(level.getPriorityForAdjustment(cmfle.adjustment), assignedController);
            level.getChunk(cmfle.adjustment.chunkID).recordEvent($"added to chunksToMesh Level Manager queue");
          } else {
            chunksWaitingForAFreeController.enqueue(level.getPriorityForAdjustment(cmfle.adjustment), cmfle.adjustment);
            level.getChunk(cmfle.adjustment.chunkID).recordEvent($"added to chunksWaitingForAFreeController Level Manager queue");
          }
          break;
        // when the level finishes loading a chunk's mesh. Render it in world
        case ChunkVisibilityAperture.SetChunkVisibleEvent scae:
          chunksToActivate.enqueue(level.getPriorityForAdjustment(scae.adjustment), scae.adjustment.chunkID);
          level.getChunk(scae.adjustment.chunkID).recordEvent($"added to chunksToActivate Level Manager queue");
          break;
        case ChunkVisibilityAperture.SetChunkInvisibleEvent scie:
          chunksToDeactivate.enqueue(level.getPriorityForAdjustment(scie.adjustment), scie.adjustment.chunkID);
          level.getChunk(scie.adjustment.chunkID).recordEvent($"added to chunksToDeactivate Level Manager queue");
          break;
        case MeshGenerationAperture.RemoveChunkMeshEvent rcme:
          if (tryToGetAssignedChunkController(rcme.adjustment.chunkID, out ChunkController assignedChunkController)) {
            chunksToDemesh.enqueue(level.getPriorityForAdjustment(rcme.adjustment), assignedChunkController);
            level.getChunk(rcme.adjustment.chunkID).recordEvent($"added to chunksToDemesh Level Manager queue");
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
      if (!chunk.meshIsEmpty) {
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
          /// if the mesh is empty, we can just assing it as visibl without assigning it to a controller
        if (chunk.tryToLock(Chunk.Resolution.Visible)) {
          chunk.setVisible(true);
          chunk.unlock(Chunk.Resolution.Visible);
          chunk.recordEvent($"generated mesh is empty, LevelManager dropping chunk");
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
      foreach(ChunkController controller in usedChunkControllers.Values) {
        if (controller.gameObject.name.Contains($"#{chunkControllerID}#")) {
          chunkController = controller;

          return true;
        }
      }

      /// check free chunks
      foreach(ChunkController controller in freeChunkControllerPool) {
        if (controller.gameObject.name.Contains($"#{chunkControllerID}#")) {
          chunkController = controller;

          return true;
        }
      }

      return false;
    }

    #endregion
  }
}