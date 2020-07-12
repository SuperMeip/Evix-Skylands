﻿using Evix.Events;
using Evix.Terrain.DataGeneration;
using Evix.Terrain.MeshGeneration;
using Evix.Terrain.Resolution;
using Evix.Voxels;
using System.Collections.Generic;

namespace Evix.Terrain.Collections {

  /// <summary>
  /// A chunk of terrain that uses the Coordinate of it's 0,0,0 as an ID in the level
  /// </summary>
  public class Chunk : IRecorded {

    /// <summary>
    /// The coordinate id of this chunk in the level
    /// </summary>
    public Coordinate id {
      get;
    }

    /// <summary>
    /// Levels of how fully loaded a chunk's data can be, and how it's displayed
    /// It's "resolution"
    /// </summary>
    public enum Resolution { UnLoaded, Loaded, Meshed, Visible };

    /// <summary>
    /// The chunk of terrain's diameter in voxels. Used for x y and z
    /// </summary>
    public const int Diameter = 16;

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
    public Resolution resolutionModificationLockType {
      get;
      private set;
    }

    /// <summary>
    /// The voxels
    /// </summary>
    byte[] voxels = null;

    #region Constructors

    /// <summary>
    /// Make a new chunk with the given id
    /// </summary>
    /// <param name="id"></param>
    public Chunk(Coordinate id) {
      this.id = id;
      recordEvent($"Chunk created with id: {id}");
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
            recordEvent($"Chunk is no longer empty");
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
              recordEvent($"Chunk is empty again");
            }
          }
        }
      }
    }

    /// <summary>
    /// Try to lock this chunk for work with this aperture
    /// </summary>
    /// <param name="aperture"></param>
    /// <returns></returns>
    public bool tryToLock(Resolution lockType) {
      if (isLockedForWork) {
        recordEvent($"Attempt to lock chunk for {lockType} failed, already locked for {resolutionModificationLockType}");
        return false;
      } else {
        isLockedForWork = true;
        resolutionModificationLockType = lockType;
        recordEvent($"locked chunk for {lockType}");
        return true;
      }
    }

    /// <summary>
    /// Have the aperture unlock the chunk when it's done working on it
    /// </summary>
    /// <param name="aperture"></param>
    internal void unlock(Resolution lockType) {
      if (lockType == resolutionModificationLockType) {
        isLockedForWork = false;
        resolutionModificationLockType = default;
        recordEvent($"unlocked chunk for {lockType}");
      } else throw new System.AccessViolationException($"Wrong adjustment resolution type tried to unlock chunk: {lockType}. Expecting {resolutionModificationLockType}");
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
    /// Set this chunk's voxel data.
    /// Can only be used when a load aperture has a lock
    /// </summary>
    /// <param name="voxels"></param>
    /// <param name="solidVoxelCount"></param>
    public void setVoxelData(byte[] voxels, int solidVoxelCount) {
      if (isLockedForWork && resolutionModificationLockType == Resolution.Loaded && currentResolution == Resolution.UnLoaded) {
        recordEvent($"Setting generated voxel data with {solidVoxelCount} voxels");
        if (solidVoxelCount > 0) {
          this.voxels = voxels;
        }
        this.solidVoxelCount = solidVoxelCount;
        currentResolution = Resolution.Loaded;
      } else throw new System.AccessViolationException($"Attempting to set voxel data on a chunk without the correct aperture lock or resolution level: {currentResolution}");
    }

    /// <summary>
    /// Return the voxel data in a struct for saving
    /// </summary>
    /// <returns></returns>
    public LevelDAO.ChunkSaveData clearVoxelData(ChunkResolutionAperture.Adjustment adjustment) {
      if (isLockedForWork 
        && currentResolution >= Resolution.Loaded 
        && adjustment.resolution == Resolution.UnLoaded
        && resolutionModificationLockType == Resolution.Loaded
      ) {
        // @todo: check if the voxels aren't nulled
        recordEvent($"clearing voxel data");
        LevelDAO.ChunkSaveData saveData = new LevelDAO.ChunkSaveData(voxels, solidVoxelCount);
        voxels = null;
        meshData = default;
        solidVoxelCount = 0;
        currentResolution = Resolution.UnLoaded;

        return saveData;
      } else throw new System.AccessViolationException($"Attempting to remove voxel data from a chunk without the correct aperture lock or resolution level: {currentResolution}");
    }

    /// <summary>
    /// Set that the chunk node has been meshed in game world for this chunk, or unmesh it
    /// </summary>
    public void setMesh(ChunkMeshData meshData) {
      if (isLockedForWork && resolutionModificationLockType == Resolution.Meshed && currentResolution == Resolution.Loaded) {
        recordEvent($"Setting chunk mesh with {meshData.triangleCount} tris");
        currentResolution = Resolution.Meshed;
        this.meshData = meshData;
      } else throw new System.AccessViolationException($"Attempting to set a chunk as mehsed on a chunk without the correct aperture lock or resolution level: {currentResolution}");
    }

    /// <summary>
    /// Remove the set chunk mesh from memmory
    /// </summary>
    public void clearMesh() {
      if (isLockedForWork && resolutionModificationLockType == Resolution.Meshed && currentResolution == Resolution.Meshed) {
        recordEvent($"Clearing chunk mesh");
        currentResolution = Resolution.Loaded;
        meshData = default;
      } else throw new System.AccessViolationException($"Attempting to remove a chunk mesh from a chunk without the correct aperture lock or resolution level: {currentResolution}");
    }

    /// <summary>
    /// Set the chunks visible resolution state
    /// </summary>
    /// <param name="activeState"></param>
    public void setVisible(bool activeState = true) {
      if (activeState) {
        if (isLockedForWork && resolutionModificationLockType == Resolution.Visible && currentResolution == Resolution.Meshed) {
          recordEvent($"Setting chunk visible");
          currentResolution = Resolution.Visible;
        } else throw new System.AccessViolationException($"Attempting to set a chunk visible without the correct aperture lock or resolution level: {currentResolution}");
      } else {
        if (isLockedForWork && resolutionModificationLockType == Resolution.Visible && currentResolution == Resolution.Visible) {
          recordEvent($"Setting chunk invisible");
          currentResolution = Resolution.Meshed;
        } else throw new System.AccessViolationException($"Attempting to set a chunk invisible without the correct aperture lock or resolution level: {currentResolution}");
      }
    }

    #endregion

    #region IRecorded Interface

    /// <summary>
    /// A history of events recorded by this chunk
    /// </summary>
    List<(string, string)> eventHistory
      = new List<(string, string)>();

    /// <summary>
    /// add an event to the history
    /// </summary>
    /// <param name="event"></param>
    public void recordEvent(string @event) {
      eventHistory.Add((System.DateTime.Now.ToString("HH: mm:ss.ff"), @event));
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
  }
}