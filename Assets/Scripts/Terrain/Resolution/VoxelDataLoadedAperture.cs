using Evix.Terrain.Collections;
using Evix.Terrain.DataGeneration;
using Evix.Terrain.MeshGeneration;

namespace Evix.Terrain.Resolution {
  class VoxelDataLoadedAperture : ChunkResolutionAperture {
    public VoxelDataLoadedAperture(IFocusLens lens, int managedChunkRadius, int managedChunkHeight = 0) 
      : base(Chunk.Resolution.Loaded, lens, managedChunkRadius, managedChunkHeight) {
    }

    #region Aperture Functions

    /// <summary>
    /// Make sure it's not already loaded how we want. IF it is we can just drop it
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="chunk"></param>
    /// <returns></returns>
    protected override bool isValidAndReady(Adjustment adjustment, Chunk chunk) {
      if ((adjustment.type == FocusAdjustmentType.InFocus && chunk.currentResolution == Chunk.Resolution.UnLoaded)
        || (adjustment.type == FocusAdjustmentType.OutOfFocus && chunk.currentResolution != Chunk.Resolution.UnLoaded)) {
        return true;
      } else {
#if DEBUG
        chunk.recordEvent($"dropped from voxel load queue, already at requested load level {chunk.currentResolution}");
#endif
        return false;
      }
    }

    /// <summary>
    /// Get the job. There's 3 types. Load from file, from noise, and save to file
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected override ApetureJobHandle getJob(Adjustment adjustment) {
      IAdjustmentJob job;
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        if (LevelDAO.ChunkFileExists(adjustment.chunkID, lens.level)) {
          job = new LevelDAO.LoadChunkDataFromFileJob(adjustment, lens.level);
          // if there's no file, we need to generate the chunk data from scratch
        } else {
          job = StaticBiomeMap.GetTerrainGenerationJob(adjustment, lens.level);
        }
        /// if it's out of focus, we want to save the chunk to file
      } else {
        Chunk chunkToSave = lens.level.getChunk(adjustment.chunkID);
        if (chunkToSave.currentResolution != Chunk.Resolution.UnLoaded) {
          job = new LevelDAO.SaveChunkDataToFileJob(adjustment, lens.level);
        } else throw new System.MissingMemberException(
          $"VoxelDataAperture is trying to save chunk data for {adjustment.chunkID} but could not find the chunk data in the level"
        );
      }

      return new ApetureJobHandle(job, onJobComplete);
    }

    /// <summary>
    /// When finished, alert the neighbors too
    /// </summary>
    /// <param name="job"></param>
    public override void onJobComplete(IAdjustmentJob job) {
      /// since neighbors may need this chunk to load, check if this one loading makes them ready to mesh:
      /// TODO: check if ForEachDirtiedNeighbor is the right set of neighbors, we may be able to use less
      if (job.adjustment.type == FocusAdjustmentType.InFocus
        && lens.tryToGetAperture(resolution + 1, out IChunkResolutionAperture nextApetureInLine)
      ) {
        MarchingTetsMeshGenerator.ForEachDirtiedNeighbor(job.adjustment.chunkID, lens.level, chunk => {
          if (chunk.currentResolution == Chunk.Resolution.Loaded) {
#if DEBUG
            lens.level.getChunk(chunk.id).recordEvent($"Attempting to get apeture job for {(Chunk.Resolution.Meshed, job.adjustment.type)} from neighbor");
#endif
            if (nextApetureInLine.tryToGetAdjustmentJobHandle(
              new Adjustment(
                chunk.id,
                job.adjustment.type,
                job.adjustment.resolution + 1,
                job.adjustment.focusID
              ),
              out ApetureJobHandle jobHandle
            )) {
              jobHandle.schedule();
#if DEBUG
              lens.incrementRunningJobCount(job.adjustment.resolution + 1);
#endif
            }
          }
        });
      }

      base.onJobComplete(job);
    }
  }

  #endregion
}
