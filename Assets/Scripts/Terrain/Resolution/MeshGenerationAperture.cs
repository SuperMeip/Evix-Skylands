using Evix.Events;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;

namespace Evix.Terrain.Resolution {
  public class MeshGenerationAperture : ChunkResolutionAperture {
    public MeshGenerationAperture(IFocusLens lens, int managedChunkRadius, int managedChunkHeight = 0)
      : base(Chunk.Resolution.Meshed, lens, managedChunkRadius, managedChunkHeight) {
    }

    #region ApertureFunctions

    /// <summary>
    /// Get the job to run
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected override IAdjustmentJob getJob(Adjustment adjustment) {
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        return MarchingTetsMeshGenerator.GetJob(adjustment, lens.level);
      } else {
        return new DemeshChunkObjectJob(adjustment, lens.level);
      }
    }

    /// <summary>
    /// Validate this chunk for meshing/de-meshing
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="chunk"></param>
    /// <returns></returns>
    internal override bool isValid(Adjustment adjustment, out Chunk chunk) {
      if (base.isValid(adjustment, out chunk)) {
        if (adjustment.type == FocusAdjustmentType.InFocus) {
          // if it's already meshed, we can drop it from the job queue
          if (chunk.currentResolution == Chunk.Resolution.Meshed && adjustment.resolution == Chunk.Resolution.Meshed) {
            chunk.recordEvent($"Chunk invalid for MeshGenerationAperture queue, already at {chunk.currentResolution} resolution");
            return false;
          }

          if (chunk.currentResolution == Chunk.Resolution.Loaded) {
            /// if it's loaded and solid, check if all the chunks that block it are solid. If they are we can ignore this chunk
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
              if (allBlockingNeighborsAreSolid && chunk.tryToLock(Chunk.Resolution.Meshed)) {
                chunk.recordEvent($"Chunk invalid for MeshGenerationAperture queue, solid chunk is hidden");
                chunk.setMesh(default);
                chunk.unlock(Chunk.Resolution.Meshed);

                return false;
              }
            }

            /// if it's loaded and empty, check if the chunks that require this one for rendering are also empty.
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
              if (neighborsThatRequireThisChunkAreEmpty && chunk.tryToLock(Chunk.Resolution.Meshed)) {
                chunk.recordEvent($"Chunk invalid for MeshGenerationAperture queue, chunk is empty and has no dependent neighbors");
                chunk.setMesh(default);
                chunk.unlock(Chunk.Resolution.Meshed);

                return false;
              }
            }
          }

          /// if the chunk is loaded and passed the solid and empty tests, it's valid
          return true;
          /// if this chunk is going out of focus and wasn't loaded to meshed level, we can just drop it.  
        } else if (chunk.currentResolution < Chunk.Resolution.Meshed) {
          chunk.recordEvent($"Chunk invalid for out of focus MeshGenerationAperture queue, chunk is already below meshed resolution at {chunk.currentResolution}");
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Check if it's ready to mesh.
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="chunk"></param>
    /// <returns></returns>
    protected override bool isReady(Adjustment adjustment, Chunk validChunk) {
      /// if a valid chunk is going out of focus, it should be ready to go
      if (adjustment.type == FocusAdjustmentType.OutOfFocus) {
        return true;
      }

      /// if the chunk has it's data loaded, lets check it's nessisary neighbors
      if (validChunk.currentResolution >= Chunk.Resolution.Loaded) {
        bool necessaryNeighborsAreLoaded = true;
        bool blockingNeighborsAreSolid = validChunk.isSolid;
        MarchingTetsMeshGenerator.ForEachRequiredNeighbor(adjustment.chunkID, lens.level, (neighborID, neighborChunk) => {
          // check if they're loaded. out of bounds chunks will never be loaded and will always be empty
          bool neighborIsWithinLevelBounds = neighborID.isWithin(Coordinate.Zero, lens.level.chunkBounds);
          if (neighborIsWithinLevelBounds && neighborChunk.currentResolution < Chunk.Resolution.Loaded) {
            necessaryNeighborsAreLoaded = false;
            if (!blockingNeighborsAreSolid) {
              return false;
            }
          }

          // if this chunk is solid, check if the neighbors are solid
          if (!neighborIsWithinLevelBounds
           || (validChunk.isSolid
            && neighborChunk.currentResolution >= Chunk.Resolution.Loaded
            && !neighborChunk.isSolid)
          ) {
            blockingNeighborsAreSolid = false;
            if (!necessaryNeighborsAreLoaded) {
              return false;
            }
          }

          return true;
        });

        // if all the neighbors are loaded, and this and all the neighbors arn't solid, it's ready to go.
        return necessaryNeighborsAreLoaded && !blockingNeighborsAreSolid;
      }

      return false;
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
        chunk.unlock(Chunk.Resolution.Meshed);

        chunk.recordEvent($"Notifying level manager of cleared mesh");
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