using Evix.Events;
using Evix.Terrain.DataGeneration;
using Evix.Terrain.Features;
using Evix.Terrain.MeshGeneration;
using Evix.Terrain.Resolution;
using Evix.Voxels;
using System;
using System.Collections.Generic;

namespace Evix.Terrain.Collections {

  /// <summary>
  /// A chunk of terrain that uses the Coordinate of it's 0,0,0 as an ID in the level
  /// </summary>
  public class Chunk
#if DEBUG
    : IRecorded
#endif
  {

    /// <summary>
    /// Levels of how fully loaded a chunk's data can be, and how it's displayed
    /// It's "resolution"
    /// Count is always the last value to keep count of the #
    /// </summary>
    public enum Resolution { UnLoaded, Loaded, Meshed, Visible, Count};

    /// <summary>
    /// The chunk of terrain's diameter in voxels. Used for x y and z
    /// </summary>
    public const int Diameter = 16;

    /// <summary>
    /// The coordinate id of this chunk in the level
    /// </summary>
    public Coordinate id {
      get;
    }

    /// <summary>
    /// The current resolution of this chunk
    /// </summary>
    public Resolution currentResolution {
      get;
      private set;
    } = Resolution.UnLoaded;

    /// <summary>
    /// This chunks generated mesh
    /// </summary>
    public ChunkMeshData meshData {
      get;
      private set;
    } = default;

    /// <summary>
    /// The number of solid (non 0) voxels in the chunk
    /// </summary>
    public int solidVoxelCount {
      get;
      private set;
    } = 0;

    /// <summary>
    /// get if this chunk is empty
    /// </summary>
    public bool isEmpty {
      get => voxels == null;
    }

    /// <summary>
    /// get if the generated mesh is empty
    /// </summary>
    public bool meshIsEmpty {
      get => meshData.isEmpty;
    }

    /// <summary>
    /// The chunk is solid if the solid voxel count equals the max voxel count
    /// </summary>
    public bool isSolid {
      get => solidVoxelCount == Diameter * Diameter * Diameter;
    }

    /// <summary>
    /// If this chunk is locked for work by an aperture
    /// </summary>
    public bool isLockedForWork {
      get;
      private set;
    } = false;

    /// <summary>
    /// The type of resolution work being preformed on this chunk
    /// </summary>
    public (Resolution resolution, ChunkResolutionAperture.FocusAdjustmentType focusAdjustmentType) adjustmentLockType {
      get;
      private set;
    }

    /// <summary>
    /// The voxels
    /// </summary>
    byte[] voxels = null;

    /// <summary>
    /// Features this chunk needs to generate when available.
    /// This is usually for when a neighbor loads a voxel feature, and it extends into an unloaded or locked chunk
    /// </summary>
    List<ITerrainFeature> featureBuffer
      = new List<ITerrainFeature>();

    #region Constructors

    /// <summary>
    /// Make a new chunk with the given id
    /// </summary>
    /// <param name="id"></param>
    public Chunk(Coordinate id) {
      this.id = id;
#if DEBUG
      recordEvent($"Chunk created with id: {id}");
#endif
    }

    #endregion

    #region ChunkID Functions

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static Coordinate IDFromWorldLocation(int x, int y, int z) {
      return new Coordinate(x >> 4, y >> 4, z >> 4);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static Coordinate IDFromWorldLocation(Coordinate worldLocation) {
      return IDFromWorldLocation(worldLocation.x, worldLocation.y, worldLocation.z); ;
    }

    /// <summary>
    /// Get the world location of the 0,0,0 of the chunk with this id
    /// </summary>
    /// <returns></returns>
    public static Coordinate IDToWorldLocation(Coordinate chunkID) {
      return new Coordinate(chunkID.x * Diameter, chunkID.y * Diameter, chunkID.z * Diameter);
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Get the voxel value stored at
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public byte this[int x, int y, int z] {
      // uses same forula as in Coordinate.flatten
      get {
        return voxels != null
          ? voxels[Coordinate.Flatten(x, y, z, Diameter)]
          : (byte)0;
      }
      set {
        if (value != Voxel.Types.Empty.Id) {
          if (voxels == null) {
            voxels = new byte[Diameter * Diameter * Diameter];
#if DEBUG
            recordEvent($"Chunk is no longer empty");
#endif
          }
          if (voxels[Coordinate.Flatten(x, y, z, Diameter)] == Voxel.Types.Empty.Id) {
            solidVoxelCount++;
          }
          voxels[Coordinate.Flatten(x, y, z, Diameter)] = value;
        } else {
          if (voxels != null && voxels[Coordinate.Flatten(x, y, z, Diameter)] != Voxel.Types.Empty.Id) {
            voxels[Coordinate.Flatten(x, y, z, Diameter)] = value;
            solidVoxelCount--;
            if (solidVoxelCount == 0) {
              voxels = null;
#if DEBUG
              recordEvent($"Chunk is empty again");
#endif
            }
          }
        }
      }
    }

    /// <summary>
    /// Shortcut using coordinate for []
    /// </summary>
    /// <param name="voxelLocation"></param>
    /// <returns></returns>
    public byte this[Coordinate voxelLocation] {
      get => this[voxelLocation.x, voxelLocation.y, voxelLocation.z];
      set => this[voxelLocation.x, voxelLocation.y, voxelLocation.z] = value;
    }

    /// <summary>
    /// Try to lock this chunk for work with this aperture
    /// </summary>
    /// <param name="aperture"></param>
    /// <returns></returns>
    public bool tryToLock((Resolution resolution, ChunkResolutionAperture.FocusAdjustmentType focusAdjustmentType) adjustmentLockType) {
      if (isLockedForWork) {
#if DEBUG
        string warning = $"Attempt to lock chunk for: {adjustmentLockType}, failed, already locked for: {this.adjustmentLockType}";
        World.Debug.logWarning(warning);
        recordEvent(warning);
#endif
        return false;
      } else {
        isLockedForWork = true;
        this.adjustmentLockType = adjustmentLockType;
#if DEBUG
        recordEvent($"locked chunk for {adjustmentLockType}");
#endif
        return true;
      }
    }

    /// <summary>
    /// Have the aperture unlock the chunk when it's done working on it
    /// </summary>
    /// <param name="aperture"></param>
    public void unlock((Resolution, ChunkResolutionAperture.FocusAdjustmentType) lockType) {
      if (lockType == adjustmentLockType) {
        isLockedForWork = false;
        adjustmentLockType = default;
#if DEBUG
        recordEvent($"unlocked chunk for {lockType}");
#endif
      } else {
#if DEBUG
        recordEvent($"Tried to unlock locked chunk {adjustmentLockType}, with incorrect type {lockType}");
#endif
        World.Debug.logAndThrowError<System.AccessViolationException>($"Wrong adjustment resolution type tried to unlock chunk {id}: {lockType}. Expecting {adjustmentLockType}");
      }
    }

    /// <summary>
    /// string override
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
      return $"[#{solidVoxelCount}::%{currentResolution}]";
    }

    #endregion

    #region Data Manipulation

    /// <summary>
    /// Try to add a feature to this chunk.
    /// Adds it to the buffer if not all criteria are met
    /// </summary>
    /// TODO: check chunk feature right before generating the mesh for the chunk.
    public void addFeature(VoxelFeature feature) {
      // if this is higher than loaded resolution and we can get a lock, just bake it quick now.
      if (currentResolution >= Resolution.Loaded 
        && tryToLock((Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.Dirty))
      ) {
        feature.bake(this);
        unlock((Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.Dirty));
      } else {
        featureBuffer.Add(feature);
      }
    }

    /// <summary>
    /// Do something for each buffered feature and then clear them.
    /// This will lock the feature buffer
    /// </summary>
    public void bakeBufferedVoxelFeatures(Level level, bool clearFeatureBuffer = true) {
      /// can only bake features via an in focus dirty or load lock
      if (isLockedForWork 
        && (adjustmentLockType == (Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.Dirty)
          || (adjustmentLockType == (Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.InFocus))
          // also allow it if we're checking for stragglers before meshing the chunk
          || (adjustmentLockType == (Resolution.Meshed, ChunkResolutionAperture.FocusAdjustmentType.InFocus) 
            && currentResolution == Resolution.Loaded))
      ) {
        lock (featureBuffer) {
          /// bake all features in
          foreach (ITerrainFeature terrainFeature in featureBuffer) {
            if (terrainFeature is VoxelFeature voxelFeature) {
              // if any of the features produce spillover fragments, try to add them to the chunks they belong to.
              foreach((Coordinate chunkID, VoxelFeature.Fragment fragment) in voxelFeature.bake(this)) {
                level.getChunk(chunkID).addFeature(fragment);
              }
            }
          }

          /// clear the buffer (?)
          featureBuffer = clearFeatureBuffer ? new List<ITerrainFeature>() : featureBuffer;
        }
      } else {
#if DEBUG
        recordEvent($"WARNING {id} could not bake feature voxel data, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
        World.Debug.logError($"Attempting bake feature voxel data on chunk {id} without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
      }
    }

    /// <summary>
    /// Set this chunk's voxel data.
    /// Can only be used when a load aperture has a lock
    /// </summary>
    /// <param name="voxels"></param>
    /// <param name="solidVoxelCount"></param>
    public void setVoxelData(byte[] voxels, int solidVoxelCount) {
      /// we can only set full voxel data on unloaded chunks that are locked for it
      if (isLockedForWork 
        && adjustmentLockType == (Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.InFocus) 
        && currentResolution == Resolution.UnLoaded
      ) {
#if DEBUG
        recordEvent($"Setting generated voxel data with {solidVoxelCount} voxels");
#endif
        if (solidVoxelCount > 0) {
          this.voxels = voxels;
        }
        this.solidVoxelCount = solidVoxelCount;
        currentResolution = Resolution.Loaded;
      } else {
#if DEBUG
        recordEvent($"WARNING {id} could not set voxel data, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
        World.Debug.logError($"Attempting to set voxel data on chunk {id} without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
      }
    }

    /// <summary>
    /// Return the voxel data in a struct for saving
    /// </summary>
    /// <returns></returns>
    public LevelDAO.ChunkSaveData clearVoxelData(ChunkResolutionAperture.Adjustment adjustment) {
      if (isLockedForWork
        && currentResolution >= Resolution.Loaded
        && adjustment.resolution == Resolution.Loaded
        && adjustmentLockType == (Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.OutOfFocus)
      ) {
#if DEBUG
        recordEvent($"clearing voxel data");
#endif
        LevelDAO.ChunkSaveData saveData = new LevelDAO.ChunkSaveData(voxels, solidVoxelCount);
        voxels = null;
        meshData = default;
        solidVoxelCount = 0;
        currentResolution = Resolution.UnLoaded;

        return saveData;
      } else {
#if DEBUG
        recordEvent($"WARNING {id} could not clear voxel data, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
# endif
        World.Debug.logError($"Attempting to clear voxel data on chunk {id} without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
        return default;
      }
    }

    /// <summary>
    /// Set that the chunk node has been meshed in game world for this chunk, or unmesh it
    /// </summary>
    public void setMesh(ChunkMeshData meshData, bool chunkIsDirty = false) {
      /// to mesh a chunk, we need to either have it be locked for a dirty update, or locked for an in focus mesh update
      if (isLockedForWork
        && adjustmentLockType == (Resolution.Meshed, chunkIsDirty
          ? ChunkResolutionAperture.FocusAdjustmentType.Dirty 
          : ChunkResolutionAperture.FocusAdjustmentType.InFocus)
          // if it's not dirty, it has to be at resolution loaded in order to set the mesh
        && (chunkIsDirty || currentResolution == Resolution.Loaded)
      ) {
#if DEBUG
        recordEvent($"Setting chunk mesh with {meshData.triangleCount} tris");
#endif
        currentResolution = Resolution.Meshed;
        this.meshData = meshData;
      } else {
#if DEBUG
        recordEvent($"WARNING {(chunkIsDirty ? "Dirty" : "")} chunk {id} could not set mesh data, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
        World.Debug.logError($"Attempting to set chunk {id} as mehsed on a {(chunkIsDirty ? "Dirty" : "")} chunk without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
      }
    }

    /// <summary>
    /// Remove the set chunk mesh from memmory
    /// </summary>
    public void clearMesh() {
#if DEBUG
      recordEvent($"trying to clear the mesh");
#endif
      /// can only clear the mesh if it's locked for out of focus, and currently at meshed (must be invisible)
      if (isLockedForWork
        && adjustmentLockType == (Resolution.Meshed, ChunkResolutionAperture.FocusAdjustmentType.OutOfFocus)
        && currentResolution == Resolution.Meshed
      ) {
#if DEBUG
        recordEvent($"Clearing chunk mesh");
#endif
        currentResolution = Resolution.Loaded;
        meshData = default;
      } else {
#if DEBUG
        recordEvent($"WARNING {id} could not clear mesh data, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
        World.Debug.logError($"Attempting to remove chunk mesh from chunk {id} without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
      }
    }

    /// <summary>
    /// Set the chunks visible resolution state
    /// </summary>
    /// <param name="activeState"></param>
    public void setVisible(bool activeState = true) {
      /// Set visible
      if (activeState) {
        if (isLockedForWork 
          && adjustmentLockType == (Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.InFocus)
          && currentResolution == Resolution.Meshed
        ) {
#if DEBUG
          recordEvent($"Setting chunk visible");
#endif
          currentResolution = Resolution.Visible;
        } else {
#if DEBUG
          recordEvent($"WARNING {id} could not be set visible, islockeddforwork may not be true ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
          World.Debug.logError($"Attempting to set a chunk {id} visible without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
        }
      /// set invisible
      } else {
        if (isLockedForWork 
          && adjustmentLockType == (Resolution.Visible, ChunkResolutionAperture.FocusAdjustmentType.OutOfFocus)
          && currentResolution == Resolution.Visible
        ) {
#if DEBUG
          recordEvent($"Setting chunk invisible");
#endif
          currentResolution = Resolution.Meshed;
        } else {
#if DEBUG
          recordEvent($"WARNING {id} could not be set invisible, islockeddforwork may not be true, ( {isLockedForWork} ) or it may have an incorrect aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
#endif
          World.Debug.logError($"Attempting to set chunk {id} invisible without the correct aperture lock: {adjustmentLockType}, or resolution level: {currentResolution}");
        }
      }
    }

    #endregion

#if DEBUG
    #region IRecorded Interface

    /// <summary>
    /// A history of events recorded by this chunk
    /// </summary>
    readonly List<(string, string)> eventHistory
      = new List<(string, string)>();

    /// <summary>
    /// add an event to the history
    /// </summary>
    /// <param name="event"></param>
    public void recordEvent(string @event) {
      lock (eventHistory) {
        eventHistory.Add((System.DateTime.Now.ToString("HH: mm:ss.ff"), @event));
      }
    }

    /// <summary>
    /// Get the last X recrded events (or all of them)
    /// </summary>
    /// <param name="lastX"></param>
    /// <returns></returns>
    public (string timestamp, string @event)[] getRecordedEvents(int? lastX = null) {
      return lastX == null
        ? eventHistory.ToArray()
        : eventHistory.GetRange(eventHistory.Count - (int)lastX, (int)lastX).ToArray();
    }
    #endregion
#endif
  }
}
