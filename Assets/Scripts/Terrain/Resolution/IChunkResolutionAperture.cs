﻿using Evix.Terrain.Collections;

namespace Evix.Terrain.Resolution {

  /// <summary>
  /// An aperture that manages the resolution of chunks within it's area
  /// </summary>
  public interface IChunkResolutionAperture {

    /// <summary>
    /// The resolution this apeture loads chuks to
    /// </summary>
    Chunk.Resolution resolution {
      get;
    }

    /// <summary>
    /// The managed chunk area radius, X and Z. Height may be different.
    /// </summary>
    int managedChunkRadius {
      get;
    }

    /// <summary>
    /// The managed chunk area height
    /// </summary>
    int managedChunkHeightRadius {
      get;
    }

    /// <summary>
    /// Initialize this aperture for the given focus. Should set up managed bounds
    /// </summary>
    /// <param name="focus"></param>
    void initializeBounds(ILevelFocus focus);

    /// <summary>
    /// Try to get highest priority adjustment in this apeture's update queue, if it's valid
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    bool tryToGetAdjustmentJobHandle(ChunkResolutionAperture.Adjustment adjustment, out ChunkResolutionAperture.ApetureJobHandle jobHandle);

    /// <summary>
    /// Do work on the completion of a given job
    /// </summary>
    /// <param name="job"></param>
    void onJobComplete(ChunkResolutionAperture.IAdjustmentJob job);

    /// <summary>
    /// Get all adjustments for a newly initialized focus
    /// </summary>
    /// <param name="newFocalPoint"></param>
    /// <returns></returns>
    int updateAdjustmentsForFocusInitilization(ILevelFocus newFocalPoint);

    /// <summary>
    /// Get al the adjustments for a focus changing locations
    /// </summary>
    /// <param name="focus"></param>
    /// <returns></returns>
    void updateAdjustmentsForFocusLocationChange(ILevelFocus focus);

    /// <summary>
    /// Get the priority for an adjustment on the given focus.
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="focus"></param>
    /// <returns></returns>
    int getPriority(ChunkResolutionAperture.Adjustment adjustment, ILevelFocus focus);
  }
}
