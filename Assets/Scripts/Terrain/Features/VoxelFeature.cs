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

      foreach (KeyValuePair<Coordinate, byte> featureLocalVoxelData in voxels) {
        /// for each feature voxel within the chunk bounds, bake it in.
        Coordinate chunkLocalVoxelLocation = chunkRoot + featureLocalVoxelData.Key;
        if (chunkLocalVoxelLocation.isWithin(Coordinate.Zero, Chunk.Diameter)) {
          chunk[chunkLocalVoxelLocation] = featureLocalVoxelData.Value;
        } else {
          /// if the coordinate is out of this chunks bounds, add it to a list for the chunk it spills over to
          Coordinate spilloverChunkID = chunk.id;
          // get the id of the chunk we've spilled over into
          if (chunkLocalVoxelLocation.x >= Chunk.Diameter) {
            spilloverChunkID.x += 1;
          } else if (chunkLocalVoxelLocation.x < 0) {
            spilloverChunkID.x -= 1;
          }
          if (chunkLocalVoxelLocation.y >= Chunk.Diameter) {
            spilloverChunkID.y += 1;
          } else if (chunkLocalVoxelLocation.y < 0) {
            spilloverChunkID.y -= 1;
          }
          if (chunkLocalVoxelLocation.z >= Chunk.Diameter) {
            spilloverChunkID.z += 1;
          } else if (chunkLocalVoxelLocation.z < 0) {
            spilloverChunkID.z -= 1;
          }

          // add the block to the spillover chunk's array.
          if (spilloverChunkID != chunk.id) {
            if (!fragmentFeatureDataBySpilloverChunkID.ContainsKey(spilloverChunkID)) {
              fragmentFeatureDataBySpilloverChunkID[spilloverChunkID] = new Dictionary<Coordinate, byte>();
            }
            // we need to calculate the location to put the block in the spillover chunk
            fragmentFeatureDataBySpilloverChunkID[spilloverChunkID][chunkLocalVoxelLocation - (spilloverChunkID - chunk.id) * Chunk.Diameter] 
              = featureLocalVoxelData.Value;
          }
        }
      }

      // build fragments from the collected extra data.
      List<(Coordinate, Fragment)> fragments = new List<(Coordinate, Fragment)>();
      foreach (KeyValuePair<Coordinate, Dictionary<Coordinate, byte>> fragmentData in fragmentFeatureDataBySpilloverChunkID) {
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