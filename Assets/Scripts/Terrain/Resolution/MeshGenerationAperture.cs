using Evix.Events;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;

namespace Evix.Terrain.Resolution {
  public class MeshGenerationAperture : ChunkResolutionAperture {
    public MeshGenerationAperture(IFocusLens lens, int managedChunkRadius, int managedChunkHeight = 0)
      : base(Chunk.Resolution.Meshed, lens, managedChunkRadius, managedChunkHeight) {
    }

    #region ApertureFunctions

    protected override bool isValidAndReady(Adjustment adjustment, Chunk chunk) {
      switch (adjustment.type) {
        /// for dirty chunks we can just go for it
        case FocusAdjustmentType.Dirty:
          return true;
        case FocusAdjustmentType.InFocus:
          /// we can only mesh a newly in focus chunk if it's at the loaded resolution.
          if (chunk.currentResolution == Chunk.Resolution.Loaded) {
            /// if the chunk is solid and all neighbors covering it are also solid, we'll get no mesh, so we can skip it
            if (chunk.isSolid) {
              bool allBlockingNeighborsAreSolid = true;
              MarchingTetsMeshGenerator.ForEachRequiredNeighbor(adjustment.chunkID, lens.level, (neigborID, neighborChunk) => {
                // If the chunk is in out of bounds, it can't be solid
                // if it's in bounds, check if it's loaded yet and isn't solid
                if (!neigborID.isWithin(Coordinate.Zero, lens.level.chunkBounds)
                  || (neighborChunk.currentResolution >= Chunk.Resolution.Loaded
                  && !neighborChunk.isSolid)
                ) {
                  allBlockingNeighborsAreSolid = false;
                  return false;
                }

                return true;
              });

              /// set mesh as empty if we don't need to load it
              if (allBlockingNeighborsAreSolid) {
                if (chunk.adjustmentLockType == (Chunk.Resolution.Meshed, FocusAdjustmentType.InFocus)) {
#if DEBUG
                  chunk.recordEvent($"Chunk can skip MeshGenerationAperture queue, solid chunk is hidden. Setting empty mesh");
#endif
                  chunk.setMesh(default);
                  return false;
                } else World.Debug.logAndThrowError<System.AccessViolationException>($"Trying to make a change inside aperture {GetType().Name} with adjustment {adjustment} on chunk {chunk.id} with an incorrect lock: {chunk.adjustmentLockType}");
              }
            }

            /// alternitively if it's loaded and empty, check if the chunks that require this one for rendering are also empty.
            // if they are we can ignore this chunk for meshing
            if (chunk.isEmpty) {
              bool neighborsThatRequireThisChunkAreEmpty = true;
              MarchingTetsMeshGenerator.ForEachRequiredNeighbor(adjustment.chunkID, lens.level, (neigborID, neighborChunk) => {
                // if the chunk is out of bounds it can't be empty
                if (neigborID.isWithin(Coordinate.Zero, lens.level.chunkBounds)
                  // if it's in bounds and loaded, check if it's empty. If it's not we flag it!
                  && neighborChunk.currentResolution >= Chunk.Resolution.Loaded
                  && !neighborChunk.isEmpty
                ) {
                  neighborsThatRequireThisChunkAreEmpty = false;
                  return false;
                }

                return true;
              });

              /// set mesh as empty if we don't need to load it
              if (neighborsThatRequireThisChunkAreEmpty) {
                if (chunk.adjustmentLockType == (Chunk.Resolution.Meshed, FocusAdjustmentType.InFocus)) {
#if DEBUG
                  chunk.recordEvent($"Chunk can skip MeshGenerationAperture queue, chunk is empty and has no dependent neighbors. Setting empty mesh");
#endif
                  chunk.setMesh(default);
                  return false;
                } else World.Debug.logAndThrowError<System.AccessViolationException>($"Trying to make a change inside aperture {GetType().Name} with adjustment {adjustment} on chunk {chunk.id} with an incorrect lock: {chunk.adjustmentLockType}");
              }
            }

            /// if the chunk is loaded and passed the solid and empty tests, it's valid
            return true;
          } else {
            /// if the chunk is already meshed we can drop it.
            if (chunk.currentResolution >= Chunk.Resolution.Meshed) {
#if DEBUG
              chunk.recordEvent($"Chunk invalid for in focus MeshGenerationAperture queue, already at {chunk.currentResolution} resolution");
#endif
              return false;
            /// if the chunk isn't loaded yet we can drop it
            } else { 
#if DEBUG
              chunk.recordEvent($"Chunk invalid for in focus MeshGenerationAperture queue, not at Loaded resolution yet.");
#endif
              return false;
            }
          }
        case FocusAdjustmentType.OutOfFocus:
          /// if the resolution is already less than meshed, we can drop it
          if (chunk.currentResolution < Chunk.Resolution.Meshed) {
#if DEBUG
            chunk.recordEvent($"Chunk invalid for out of focus MeshGenerationAperture job, chunk is already below meshed resolution at {chunk.currentResolution}");
#endif
            return false;
          }

          return true;
        default:
          return false;
      }
    }

    /// <summary>
    /// Get the job to run
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected override ApetureJobHandle getJob(Adjustment adjustment) {
      IAdjustmentJob job;
      if (adjustment.type == FocusAdjustmentType.InFocus || adjustment.type == FocusAdjustmentType.Dirty) {
        job = MarchingTetsMeshGenerator.GetJob(adjustment, lens.level);
      } else {
        job = new DemeshChunkObjectJob(adjustment, lens.level);
      }

      return new ApetureJobHandle(job, onJobComplete);
    }

    #endregion

    #region Jobs

    /// <summary>
    /// A job for notifying the main thread to de mesh the controller for this chunk
    /// </summary>
    public struct DemeshChunkObjectJob : IAdjustmentJob {

      /// <summary>
      /// The chunk id we're updating to active
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      /// <summary>
      /// The level we're working on
      /// </summary>
      Level level;

      public DemeshChunkObjectJob(Adjustment adjustment, Level level) {
        this.adjustment = adjustment;
        this.level = level;
      }

      /// <summary>
      /// notify the chunk activaton channel that we want this chunk active
      /// </summary>
      public void doWork() {
        Chunk chunk = level.getChunk(adjustment.chunkID);
        chunk.clearMesh();
        chunk.unlock((Chunk.Resolution.Meshed, FocusAdjustmentType.OutOfFocus));

#if DEBUG
        chunk.recordEvent($"Notifying level manager of cleared mesh");
#endif
        World.EventSystem.notifyChannelOf(
          new RemoveChunkMeshEvent(adjustment),
          EventSystems.WorldEventSystem.Channels.ChunkActivationUpdates
        );
      }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event notifying the level controller that a chunk mesh is ready
    /// </summary>
    public struct ChunkMeshLoadingFinishedEvent : IEvent {

      public string name {
        get;
      }

      /// <summary>
      /// The chunk id we're updating to active
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      /// <summary>
      /// The chunk mesh made from the job
      /// </summary>
      public ChunkMeshData generatedChunkMesh {
        get;
      }

      public ChunkMeshLoadingFinishedEvent(Adjustment adjustment, ChunkMeshData generatedChunkMesh) {
        this.adjustment = adjustment;
        this.generatedChunkMesh = generatedChunkMesh;
        name = $"Chunk mesh finished generating for {adjustment.chunkID}";
      }
    }

    /// <summary>
    /// An event to notify the level controller to set a chunk inactive
    /// </summary>
    public struct RemoveChunkMeshEvent : IEvent {

      /// <summary>
      /// The name of the event
      /// </summary>
      public string name {
        get;
      }

      /// <summary>
      /// The chunk id we're updating to active
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      public RemoveChunkMeshEvent(Adjustment adjustment) {
        this.adjustment = adjustment;
        name = $"Setting chunk active: {adjustment.chunkID}";
      }
    }

    #endregion
  }
}