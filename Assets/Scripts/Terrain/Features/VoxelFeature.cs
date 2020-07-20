using Evix.Terrain.Collections;
using Evix.Terrain.Resolution;
using System;

namespace Evix.Terrain.Features {

  /// <summary>
  /// A terrain feature made of voxels
  /// </summary>
  public abstract class VoxelFeature : ITerrainFeature {

    /// <summary>
    /// The base world voxel location of this feature
    /// </summary>
    public Coordinate chunkRoot {
      get;
    }

    /// <summary>
    /// The parent chunk id, the id of the chunk with this feature's root in it.
    /// </summary>
    public Coordinate parentChunkID {
      get;
    }

    /// <summary>
    /// The bounds of this object in voxels, width, height, and depth
    /// </summary>
    public Coordinate bounds {
      get;
      protected set;
    }

    /// <summary>
    /// The voxels this stores the generated feature in
    /// </summary>
    protected byte[] voxels;

    /// <summary>
    /// The local location in the voxel array that corresponds to the XYZ of the world root of this feature
    /// </summary>
    protected Coordinate localRoot;

    /// <summary>
    /// Make a new voxel feature. base constructor
    /// </summary>
    /// <param name="root"></param>
    protected VoxelFeature(Coordinate root, Coordinate parentChunkID) {
      chunkRoot = root;
      this.parentChunkID = parentChunkID;
    }

    /// <summary>
    /// bake this feature into a chunk
    /// </summary>
    /// <param name="chunk"></param>
    public void bake(Chunk chunk) {
      /// if this isn't the chunk this feature started in, get the offset
      Coordinate parentChunkLocationOffset = Coordinate.Zero;
      if (parentChunkID != chunk.id) {
        // TODO: make sure the offset is subtracted in the right directon
        parentChunkLocationOffset = (chunk.id - parentChunkID) * Chunk.Diameter;
      }

      /// Get the read point to start at, relative to the feature's local root (if the local root was 0,0,0)
      Coordinate featureStartBuffer = Coordinate.Zero - localRoot;

      // get the point to end voxel read at if the local root was at 0,0,0.
      Coordinate featureEndPoint = featureStartBuffer + bounds;

      /// since the feature's root is now effectively 0,0,0 given our featureStartBuffer, featureEndPoint bounds,
      // what do we need to add to a point to get the chunk location?
      Coordinate chunkFeatureLocationDelta = parentChunkLocationOffset + chunkRoot;

      // get the location in the chunk where the feature's overlap starts. 
      Coordinate chunkOverlapStart = chunkFeatureLocationDelta + featureStartBuffer;

      // get the location in the chunk where the feature overlap ends
      Coordinate chunkOverlapEnd = chunkFeatureLocationDelta + featureEndPoint;

      // itterate between them
      chunkOverlapStart.until(chunkOverlapEnd, chunkVoxelLocation => {
        // if the voxel from the feature at this associated location is within this chunk, add it to this chunk
        if (chunkVoxelLocation.isWithin(Coordinate.Zero, Chunk.Diameter)) {
          chunk[chunkVoxelLocation] = voxels[(chunkVoxelLocation - chunkFeatureLocationDelta).flatten(bounds.y, bounds.z)];
        }
      });
    }

    /// <summary>
    /// A job to add features to a loaded chunk from it's buffer
    /// </summary>
    public struct LoadVoxelTerrainFeaturesJob : ChunkResolutionAperture.IAdjustmentJob {

      /// <summary>
      /// The adjustment
      /// </summary>
      public ChunkResolutionAperture.Adjustment adjustment {
        get;
      }

      /// <summary>
      /// The level this job is working on
      /// </summary>
      readonly Level level;

      /// <summary>
      /// Make a new job
      /// </summary>
      /// <param name="adjustment"></param>
      /// <param name="level"></param>
      public LoadVoxelTerrainFeaturesJob(ChunkResolutionAperture.Adjustment adjustment, Level level) {
        this.adjustment = adjustment;
        this.level = level;
      }

      public void doWork() {
        Chunk chunk = level.getChunk(adjustment.chunkID);
        chunk.bakeBufferedVoxelFeatures();
        chunk.unlock((Chunk.Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.Dirty));
      }
    }
  }
}