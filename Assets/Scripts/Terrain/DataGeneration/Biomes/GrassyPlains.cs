using Evix.Terrain.Collections;
using Evix.Terrain.Features;
using System;

namespace Evix.Terrain.DataGeneration.Biomes {
  public class GrassyPlains : Biome {

    /// <summary>
    /// The base sea level for the plain
    /// </summary>
    public int seaLevel = World.SeaLevel;

    /// <summary>
    /// The maximim hill height
    /// </summary>
    public int maxHillHeightVariance = 10;

    /// <summary>
    /// the maximum vally deph in the plains
    /// </summary>
    public int maxValleyDephVarriance = 10;

    /// <summary>
    ///  Make a new grassy plains biome with the given seed
    /// </summary>
    /// <param name="seed"></param>
    public GrassyPlains(int seed) : base(
      seed,
      new IFeatureType[] {
        new Tree(seed) 
      }
    ) { }

    public override byte generate(Coordinate worldLocation, Coordinate chunkID, out ITerrainFeature feature) {
      feature = null;
      int surfaceHeightForXZ = seaLevel + (int)noise.GetPerlin(worldLocation.x, worldLocation.z).scale(maxHillHeightVariance, -maxValleyDephVarriance);
      if (worldLocation.y == surfaceHeightForXZ) {
        if (worldLocation.x + worldLocation.y % 12 == 0) {
          feature = potentialFeatures[0].getInstance(worldLocation - (chunkID * Chunk.Diameter));
        }
        return TerrainBlock.Types.Grass.Id;
      } else if (worldLocation.y < surfaceHeightForXZ) {
        return TerrainBlock.Types.Dirt.Id;
      } else {
        return TerrainBlock.Types.Air.Id;
      }
    }
  }
}
