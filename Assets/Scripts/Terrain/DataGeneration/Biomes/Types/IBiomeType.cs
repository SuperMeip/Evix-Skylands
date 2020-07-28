using System;

namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// Represents a generator of a type of biome.
  /// Like a Biome Factory for a given type of biome.
  /// </summary>
  public interface IBiomeType : IEquatable<IBiomeType> {

    /// <summary>
    /// The name of the biome type this represents
    /// </summary>
    string name {
      get;
    }

    /// <summary>
    /// The type of biome this produces/represents
    /// </summary>
    string type {
      get;
    }

    /// <summary>
    /// Get an instance of the type of Terrain Feature this feature type represents
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    Biome make(int seed);
  }
}