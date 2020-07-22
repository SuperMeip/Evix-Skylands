using Evix.Terrain.Features;
using Evix.Terrain.DataGeneration.Sources.Noise;

namespace Evix.Terrain.DataGeneration.Biomes {
  public abstract class Biome {

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
  }
}
