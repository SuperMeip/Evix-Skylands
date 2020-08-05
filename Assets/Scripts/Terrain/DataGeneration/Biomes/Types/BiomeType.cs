using System;

namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// Represents a generator for a type of biome.
  /// TODO: this should take a constructor object that contains the settings for different biome types
  /// </summary>
  public class BiomeType<TypeOfBiome>
    : IBiomeType
    where TypeOfBiome : Biome {

    /// <summary>
    /// By default we return just the type name
    /// </summary>
    public string name {
      get => name == ""
        ? typeof(TypeOfBiome).Name
        : name;
      protected set {
        name = value;
      }
    }

    /// <summary>
    /// The settings for this biome type
    /// Used to create multple biomes of the same type with different settings
    /// </summary>
    public IBiomeSettings settings {
      get;
    }

    /// <summary>
    /// Get the type of biome this produces/represents
    /// </summary>
    public string type {
      get => typeof(TypeOfBiome).Name;
    }

    /// <summary>
    /// Make a new type of biome with the given settings
    /// </summary>
    /// <param name="settings"></param>
    public BiomeType(IBiomeSettings settings) {
      this.settings = settings;
    }

    /// <summary>
    /// Make a new type of biome with no settings
    /// </summary>
    /// <param name="settings"></param>
    public BiomeType() {
      settings = null;
    }

    /// <summary>
    /// Make a new biome of the type this BiomeType represents
    /// </summary>
    /// <returns></returns>
    public virtual Biome make(int seed) {
      return (Biome)typeof(TypeOfBiome)
        .GetConstructor(new Type[] { typeof(int), settings.GetType() })
        .Invoke(new object[] { seed, settings });
    }

    /// <summary>
    /// Equality override
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(IBiomeType other) {
      return name == other.name 
        && settings.Equals(other.settings);
    }
  }
}
