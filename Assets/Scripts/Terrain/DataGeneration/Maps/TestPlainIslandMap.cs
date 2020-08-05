using Evix.Terrain.DataGeneration.Biomes;

namespace Evix.Terrain.DataGeneration.Maps {
  class TestPlainIslandMap : BiomeMap {
    public TestPlainIslandMap(Level level, int numberOfVoronoiPoints = 250) : base(
      level,
      numberOfVoronoiPoints
    ) { }

    /// <summary>
    /// Get that grassy biome
    /// </summary>
    /// <returns></returns>
    protected override IBiomeType getBiomeTypeFor(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity) {
      return new BiomeType<GrassyPlains>(new GrassyPlains.PlainSettings() { 
        maxHillHeightVariance = 10,
        maxValleyDephVarriance = 10
      });
    }
  }
}
