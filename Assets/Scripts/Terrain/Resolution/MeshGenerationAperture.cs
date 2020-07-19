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
            bool necessaryNeighborsAreLoaded = true;
            bool necessaryNeighborsAreEmpty = chunk.isEmpty;
            bool blockingNeighborsAreSolid = chunk.isSolid;
            MarchingTetsMeshGenerator.ForEachRequiredNeighbor(adjustment.chunkID, lens.level, (neighborID, neighborChunk) => {
              bool neighborIsWithinLevelBounds = neighborID.isWithin(Coordinate.Zero, lens.level.chunkBounds);
              // check if they're loaded. out of bounds chunks will never be loaded and can be skipped 
              if (neighborIsWithinLevelBounds && neighborChunk.currentResolution < Chunk.Resolution.Loaded) {
                necessaryNeighborsAreLoaded = false;
                return false;
              }

              // out of bounds chunks will never be loaded and will always be empty, we only need to check in bounds chunks for empty
              if (neighborIsWithinLevelBounds && !neighborChunk.isEmpty) {
                necessaryNeighborsAreEmpty = false;
              }

              // only in bounds chunks can be solid, so if it's out of bounds or not solid, we mark it so.
              if (!neighborIsWithinLevelBounds || !neighborChunk.isSolid) {
                blockingNeighborsAreSolid = false;
              }

              return true;
            });

            if (!necessaryNeighborsAreLoaded) {
#if DEBUG
              chunk.recordEvent($"Chunk is not ready for MeshGenerationAperture job, Necessary neighbors not loaded yet.");
#endif
              return false;
            }

            /// we don't need to load the mesh if it and it's neighbors are all solid or empty
            if (blockingNeighborsAreSolid || necessaryNeighborsAreEmpty) {
              /// set mesh as empty
              if (chunk.adjustmentLockType == (Chunk.Resolution.Meshed, FocusAdjustmentType.InFocus)) {
#if DEBUG
                chunk.recordEvent($"Chunk can skip MeshGenerationAperture queue, {(blockingNeighborsAreSolid ? "solid chunk is hidden" : "required neighbors are all empty")}. Setting empty mesh");
#endif
                chunk.setMesh(default);
                return false;
              } else World.Debug.logAndThrowError<System.AccessViolationException>($"Trying to make a change inside aperture {GetType().Name} with adjustment {adjustment} on chunk {chunk.id} with an incorrect lock: {chunk.adjustmentLockType}");
            }

            /// if the chunk is loaded and passed the solid and empty tests, and has it's neighbors loaded, it's valid
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
          /// if the resolution is already less than meshed, we can drop it, or if it's still visible it needs to be turned off first
          if (chunk.currentResolution != Chunk.Resolution.Meshed) {
#if DEBUG
            chunk.recordEvent($"Chunk invalid for out of focus MeshGenerationAperture job, not at correct resolution MESHED, at resolution: {chunk.currentResolution}");
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