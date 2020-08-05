using Evix.Terrain.Features;
using Evix.Terrain.DataGeneration.Sources.Noise;
using Evix.Terrain.Collections;
using Evix.Voxels;
using System.Collections.Generic;

namespace Evix.Terrain.DataGeneration.Biomes {
  public abstract class Biome {

    /// <summary>
    /// The current universal incremented polygon id being used
    /// </summary>
    static int CurrentMaxBiomeID = 0;

    /// <summary>
    /// The universal id of this biome
    /// </summary>
    public int Id {
      get;
    }

    /// <summary>
    /// The seed used to generate information about this biome
    /// </summary>
    protected readonly int seed;

    /// <summary>
    /// The noise generator used for this voxel source
    /// </summary>
    protected FastNoise noise;

    /// <summary>
    /// The potential features this biome can generate
    /// </summary>
    protected readonly IFeatureType[] potentialFeatures;

    /// <summary>
    /// Settings for this biome.
    /// </summary>
    protected readonly IBiomeSettings settings;
    
    /// <summary>
    /// Make a new biome with the given potential features
    /// </summary>
    /// <param name="potentialFeatures"></param>
    protected Biome(int seed, IBiomeSettings settings = null, IFeatureType[] potentialFeatures = null) {
      Id = System.Threading.Interlocked.Increment(ref CurrentMaxBiomeID);
      this.seed = seed;
      this.settings = settings;
      this.potentialFeatures = potentialFeatures;
      noise = new FastNoise(seed);
    }

    /// <summary>
    /// Generic function to generate the chunk's voxels and voxel features.
    /// This does not bake the voxel features into the chunk
    /// </summary>
    /// <returns></returns>
    public virtual void generateVoxelsFor(Chunk chunk) {
      int solidVoxelCount= 0;
      byte[] voxels = new byte[Chunk.Diameter * Chunk.Diameter * Chunk.Diameter];
      List<ITerrainFeature> features = new List<ITerrainFeature>();
      Coordinate chunkWorldLocation = Chunk.IDToWorldLocation(chunk.id);

      /// For each XZ location in the chunk:
      chunkWorldLocation.xz.until((chunkWorldLocation + Chunk.Diameter).xz, currentWorldXZLocation => {
        currentWorldXZLocation.until(currentWorldXZLocation.replaceY(Chunk.Diameter), currentVoxelWorldLocation => {
          XZMapData xzData = getMapDataForXZLocation(currentWorldXZLocation);
          byte voxelValue = generateVoxelAt(currentVoxelWorldLocation, out ITerrainFeature potentialFeature, xzData);
          // if the voxel isn't empty, we add it to the generated data and count it.
          if (voxelValue != Voxel.Types.Empty.Id) {
            Coordinate localChunkVoxelLocation = currentVoxelWorldLocation - chunkWorldLocation;
            voxels[localChunkVoxelLocation.flatten(Chunk.Diameter)] = voxelValue;
            solidVoxelCount++;
          }

          // add a feature to the list. We don't bake them until after the terrain is generated.
          if (potentialFeature != null) {
            features.Add(potentialFeature);
          }
        });
      });

      // set the base voxel data
      chunk.setVoxelData(voxels, solidVoxelCount);

      List<(Coordinate chunkID, VoxelFeature.Fragment voxelFeatureFragment)> neighborFragments 
        = new List<(Coordinate chunkID, VoxelFeature.Fragment voxelFeatureFragment)>();
      /// While we have a lock, bake the voxel features
      foreach (ITerrainFeature feature in features) {
        chunk.addFeature(feature, false);
      }
    }

    /// <summary>
    /// Get the XZ map data for a given location in this biome. Stuff like surface height that doesn't change for Y
    /// </summary>
    /// <param name="currentWorldXZLocation"></param>
    /// <returns></returns>
    public abstract XZMapData getMapDataForXZLocation(Coordinate currentWorldXZLocation);

    /// <summary>
    /// Generate a voxel byte at the given location.
    /// May also return a feature
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    public abstract byte generateVoxelAt(Coordinate worldLocation, out ITerrainFeature feature, XZMapData xzData);

    /// <summary>
    /// If this biome is of the given type, with the correct settings
    /// </summary>
    /// <param name="biomeType"></param>
    /// <returns></returns>
    public bool isOfType(IBiomeType biomeType) {
      return GetType().Name == biomeType.type
        && settings.Equals(biomeType.settings);
    }

    /// <summary>
    /// Map data for a given XZ position 
    /// </summary>
    public class XZMapData {
      /// <summary>
      ///  The height at the surface for the given XZ location
      /// </summary>
      public int surfaceHeight;
    }
  }
}
