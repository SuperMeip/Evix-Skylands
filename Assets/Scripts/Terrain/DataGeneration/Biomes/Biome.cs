using Evix.Terrain.Features;

namespace Evix.Terrain.DataGeneration.Biomes {
  public abstract class Biome {

    /// <summary>
    /// The potential features this biome can generate
    /// </summary>
    protected readonly ITerrainFeature[] potentialFeatures;
    
    /// <summary>
    /// Make a new biome with the given potential features
    /// </summary>
    /// <param name="potentialFeatures"></param>
    protected Biome(ITerrainFeature[] potentialFeatures) {
      this.potentialFeatures = potentialFeatures;
    }

    /// <summary>
    /// Generate a voxel byte at the given location.
    /// May also return a feature
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    public abstract byte generate(Coordinate worldLocation, out ITerrainFeature feature);
  }
}
