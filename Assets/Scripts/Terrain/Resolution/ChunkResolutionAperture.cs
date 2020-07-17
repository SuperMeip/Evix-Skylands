using Evix.Terrain.Collections;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Evix.Terrain.Resolution {

  // TODO: Add static global timer singleton to track times for things in DEBUG mode

  /// <summary>
  /// An area that manages the resolution of chunks within it
  /// </summary>
  public abstract class ChunkResolutionAperture : IChunkResolutionAperture {

    /// <summary>
    /// The focus change type of an adjustment to be made to a chunk
    /// </summary>
    public enum FocusAdjustmentType { InFocus, OutOfFocus, Dirty };

    /// <summary>
    /// The resolution this aperture loads to
    /// </summary>
    public Chunk.Resolution resolution {
      get;
      private set;
    }

    /// <summary>
    /// The managed chunk area radius, X and Z. Height may be different.
    /// </summary>
    public int managedChunkRadius {
      get;
      private set;
    }

    /// <summary>
    /// The managed chunk area height
    /// </summary>
    public int managedChunkHeightRadius {
      get;
      private set;
    }

    /// <summary>
    /// The level this apeture works for
    /// </summary>
    protected IFocusLens lens {
      get;
    }

    /// <summary>
    /// The maximum distance a managed chunk should be from the center of this aperture.
    /// </summary>
    readonly int MaxManagedChunkDistance;

    /// <summary>
    /// The y weight multiplier of this apeture, used for priority and distance skewing
    /// </summary>
    readonly float YWeightMultiplier;

    /// <summary>
    /// The priority queue that this aperture manages
    /// </summary>
    List<Adjustment> adjustmentQueue
      = new List<Adjustment>();

    /// <summary>
    /// The chunk bounds this aperture is managing
    /// </summary>
    Coordinate[] managedChunkBounds
      = new Coordinate[2];

    #region constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="level"></param>
    /// <param name="managedChunkRadius"></param>
    /// <param name="managedChunkHeight"></param>
    protected ChunkResolutionAperture(
      Chunk.Resolution resolution,
      IFocusLens lens,
      int managedChunkRadius,
      int managedChunkHeight = 0,
      float yDistanceWeightMultiplier = 5.0f
      ) {
      this.resolution = resolution;
      this.lens = lens;
      this.managedChunkRadius = managedChunkRadius;
      YWeightMultiplier = yDistanceWeightMultiplier;
      managedChunkHeightRadius = managedChunkHeight == 0 ? managedChunkRadius : managedChunkHeight;
      double distanceSquared = Math.Pow(managedChunkRadius, 2);
      double distanceHeightSquared = Math.Pow(managedChunkHeightRadius, 2);
      MaxManagedChunkDistance = (int)Math.Sqrt(
        // a[a'^2 + b'^2] squared + b squared 
        distanceSquared + distanceSquared + distanceHeightSquared
      );
    }

    #endregion

    #region IChunkResolutionAperture API Functions

    /// <summary>
    /// Check if this adjustment is valid for this aperture still.
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="chunk"></param>
    /// <returns></returns>
    internal virtual bool isValid(Adjustment adjustment, out Chunk chunk) {
      chunk = lens.level.getChunk(adjustment.chunkID);

      return true;
    }

    /// <summary>
    /// Check if this adjustment is ready to schedule a job for.
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected virtual bool isReady(Adjustment adjustment, Chunk validatedChunk) {
      return true;
    }

    /// <summary>
    /// Construct a job for this adjustment to be scheduled.
    /// </summary>
    /// <param name="adjustment"></param>
    /// <returns></returns>
    protected abstract ApetureJobHandle getJob(Adjustment adjustment);

    #endregion

    #region Lens Adjustment API

    /// <summary>
    /// Try to get the next adjustment job that should be run for this aperture
    /// </summary>
    /// <param name="jobHandle"></param>
    /// <returns></returns>
    public bool tryToGetNextAdjustmentJob(ILevelFocus focus, out ApetureJobHandle jobHandle) {
      jobHandle = null;
      int queueBottleneck = 20;
      for (int index = 0; (index < adjustmentQueue.Count) && (queueBottleneck-- > 0); index++) {
        Adjustment waitingAdjustment;
        // get the 0th adjustment
        lock (adjustmentQueue) {
          waitingAdjustment = adjustmentQueue[index];
          adjustmentQueue.RemoveAt(index);
        }
        index--;

        if (!isWithinManagedBounds(waitingAdjustment) || !isValid(waitingAdjustment, out Chunk validatedChunk)) {
          continue;
        }

        // if the item is not locked by another job, check if it's ready
        // if it's ready, we'll try to lock it so this aperture can work on it
        if (!validatedChunk.isLockedForWork
          && isReady(waitingAdjustment, validatedChunk)
          && validatedChunk.tryToLock((waitingAdjustment.resolution, waitingAdjustment.type))
        ) {
          jobHandle = getJob(waitingAdjustment);
#if DEBUG
          validatedChunk.recordEvent($"Aperture Type {GetType().Name} running job {jobHandle.job.GetType().Name} for adjustment: {waitingAdjustment}");
#endif
          return true;
          // if it's not ready, or there's a conflict requeue
          // if there's a conflict, it means a job is already running on this chunk and we should wait for that one to finish
        } else {
          // shove it back into the sort where we got it from
          adjustmentQueue.Insert(index + 1, waitingAdjustment);
        }
      }

      return false;
    }

    /// <summary>
    /// Add a dirty chunk to this apeture's queue
    /// </summary>
    /// <param name="chunkID"></param>
    /// <param name="focus"></param>
    public void addDirtyChunk(Coordinate chunkID, ILevelFocus focus) {
      adjustmentQueue.Insert(0, new Adjustment(
        chunkID,
        FocusAdjustmentType.Dirty,
        resolution,
        focus.id
      ));
#if DEBUG
      lens.level.getChunk(chunkID).recordEvent($"Apeture type {GetType()} for {resolution} resolution has been notified this chunk is dirty");
#endif
    }

    /// <summary>
    /// Get the chunks for a new focus point being initilized
    /// </summary>
    /// <param name="newFocalPoint"></param>
    public int updateAdjustmentsForFocusInitilization(ILevelFocus newFocalPoint) {
      List<Adjustment> chunkAdjustments = new List<Adjustment>();
      managedChunkBounds = getManagedChunkBounds(newFocalPoint);

      /// just get the new in focus chunks for the whole managed area
      managedChunkBounds[0].until(managedChunkBounds[1], inFocusChunkLocation => {
        chunkAdjustments.Add(new Adjustment(inFocusChunkLocation, FocusAdjustmentType.InFocus, resolution, newFocalPoint.id));
      });

      adjustmentQueue.AddRange(chunkAdjustments);
      sortQueueAround(newFocalPoint);

#if DEBUG
      foreach (Adjustment adjustment in chunkAdjustments) {
        lens.level.getChunk(adjustment.chunkID).recordEvent($"Added to apeture queue for {resolution}");
      }
#endif

      return chunkAdjustments.Count;
    }

    /// <summary>
    /// Adjust the bounds and resolution loading for the given focus.
    /// </summary>
    /// <param name="focus"></param>
    public void updateAdjustmentsForFocusLocationChange(ILevelFocus focus) {
      List <Adjustment> chunkAdjustments = new List<Adjustment>();
      Coordinate[] newManagedChunkBounds = getManagedChunkBounds(focus);

      /// get newly in focus chunks
      newManagedChunkBounds.forEachPointNotWithin(managedChunkBounds, inFocusChunkLocation => {
        Adjustment adjustment = new Adjustment(inFocusChunkLocation, FocusAdjustmentType.InFocus, resolution, focus.id);
        /// test for and remove the opposite adjustment
        chunkAdjustments.Add(adjustment);
#if DEBUG
        lens.level.getChunk(adjustment.chunkID).recordEvent($"Added to apeture queue for {adjustment.type} {resolution}");
#endif
      });

      /// see if we should get newly out of focus chunks
      managedChunkBounds.forEachPointNotWithin(newManagedChunkBounds, inFocusChunkLocation => {
        Adjustment adjustment = new Adjustment(inFocusChunkLocation, FocusAdjustmentType.OutOfFocus, resolution, focus.id);
        /// test for and remove the opposite adjustment
        chunkAdjustments.Add(adjustment);
#if DEBUG
        lens.level.getChunk(adjustment.chunkID).recordEvent($"Added to apeture queue for {adjustment.type} {resolution}");
#endif
      });

      /// update the new managed bounds
      managedChunkBounds = newManagedChunkBounds;
      adjustmentQueue.AddRange(chunkAdjustments);
      sortQueueAround(focus);
    }

    /// <summary>
    /// Get an adjustment's priority for this aperture.
    /// Lower is better(?)
    /// </summary>
    /// <returns></returns>
    public virtual int getPriority(Adjustment adjustment, ILevelFocus focus) {
      /// dirty are top priority
      if (adjustment.type == FocusAdjustmentType.Dirty) {
        return 0;
      }

      int distancePriority = adjustment.type == FocusAdjustmentType.InFocus
      ? (int)adjustment.chunkID.distanceYFlattened(focus.currentChunkID, YWeightMultiplier)
      : MaxManagedChunkDistance - (int)adjustment.chunkID.distanceYFlattened(focus.currentChunkID, YWeightMultiplier);

      return distancePriority/* + (int)adjustment.resolution * 3*/;
    }

    /// <summary>
    /// Sort the queue by priority around the given focus
    /// </summary>
    /// <param name="focus"></param>
    protected void sortQueueAround(ILevelFocus focus) {
      lock (adjustmentQueue) {
        adjustmentQueue = adjustmentQueue.OrderBy(adjustment => getPriority(adjustment, focus)).ToList();
      }
      /// update our index based dictionary too
      //waitingAdjustments = new ConcurrentDictionary<Coordinate, Adjustment>(adjustmentQueue.ToDictionary(a => a.chunkID, a => a));
    }


    /// <summary>
    /// Comparer for comparing two keys, handling equality as beeing greater
    /// Use this Comparer e.g. with SortedLists or SortedDictionaries, that don't allow duplicate keys
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class DuplicateIntComparer
      : System.Collections.IComparer {
      #region IComparer<TKey> Members

      public int Compare(object x, object y) {
        int result = ((int)x).CompareTo((int)y);

        return result == 0
          ? 1   // Handle equality as beeing greater
          : result;
      }

      #endregion
    }

    /// <summary>
    /// Do work on the completion of a job
    /// </summary>
    /// <param name="job"></param>
    public virtual void onJobComplete(IAdjustmentJob job) { }

    /// <summary>
    /// An adjustment done on a chunk
    /// </summary>
    public struct Adjustment {

      /// <summary>
      /// The chunk to adjust
      /// </summary>
      public readonly Coordinate chunkID;

      /// <summary>
      /// The type of focus adjustment, in or out of focus
      /// </summary>
      public readonly FocusAdjustmentType type;

      /// <summary>
      /// The resolution of the aperture making the adjustment
      /// </summary>
      public readonly Chunk.Resolution resolution;

      /// <summary>
      /// The id of the focus this adjustment was made for
      /// </summary>
      public readonly int focusID;

      /// <summary>
      /// The opposite of this adjustment
      /// </summary>
      public Adjustment Opposite {
        get => new Adjustment(
          chunkID,
          type == FocusAdjustmentType.InFocus
            ? FocusAdjustmentType.OutOfFocus
            : FocusAdjustmentType.InFocus,
          resolution,
          focusID
        );
      }

      /// <summary>
      /// Make a new adjustment
      /// </summary>
      /// <param name="chunkID"></param>
      /// <param name="adjustmentType"></param>
      /// <param name="resolution"></param>
      public Adjustment(Coordinate chunkID, FocusAdjustmentType adjustmentType, Chunk.Resolution resolution, int focusID) {
        this.chunkID = chunkID;
        type = adjustmentType;
        this.resolution = resolution;
        this.focusID = focusID;
      }

      public override string ToString() {
        return $"[{chunkID}] has moved {type} for apeture #: {resolution}";
      }
    }

    /// <summary>
    /// Handle used to control, schedule, and access an Apeture job
    /// </summary>
    public class ApetureJobHandle {

      /// <summary>
      /// The job to run.
      /// Struct that stores input, output, and the function to run
      /// </summary>
      public readonly IAdjustmentJob job;

      /// <summary>
      /// If we should run this job in the current thread
      /// </summary>
      public readonly bool runSynchronously;

      /// <summary>
      /// Check if the job completed
      /// </summary>
      public bool jobIsComplete {
        get => task != null && task.IsCompleted;
      }

      /// <summary>
      /// The task running the job
      /// </summary>
      Task task;

      /// <summary>
      /// Make a new handle for a job
      /// </summary>
      /// <param name="job"></param>
      public ApetureJobHandle(IAdjustmentJob job, bool runSynchronously = false) {
        this.job = job;
        this.runSynchronously = runSynchronously;
        task = null;
      }

      /// <summary>
      /// Schedule the job to run async
      /// </summary>
      public void schedule() {
        if (!runSynchronously) {
          task = new Task(() => job.doWork());
          task.Start();
        } else {
          job.doWork();
        }
      }
    }

    /// <summary>
    /// An interface for a simple job to handle an adjustment
    /// </summary>
    public interface IAdjustmentJob {

      /// <summary>
      /// The adjustment being handled
      /// </summary>
      Adjustment adjustment {
        get;
      }

      /// <summary>
      /// The job function to run asycn
      /// </summary>
      /// <returns></returns>
      void doWork();
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Check if the location is within any of the managed bounds
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    public bool isWithinManagedBounds(Coordinate chunkID) {
      return chunkID.isWithin(managedChunkBounds);
    }

    /// <summary>
    /// Get a sibiling aperture that shares this lens of the given type
    /// </summary>
    /// <param name="resolution"></param>
    /// <returns></returns>
    protected bool tryToGetSiblingAperture(Chunk.Resolution resolution, out ChunkResolutionAperture siblingAperture) {
      if (lens.tryToGetAperture(resolution, out IChunkResolutionAperture genericSibling)) {
        siblingAperture = genericSibling as ChunkResolutionAperture;
        return true;
      }

      siblingAperture = null;
      return false;
    }

    /// <summary>
    /// Get the managed chunk bounds for the given focus.
    /// </summary>
    /// <param name="focus"></param>
    /// <returns></returns>
    Coordinate[] getManagedChunkBounds(ILevelFocus focus) {
      Coordinate focusLocation = focus.currentChunkID;
      return new Coordinate[] {
        (
          Math.Max(focusLocation.x - managedChunkRadius, 0),
          Math.Max(focusLocation.y - managedChunkHeightRadius, 0),
          Math.Max(focusLocation.z - managedChunkRadius, 0)
        ),
        (
          Math.Min(focusLocation.x + managedChunkRadius, lens.level.chunkBounds.x),
          Math.Min(focusLocation.y + managedChunkHeightRadius, lens.level.chunkBounds.y),
          Math.Min(focusLocation.z + managedChunkRadius, lens.level.chunkBounds.z)
        )
      };
    }

    /// <summary>
    /// Get if this adjustment is still at a valid distance for being managed by this aperture
    /// </summary>
    /// <param name="coordinate"></param>
    /// <returns></returns>
    bool isWithinManagedBounds(Adjustment adjustment) {
      bool chunkIsInFocusBounds = isWithinManagedBounds(adjustment.chunkID);
      if ((!chunkIsInFocusBounds && adjustment.type == FocusAdjustmentType.InFocus)
        || (chunkIsInFocusBounds && adjustment.type == FocusAdjustmentType.OutOfFocus)
      ) {
#if DEBUG
        lens.level.getChunk(adjustment.chunkID).recordEvent($"Aperture Type {GetType()} has dropped out of bounds adjustment: {adjustment}");
#endif
        return false;
      }

      return true;
    }

    #endregion
  }
}
