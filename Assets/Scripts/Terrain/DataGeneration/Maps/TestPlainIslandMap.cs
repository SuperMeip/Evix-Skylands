using Evix.Terrain.DataGeneration.Biomes;
using System;

namespace Evix.Terrain.DataGeneration.Maps {
  class TestPlainIslandMap : BiomeMap {
    public TestPlainIslandMap(Level level) : base(
      level,
      new IBiomeType[] {
        new BiomeType<GrassyPlains>()
      }
    ) { } 

    protected override Biome getBiomeForChunk(Coordinate chunkID) {
      throw new NotImplementedException();
    }

    protected override Biome getBiomeTypeFor(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity) {
      return validBiomeTypes[0].make(seed);
    }
  }
}
