using Evix.Events;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;
using System.Collections.Generic;

namespace Evix.Terrain.Resolution {
  public abstract class FocusLens : IFocusLens {

    /// <summary>
    /// The level this apeture works for
    /// </summary>
    public Level level {
      get;
      private set;
    }

    /// <summary>
    /// The managed focus
    /// </summary>
    protected ILevelFocus focus {
      get;
      private set;
    }

    /// <summary>
    /// The layered apertures that make up this lens, in order of priority
    /// </summary>
    readonly protected Dictionary<Chunk.Resolution, IChunkResolutionAperture> apeturesByResolution
      = new Dictionary<Chunk.Resolution, IChunkResolutionAperture>();

    /// <summary>
    /// The layered apertures that make up this lens, in order of priority
    /// </summary>
    readonly IChunkResolutionAperture[] apeturesByPriority;

    #region Constructors and Initialization

    /// <summary>
    /// Create a new level of the given size that uses the given apetures.
    /// </summary>
    /// <param name="chunkBounds"></param>
    /// <param name="apeturesByPriority"></param>
    protected FocusLens(Level level, ILevelFocus focus, ILensOptions lensOptions) {
      this.level = level;
      this.focus = focus;
      apeturesByPriority = initializeApertures(lensOptions);
      foreach (IChunkResolutionAperture aperture in apeturesByPriority) {
        apeturesByResolution[aperture.resolution] = aperture;
      }
    }

    /// <summary>
    /// Create a new level of the given size that uses the given apetures.
    /// </summary>
    /// <param name="chunkBounds"></param>
    /// <param name="apeturesByPriority"></param>
    protected FocusLens(Level level, ILevelFocus focus) {
      this.level = level;
      this.focus = focus;
      apeturesByPriority = initializeApertures();
      foreach (IChunkResolutionAperture aperture in apeturesByPriority) {
        apeturesByResolution[aperture.resolution] = aperture;
      }
    }

    /// <summary>
    /// Initialize this lens around it's foucs.
    /// </summary>
    /// <returns>the number of chunk nodes this focus will need for rendering</returns>
    public int initialize() {
      int chunksThatNeedARenderNode = 0;

      /// set up all the apeture's base bounds for their focus
      foreach(IChunkResolutionAperture aperture in apeturesByPriority) {
        aperture.initializeBounds(focus);
      }

      /// start by setting off all jobs for the base apeture
      apeturesByPriority[0].updateAdjustmentsForFocusInitilization(focus);

      /// check if there's a meshed component to this lens, if there is we'll need controllers, return how many we'll need.
      if (tryToGetAperture(Chunk.Resolution.Meshed, out IChunkResolutionAperture meshAperture)) { 
        chunksThatNeedARenderNode = meshAperture.managedChunkRadius * meshAperture.managedChunkRadius * meshAperture.managedChunkHeightRadius;
      }

      return chunksThatNeedARenderNode;
    }

    /// <summary>
    /// Get the initialized apertures this lens will be constructed of in order of priority
    /// </summary>
    /// <returns></returns>
    protected abstract IChunkResolutionAperture[] initializeApertures(ILensOptions lensOptions = null);

    /// <summary>
    /// Base interface for lens option structs
    /// </summary>
    public interface ILensOptions {

      /// <summary>
      /// The chunk radius to use as a base for all others.
      /// Ususally the visual chunk radius
      /// </summary>
      int baseChunkRadius {
        get;
      }
    }

    #endregion

    #region Apeture Loop Functions

    /// <summary>
    /// Update each apeture whenever the focus this lens is focused on moves, in reverse priority
    /// </summary>
    public void updateAdjustmentsForFocusMovement() {
      for (int index = apeturesByPriority.Length - 1; index >= 0; index--) {
        IChunkResolutionAperture aperture = apeturesByPriority[index];
        World.Debug.Timer.start("updateAdjustmentsForFocusLocationChange");
        aperture.updateAdjustmentsForFocusLocationChange(focus);
        World.Debug.Timer.record("updateAdjustmentsForFocusLocationChange", aperture.GetType().Name);
      }
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Get the priority for an adjustment from aperture calculations
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="focus"></param>
    /// <returns></returns>
    public float getAdjustmentPriority(ChunkResolutionAperture.Adjustment adjustment, ILevelFocus focus) {
      return apeturesByResolution[adjustment.resolution].getPriority(adjustment, focus);
    }

    /// <summary>
    /// Try to get the apeture type from this lens
    /// </summary>
    /// <param name="resolution"></param>
    /// <param name="aperture"></param>
    /// <returns></returns>
    public bool tryToGetAperture(Chunk.Resolution resolution, out IChunkResolutionAperture aperture) {
      return apeturesByResolution.TryGetValue(resolution, out aperture);
    }

    /// <summary>
    /// Capture notifications about dirtied chunks
    /// </summary>
    /// <param name="event"></param>
    public void notifyOf(IEvent @event) {
      if (@event is Level.ChunkDirtiedEvent cde) {
        foreach(ChunkResolutionAperture aperture in apeturesByPriority) {
          if (aperture.resolution == Chunk.Resolution.Meshed && aperture.isWithinManagedBounds(cde.chunkID)) {
            // throw in jobs to updat the neighbors
            // TODO: are these the right neighbors?
            MarchingTetsMeshGenerator.ForEachDirtiedNeighbor(
              cde.chunkID,
              level,
              neighboringChunk => aperture.updateDirtyChunk(neighboringChunk.id, focus)
            );
            // then throw in the current chunk, so it's at position 0
            aperture.updateDirtyChunk(cde.chunkID, focus);
          }
        }
      }
    }

#if DEBUG
    /// <summary>
    /// All running job counts for debugging
    /// </summary>
    readonly int[] runningJobCounts
      = new int[(int)Chunk.Resolution.Count];

    /// <summary>
    /// add a running job to the count of jobs running for a given aperture
    /// </summary>
    /// <param name="jobHandle"></param>
    public void incrementRunningJobCount(Chunk.Resolution forApertureOfResolution) {
      runningJobCounts[(int)forApertureOfResolution]++;
    }

    /// <summary>
    /// remove a count from the jobs running for a given aperture
    /// </summary>
    /// <param name="jobHandle"></param>
    public void decrementRunningJobCount(Chunk.Resolution forApertureOfResolution) {
      runningJobCounts[(int)forApertureOfResolution]--;
    }

    /// <summary>
    /// Get the running count results
    /// </summary>
    /// <returns></returns>
    public List<(int count, string apertureType)> getRunningJobCountPerAperture() {
      List<(int, string)> results = new List<(int, string)>();
      for(Chunk.Resolution resolution = Chunk.Resolution.Loaded; resolution < Chunk.Resolution.Count; resolution++) {
        if (tryToGetAperture(resolution, out IChunkResolutionAperture aperture)) {
          results.Add((runningJobCounts[(int)resolution], aperture.GetType().Name));
        }
      }

      return results;
    }
#endif

    #endregion
  }
}
