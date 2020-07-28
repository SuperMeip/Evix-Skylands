﻿using System;

namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// Represents a generator for a type of biome.
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
    /// Get the type of biome this produces/represents
    /// </summary>
    public string type {
      get => typeof(TypeOfBiome).Name;
    }

    /// <summary>
    /// Make a new biome of the type this BiomeType represents
    /// </summary>
    /// <returns></returns>
    public virtual Biome make(int seed) {
      return (Biome)typeof(TypeOfBiome)
        .GetConstructor(new Type[] { typeof(int) })
        .Invoke(new object[] {seed });
    }

    /// <summary>
    /// Equality override
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(IBiomeType other) {
      return name == other.name;
    }
  }
}
