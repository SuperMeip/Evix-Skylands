using Evix.Events;
using Evix.Terrain.Collections;

namespace Evix.Terrain.Resolution {
  public class ChunkVisibilityAperture : ChunkResolutionAperture {
    public ChunkVisibilityAperture(IFocusLens lens, int managedChunkRadius, int managedChunkHeight = 0)
      : base(Chunk.Resolution.Visible, lens, managedChunkRadius, managedChunkHeight) {
    }

    #region ApertureFunctions

    protected override bool isValidAndReady(Adjustment adjustment, Chunk chunk) {
      /// in focus
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        /// can only set meshed chunks visible
        if (chunk.currentResolution != Chunk.Resolution.Meshed) {
#if DEBUG
          if (chunk.currentResolution == Chunk.Resolution.Visible) {
            chunk.recordEvent($"Chunk invalid for Visible Chunk queue, already at {chunk.currentResolution} resolution");
          } else {
            chunk.recordEvent($"Chunk invalid for Visible Chunk job, not at expected resolution: {chunk.currentResolution}; Meshed requied");
          }
#endif
          return false;
        /// if the chunk is meshed but empty, we can just upgrade it's resolution without notifying the level manager
        } else if (chunk.meshIsEmpty) {
          if (chunk.adjustmentLockType == (Chunk.Resolution.Visible, FocusAdjustmentType.InFocus)) {
#if DEBUG
          chunk.recordEvent($"Chunk can skip ChunkVisibilityAperture job, chunk has no mesh. Setting as visible");
#endif
            chunk.setVisible(true);
            return false;
          } else World.Debug.logAndThrowError<System.AccessViolationException>($"Trying to make a change inside aperture {GetType().Name} with adjustment {adjustment} on chunk {chunk.id} with an incorrect lock: {chunk.adjustmentLockType}");
        }

        // if the chunk is meshed and not empty, lets try to set it visible
        return true;
      /// out of focus
      } else {
        if (chunk.currentResolution != Chunk.Resolution.Visible) {
#if DEBUG
          chunk.recordEvent($"Chunk invalid for inVisible Chunk queue, currently at {chunk.currentResolution} resolution");
#endif
          return false;
        /// if chunk is visible, we can try to make it un-visible
        } else {
          return true;
        }
      }
    }

    /// <summary>
    /// Get the right job
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected override ApetureJobHandle getJob(Adjustment adjustment) {
      IAdjustmentJob job;
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        job = new SetChunkVisibleJob(adjustment);
      } else {
        job = new SetChunkInvisibleJob(adjustment);
      }

      return new ApetureJobHandle(job, onJobComplete);
    }

    #endregion

    #region Jobs

    /// <summary>
    /// A job for notifying the main thread to set the chunk object active
    /// </summary>
    public struct SetChunkVisibleJob : IAdjustmentJob {

      /// <summary>
      /// The chunk id we're updating to active
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      public SetChunkVisibleJob(Adjustment adjustment) {
        this.adjustment = adjustment;
      }

      /// <summary>
      /// notify the chunk activaton channel that we want this chunk active
      /// </summary>
      public void doWork() {
        World.EventSystem.notifyChannelOf(
          new SetChunkVisibleEvent(adjustment),
          EventSystems.WorldEventSystem.Channels.ChunkActivationUpdates
        );
      }
    }

    /// <summary>
    /// A job for notifying the main thread to set the chunk object inactive
    /// </summary>
    public struct SetChunkInvisibleJob : IAdjustmentJob {

      /// <summary>
      /// The chunk id we're updating to inactive
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      public SetChunkInvisibleJob(Adjustment adjustment) {
        this.adjustment = adjustment;
      }

      /// <summary>
      /// notify the chunk activaton channel that we want this chunk inactive
      /// </summary>
      public void doWork() {
        World.EventSystem.notifyChannelOf(
          new SetChunkInvisibleEvent(adjustment),
          EventSystems.WorldEventSystem.Channels.ChunkActivationUpdates
        );
      }
    }

    #endregion

    #region Events

    /// <summary>
    /// An event to notify the level controller to set a chunk active
    /// </summary>
    public struct SetChunkVisibleEvent : IEvent {

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

      public SetChunkVisibleEvent(Adjustment adjustment) {
        this.adjustment = adjustment;
        name = $"Setting chunk active: {adjustment.chunkID}";
      }
    }

    /// <summary>
    /// An event to notify the level controller to set a chunk inactive
    /// </summary>
    public struct SetChunkInvisibleEvent : IEvent {

      /// <summary>
      /// The name of the event
      /// </summary>
      public string name {
        get;
      }

      /// <summary>
      /// The chunk id we're updating to inactive
      /// </summary>
      public Adjustment adjustment {
        get;
      }

      public SetChunkInvisibleEvent(Adjustment adjustment) {
        this.adjustment = adjustment;
        name = $"Setting chunk active: {adjustment.chunkID}";
      }
    }

    #endregion
  }
}
