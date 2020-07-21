using Evix.Terrain.Collections;
using Evix.Terrain.Resolution;
using System.Collections.Generic;

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
    /// The bounds of this object in voxels, width, height, and depth
    /// </summary>
    public Coordinate bounds {
      get;
      protected set;
    }

    /// <summary>
    /// The voxels this stores the generated feature in
    /// </summary>
    protected Dictionary<Coordinate, byte> voxels;

    /// <summary>
    /// The local location in the voxel array that corresponds to the XYZ of the world root of this feature
    /// </summary>
    protected Coordinate localRoot
      = Coordinate.Zero;

    /// <summary>
    /// Make a new voxel feature. base constructor
    /// </summary>
    /// <param name="root"></param>
    protected VoxelFeature(Coordinate root) {
      chunkRoot = root;
    }

    /// <summary>
    /// bake this feature into a chunk,
    /// Returns fragments of the feature that need to be baked into other chunks
    /// </summary>
    /// <param name="chunk"></param>
    /// <returns>A list of spillover fragments with the chunk they should be baked into</returns>
    public virtual List<(Coordinate chunkID, Fragment voxelFeatureFragment)> bake(Chunk chunk) {
      Dictionary<Coordinate, Dictionary<Coordinate, byte>> fragmentFeatureDataBySpilloverChunkID
        = new Dictionary<Coordinate, Dictionary<Coordinate, byte>>();

      /// Get the read point to start at, relative to the feature's local root (if the local root was 0,0,0)
      Coordinate featureStartBuffer = Coordinate.Zero - localRoot;

      // get the point to end voxel read at if the local root was at 0,0,0.
      Coordinate featureEndPoint = featureStartBuffer + bounds;

      /// since the feature's root is now effectively 0,0,0 given our featureStartBuffer, featureEndPoint bounds,
      // what do we need to add to a point to get the chunk location?
      Coordinate chunkFeatureLocationDelta = chunkRoot;

      // get the location in the chunk where the feature's overlap starts. 
      Coordinate chunkOverlapStart = chunkFeatureLocationDelta + featureStartBuffer;

      // get the location in the chunk where the feature overlap ends
      Coordinate chunkOverlapEnd = chunkFeatureLocationDelta + featureEndPoint;

      // itterate between them
      chunkOverlapStart.until(chunkOverlapEnd, chunkVoxelLocation => {
        // if the voxel from the feature at this associated location is within this chunk, add it to this chunk
        byte localFeatureVoxel = voxels[(chunkVoxelLocation - chunkFeatureLocationDelta).flatten(bounds.y, bounds.z)];
        if (chunkVoxelLocation.isWithin(Coordinate.Zero, Chunk.Diameter)) {
          chunk[chunkVoxelLocation] = localFeatureVoxel;
        } else {
          // if the coordinate is out of this chunks bounds, add the chunk it spills over to
          //    to the return
          Coordinate spilloverChunkID = chunk.id;
          if (chunkVoxelLocation.x >= Chunk.Diameter) {
            spilloverChunkID.x += 1;
          } else if (chunkVoxelLocation.x < 0) {
            spilloverChunkID.x -= 1;
          }
          if (chunkVoxelLocation.y >= Chunk.Diameter) {
            spilloverChunkID.y += 1;
          } else if (chunkVoxelLocation.y < 0) {
            spilloverChunkID.y -= 1;
          }
          if (chunkVoxelLocation.z >= Chunk.Diameter) {
            spilloverChunkID.z += 1;
          } else if (chunkVoxelLocation.z < 0) {
            spilloverChunkID.z -= 1;
          }

          /// for each block that's spilled over, add it to the parent chunk's array.
          if (spilloverChunkID != chunk.id) {
            if (!fragmentFeatureDataBySpilloverChunkID.ContainsKey(spilloverChunkID)) {
              fragmentFeatureDataBySpilloverChunkID[spilloverChunkID] = new Dictionary<Coordinate, byte>();
            }
           fragmentFeatureDataBySpilloverChunkID[spilloverChunkID][chunkVoxelLocation - (spilloverChunkID - chunk.id) * Chunk.Diameter] = localFeatureVoxel;
          }
        }
      });

      // build fragments from the collected extra data.
      List<(Coordinate, Fragment)> fragments = new List<(Coordinate, Fragment)>();
      foreach(KeyValuePair<Coordinate, Dictionary<Coordinate, byte>> fragmentData in fragmentFeatureDataBySpilloverChunkID) {
        fragments.Add((fragmentData.Key, new Fragment(fragmentData.Value)));
      }

      return fragments;
    }

    /// <summary>
    /// Make a new fragment with the given data
    /// </summary>
    public class Fragment : VoxelFeature {
      public Fragment(Dictionary<Coordinate, byte> localChunkBlocks) : base((0,0,0)) {
        voxels = localChunkBlocks;
      }

      /// <summary>
      /// Only bake a fragment into the chunk it's given to
      /// </summary>
      /// <param name="chunk"></param>
      /// <returns></returns>
      public override List<(Coordinate chunkID, Fragment voxelFeatureFragment)> bake(Chunk chunk) {
        foreach(KeyValuePair<Coordinate, byte> voxelData in voxels) {
          chunk[voxelData.Key] = voxelData.Value;
        }

        return new List<(Coordinate chunkID, Fragment voxelFeatureFragment)>();
      }
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
        chunk.bakeBufferedVoxelFeatures(level);
        chunk.unlock((Chunk.Resolution.Loaded, ChunkResolutionAperture.FocusAdjustmentType.Dirty));
      }
    }
  }
}