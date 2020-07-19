using Evix.Events;
using Evix.Terrain.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.Resolution {

  /// <summary>
  /// A collection of layered Chunk resolution Apertures used to handle a level focus
  /// </summary>
  public interface IFocusLens : IObserver {

    /// <summary>
    /// The level this lens works for
    /// </summary>
    Level level {
      get;
    }

    /// <summary>
    /// Start the lens loading around it's focus
    /// </summary>
    int initialize();

    /// <summary>
    /// Update the adjustment queue with new adjustments based on focus movement.
    /// </summary>
    void updateAdjustmentsForFocusMovement();

    /// <summary>
    /// Get the calculated priority of an adjustment for this lens
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    float getAdjustmentPriority(ChunkResolutionAperture.Adjustment adjustment, ILevelFocus focus);

    /// <summary>
    /// Try to get an aperture that exsists in this lens by it's resolution
    /// </summary>
    /// <param name="resolution"></param>
    /// <param name="aperture"></param>
    /// <returns></returns>
    bool tryToGetAperture(Chunk.Resolution resolution, out IChunkResolutionAperture aperture);

#if DEBUG
    /// <summary>
    /// Count a running job for this len's apeture
    /// </summary>
    /// <param name="jobHandle"></param>
    void incrementRunningJobCount(Chunk.Resolution forApertureOfResolution);

    /// <summary>
    /// Removing a runing job from this len's aperture job count
    /// </summary>
    /// <param name="jobHandle"></param>
    void decrementRunningJobCount(Chunk.Resolution forApertureOfResolution);

    /// <summary>
    /// Get the running job count indexed by the aperture resolutions this chunk uses.
    /// for reporting
    /// </summary>
    /// <returns></returns>
    List<(int count, string apertureType)> getRunningJobCountPerAperture();
  }
#endif
}
