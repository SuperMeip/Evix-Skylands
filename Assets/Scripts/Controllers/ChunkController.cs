using Evix.Events;
using Evix.Managers;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;
using Evix.Testing;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

namespace Evix.Controllers {

  /// <summary>
  /// Controls a chunk in world
  /// </summary>
  [RequireComponent(typeof(MeshCollider))]
  [RequireComponent(typeof(MeshRenderer))]
  [RequireComponent(typeof(MeshFilter))]
  public class ChunkController : MonoBehaviour, IRecorded {

    /// <summary>
    /// The manager that manages this chunk
    /// </summary>
    public LevelTerrainManager terrainManager {
      get;
      private set;
    }

    /// <summary>
    /// The current chunk location of the chunk this gameobject is representing.
    /// </summary>
    public Coordinate chunkLocation {
      get;
      private set;
    }

    /// <summary>
    /// If this controller is being used.
    /// </summary>
    public bool isActive {
      get;
      private set;
    } = false;

    /// <summary>
    /// If this chunk has been meshed with chunk data.
    /// </summary>
    public bool isMeshed {
      get;
      private set;
    } = false;

    /// <summary>
    /// The current generated mesh
    /// </summary>
    Chunk chunkData;

    /// <summary>
    /// The collider for this chunk
    /// </summary>
    MeshCollider meshCollider;

    /// <summary>
    /// The mesh renderer for this chunk
    /// </summary>
    MeshFilter meshFilter;

    /// <summary>
    /// The job handler for the collider mesh baking job
    /// </summary>
    JobHandle colliderBakerHandler;

    #region Initialization

    /// <summary>
    /// Initialize this chunk controller
    /// </summary>
    public void initalize(LevelTerrainManager terrainManager) {
      this.terrainManager = terrainManager;
      meshCollider = GetComponent<MeshCollider>();
      meshFilter = GetComponent<MeshFilter>();
      meshFilter.mesh = new Mesh();
      meshFilter.mesh.Clear();
      recordEvent($"Initialized as {gameObject.name}");
    }

    #endregion

    #region Chunk Control

    /// <summary>
    /// Set the chunk to render.
    /// </summary>
    public void setChunkToMesh(Coordinate chunkID, Chunk chunk) {
      isActive = true;
      chunkData = chunk;
      chunkLocation = chunkID;
      chunk.recordEvent($"chunk mesh data assigned to a controller");
      recordEvent($"Set chunk data with {chunk.meshData.triangleCount} tris for chunk {chunkID}");
    }

    /// <summary>
    /// Set up the mesh
    /// This can only be called in the main thread
    /// </summary>
    public void meshChunkWithCurrentData() {
      if (chunkData == null || chunkData.meshIsEmpty) {
        recordEvent($"ChunkData is missing for chunk ID {chunkLocation} on chunk object: {gameObject.name}");
        return;
      } else {
        meshFilter.mesh.SetVertices(chunkData.meshData.vertices);
        meshFilter.mesh.SetColors(chunkData.meshData.colors);
        meshFilter.mesh.SetTriangles(chunkData.meshData.triangles, 0);
        meshFilter.mesh.RecalculateNormals();

        transform.position = chunkLocation.vec3 * Chunk.Diameter;
        meshCollider.sharedMesh = meshFilter.mesh;
        isMeshed = true;

        /// schedule a job to bake the mesh collider asyncly so it doesn't lag.
        colliderBakerHandler = (new ColliderMeshBakingJob(meshFilter.mesh.GetInstanceID())).Schedule();
        chunkData.recordEvent($"Chunkcontroller has set data on mesh filter with {chunkData.meshData.triangleCount} tris");
        recordEvent($"set data on mesh filter for chunk {chunkLocation} with {chunkData.meshData.triangleCount} tris");
      }
    }

    /// <summary>
    /// Set the active state of this chunk.
    /// This can only be called in the main thread
    /// </summary>
    /// <param name="activeState"></param>
    public void setVisible(bool activeState = true) {
      if (activeState) {
        gameObject.SetActive(true);
        chunkData.setVisible();
        chunkData.unlock(Chunk.Resolution.Visible);
        recordEvent($"setting chunk visible");
      } else {
        gameObject.SetActive(false);
        chunkData.setVisible(false);
        chunkData.unlock(Chunk.Resolution.Visible);
        recordEvent($"hiding chunk");
      }
    }

    /// <summary>
    /// deactivate and free up this object for use again by the level controller
    /// This can only be called in the main thread
    /// </summary>
    public void clearAssignedChunkData() {
      isActive = false;
      recordEvent($"clearing data for chunk: {chunkData?.id.ToString() ?? "NONE"}");
      if (chunkData != null) {
        chunkData.recordEvent($"clearing chunkcontroller data");
      }
      chunkLocation = default;
      meshFilter.mesh.Clear();
      meshCollider.sharedMesh = null;
      isMeshed = false;
      chunkData = null;
    }

    /// <summary>
    /// Check if the collider was baked by a job for this chunk
    /// </summary>
    /// <returns></returns>
    public bool checkColliderIsBaked() {
      if (colliderBakerHandler.IsCompleted) {
        recordEvent($"collider finished bakeing for {chunkLocation}");
        return true;
      }

      return false;
    }

    #endregion

    #region IRecorded Interface

    /// <summary>
    /// A history of events recorded by this chunk
    /// </summary>
    readonly List<(string, string)> eventHistory
      = new List<(string, string)>();

    /// <summary>
    /// add an event to the history
    /// </summary>
    /// <param name="event"></param>
    public void recordEvent(string @event) {
      eventHistory.Add((System.DateTime.Now.ToString("HH: mm:ss.ff"), @event));
    }

    /// <summary>
    /// Get the last X recrded events (or all of them)
    /// </summary>
    /// <param name="lastX"></param>
    /// <returns></returns>
    public (string timestamp, string @event)[] getRecordedEvents(int? lastX = null) {
      return lastX == null
        ? eventHistory.ToArray()
        : eventHistory.GetRange(eventHistory.Count - (int)lastX, (int)lastX).ToArray();
    }

    #endregion
  }

  #region Jobs

  /// <summary>
  /// A unity job to bake the collider mesh
  /// </summary>
  struct ColliderMeshBakingJob : IJob {

    /// <summary>
    /// The id of the mesh to bake
    /// </summary>
    readonly int meshID;

    /// <summary>
    /// Create a new mesh baking job for this controller
    /// </summary>
    /// <param name="meshID"></param>
    /// <param name="chunkController"></param>
    public ColliderMeshBakingJob(int meshID) {
      this.meshID = meshID;
    }

    /// <summary>
    /// Execute the job and bake the mesh
    /// </summary>
    public void Execute() {
      Physics.BakeMesh(meshID, false);
    }
  }

  #endregion

  #region Unity Inspector Additions

#if UNITY_EDITOR
  /// <summary>
  /// Show off the chunk ID
  /// </summary>
  [CustomEditor(typeof(ChunkController))]
  class FocusCustomInspoector : Editor {
    public override void OnInspectorGUI() {
      /// Just info about the chunk
      EditorGUILayout.LabelField("Chunk Info:");
      EditorGUI.BeginDisabledGroup(true);
      ChunkController chunkController = target as ChunkController;
      EditorGUILayout.Vector3Field("Current Controlled Chunk ID", chunkController.chunkLocation.vec3);
      EditorGUILayout.Toggle("Is Active", chunkController.isActive);
      EditorGUILayout.Toggle("Is Meshed", chunkController.isMeshed);
      EditorGUI.EndDisabledGroup();

      if (GUILayout.Button("Print Chunk Controller Edit History Log")) {
        LevelDataTester.PrintChunkControllerRecords(
          int.Parse(chunkController.gameObject.name.Split('#')[1]),
          chunkController.terrainManager
        );
      }

      if (GUILayout.Button("Print Current Chunk Edit History Log")) {
        LevelDataTester.PrintChunkDataRecords(chunkController.chunkLocation);
      }
      DrawDefaultInspector();
    }
  }
#endif

  #endregion
}