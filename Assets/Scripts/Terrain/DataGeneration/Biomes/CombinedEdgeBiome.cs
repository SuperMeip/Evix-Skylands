using Evix.Terrain.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.DataGeneration.Biomes {

  /// <summary>
  /// A biome made of more than one biome, for a chunk on the edge
  /// </summary>
  class CombinedEdgeBiome : Biome {

    /// <summary>
    /// The biomes by distance weight
    /// </summary>
    readonly (Coordinate center, Biome biome)[] biomes;

    /// <summary>
    /// Make a combined biome on the edge of the given biomes
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="biomes"></param>
    public CombinedEdgeBiome(int seed, (Coordinate center, Biome biome)[] biomes) : base (seed) {
      this.biomes = biomes;
    }

    public override byte generateVoxelAt(Coordinate worldLocation, out ITerrainFeature feature, XZMapData xzData) {
      CombinedXZMapData combinedXZData = xzData as CombinedXZMapData;
      return combinedXZData.closestBiome.generateVoxelAt(worldLocation, out feature, combinedXZData.closestBiomeXZData);
    }

    public override XZMapData getMapDataForXZLocation(Coordinate currentWorldXZLocation) {
      return new CombinedXZMapData(currentWorldXZLocation, biomes);
    }

    /// <summary>
    /// Combined xz data
    /// </summary>
    public class CombinedXZMapData : XZMapData {

      /// <summary>
      /// The closest biome to the voxel.
      /// </summary>
      public Biome closestBiome {
        get;
      }

      /// <summary>
      /// The xz data for each biome
      /// </summary>
      public XZMapData closestBiomeXZData {
        get;
      }

      /// <summary>
      /// Biomes with the distance from them to the current block, sorted by distance.
      /// </summary>
      public List<(float distance, Biome biome)> biomesWithDistanceWeights {
        get;
      } = new List<(float distance, Biome biome)>();

      /// <summary>
      /// Make a combined set of data for the xz
      /// </summary>
      /// <param name="xz"></param>
      /// <param name="biomes"></param>
      public CombinedXZMapData(Coordinate xz, (Coordinate center, Biome biome)[] biomes) {
        foreach(var (biomeCenter, biome) in biomes) {
          biomesWithDistanceWeights.Add((biomeCenter.xz.distance(xz), biome));
        }
        biomesWithDistanceWeights = biomesWithDistanceWeights.OrderBy(biome => biome.distance).ToList();
        closestBiome = biomesWithDistanceWeights.First().biome;
        closestBiomeXZData = closestBiome.getMapDataForXZLocation(xz);
      }
    }
  }
}
