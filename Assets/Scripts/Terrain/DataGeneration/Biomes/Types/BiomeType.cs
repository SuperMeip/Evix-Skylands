namespace Evix.Terrain.DataGeneration.Biomes {
  /// <summary>
  /// Represents a generator for a type of biome.
  /// </summary>
  public abstract class BiomeType : IBiomeType {

    /// <summary>
    /// Make a new biome of the type this BiomeType represents
    /// </summary>
    /// <returns></returns>
    public abstract Biome make();
  }
}
