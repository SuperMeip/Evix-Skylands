using Evix.Events;
using Evix.Terrain.Collections;

namespace Evix.Terrain.Resolution {
  public class ChunkVisibilityAperture : ChunkResolutionAperture {
    public ChunkVisibilityAperture(IFocusLens lens, int managedChunkRadius, int managedChunkHeight = 0)
      : base(Chunk.Resolution.Visible, lens, managedChunkRadius, managedChunkHeight) {
    }

    #region ApertureFunctions

    /// <summary>
    /// Get the right job
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected override IAdjustmentJob getJob(Adjustment adjustment) {
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        return new SetChunkVisibleJob(adjustment);
      } else {
        return new SetChunkInvisibleJob(adjustment);
      }
    }

    internal override bool isValid(Adjustment adjustment, out Chunk chunk) {
      // if this is valid up to the meshed level, it's valid so far.
      if (base.isValid(adjustment, out chunk)) {
        if (adjustment.type == FocusAdjustmentType.InFocus) {
          // if it's already visible, we can drop it from the job queue
          if (chunk.currentResolution == Chunk.Resolution.Visible) {
#if DEBUG
            chunk.recordEvent($"Chunk invalid for Visible Chunk queue, already at {chunk.currentResolution} resolution");
#endif
            return false;
          }

          // if the chunk's is loaded and the mesh has been generated, and it's empty then we can just set it as visible
          if (chunk.currentResolution >= Chunk.Resolution.Meshed && chunk.meshIsEmpty) {
#if DEBUG
            chunk.recordEvent($"dropped from ChunkVisibilityAperture, chunk is meshed and mesh is empty");
#endif
            if (chunk.currentResolution == Chunk.Resolution.Visible) {
              return false;
            } else if (chunk.tryToLock((Chunk.Resolution.Visible, FocusAdjustmentType.InFocus))) {
              chunk.setVisible(true);
              chunk.unlock((Chunk.Resolution.Visible, FocusAdjustmentType.InFocus));

              return false;
            }
          }

          // if the chunk isn't loaded yet, and we're waiting to mesh it, it's still valid, just not ready.
          return true;
        } else {
          // if it's already out focus enough, we can drop it from the job queue
          if (adjustment.type == FocusAdjustmentType.OutOfFocus && chunk.currentResolution <= Chunk.Resolution.Meshed) {
#if DEBUG
            chunk.recordEvent($"Chunk invalid for inVisible Chunk queue, already at {chunk.currentResolution} resolution");
#endif
            return false;
          }

        // if it's out of focus or dirty, we're fine to go
          return true;
        }
      }

      // if it's not valid for the parent aperture (meshGeneration) then it's not valid for this queue either.
      return false;
    }

    protected override bool isReady(Adjustment adjustment, Chunk validatedChunk) {
      if (adjustment.type == FocusAdjustmentType.InFocus) {
        if (validatedChunk.currentResolution == Chunk.Resolution.Meshed) {
          return true;
        }
      } else if (adjustment.type == FocusAdjustmentType.OutOfFocus) {
        if (validatedChunk.currentResolution == Chunk.Resolution.Visible) {
          return true;
        }
      }

      return false;
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
