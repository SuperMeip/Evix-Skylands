﻿using Evix.Terrain.Features;
using Evix.Terrain.DataGeneration.Sources.Noise;

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
    /// Make a new biome with the given potential features
    /// </summary>
    /// <param name="potentialFeatures"></param>
    protected Biome(int seed, IFeatureType[] potentialFeatures) {
      Id = System.Threading.Interlocked.Increment(ref CurrentMaxBiomeID);
      this.seed = seed;
      this.potentialFeatures = potentialFeatures;
      noise = new FastNoise(seed);
    }

    /// <summary>
    /// Generate a voxel byte at the given location.
    /// May also return a feature
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    public abstract byte generateAt(Coordinate worldLocation, Coordinate chunkID, out ITerrainFeature feature);

    /// <summary>
    /// If this biome is of the given type
    /// </summary>
    /// <param name="biomeType"></param>
    /// <returns></returns>
    public bool isOfType(IBiomeType biomeType) {
      return GetType().Name == biomeType.type;
    }
  }
}
