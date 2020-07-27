using System;

namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// Represents a generator for a type of biome.
  /// </summary>
  public class BiomeType<TypeOfBiome> 
    : IBiomeType 
    where TypeOfBiome : Biome {

    /// <summary>
    /// Make a new biome of the type this BiomeType represents
    /// </summary>
    /// <returns></returns>
    public virtual Biome make(int seed) {
      return (Biome)typeof(TypeOfBiome)
        .GetConstructor(new Type[] { typeof(int) })
        .Invoke(new object[] {seed });
    }
  }
}
