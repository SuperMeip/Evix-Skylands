namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// Represents a generator of a type of biome.
  /// Like a Biome Factory for a given type of biome.
  /// </summary>
  public interface IBiomeType {

    /// <summary>
    /// Get an instance of the type of Terrain Feature this feature type represents
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    Biome make();
  }
}