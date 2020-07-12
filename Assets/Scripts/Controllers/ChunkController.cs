using Evix.Managers;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;
using Unity.Jobs;
using UnityEngine;

namespace Evix.Controllers {

  /// <summary>
  /// Controls a chunk in world
  /// </summary>
  [RequireComponent(typeof(MeshCollider))]
  [RequireComponent(typeof(MeshRenderer))]
  [RequireComponent(typeof(MeshFilter))]
  public class ChunkController : MonoBehaviour {

    /// <summary>
    /// The current chunk location of the chunk this gameobject is representing.
    /// </summary>
    [SerializeField]
    [ReadOnly]
    Coordinate chunkLocation;

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
    public void initalize() {
      meshCollider = GetComponent<MeshCollider>();
      meshFilter = GetComponent<MeshFilter>();
      meshFilter.mesh = new Mesh();
      meshFilter.mesh.Clear();
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
    }

    /// <summary>
    /// Set up the mesh
    /// This can only be called in the main thread
    /// </summary>
    public void meshChunkWithCurrentData() {
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
      } else {
        chunkData.setVisible(false);
        chunkData.unlock(Chunk.Resolution.Visible);
      }
    }

    /// <summary>
    /// deactivate and free up this object for use again by the level controller
    /// This can only be called in the main thread
    /// </summary>
    public void clearAssignedChunkData() {
      if (chunkData != null) {
        chunkData.recordEvent($"clearing chunkcontroller data");
      }
      chunkLocation = default;
      meshFilter.mesh.Clear();
      meshCollider.sharedMesh = null;
      isMeshed = false;
      isActive = false;
      chunkData = null;
    }

    /// <summary>
    /// Check if the collider was baked by a job for this chunk
    /// </summary>
    /// <returns></returns>
    public bool checkColliderIsBaked() {
      return colliderBakerHandler.IsCompleted;
    }

    #endregion
  }

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
}