using Evix.Terrain.Collections;
using Evix.Terrain.Features;

namespace Evix.Terrain.DataGeneration.Biomes {
  public class GrassyPlains : Biome {

    /// <summary>
    /// The base sea level for the plain
    /// </summary>
    public int seaLevel = World.SeaLevel;

    /// <summary>
    ///  Make a new grassy plains biome with the given seed
    /// </summary>
    /// <param name="seed"></param>
    public GrassyPlains(int seed, PlainSettings settings) : base(
      seed,
      settings,
      new IFeatureType[] {
        new Tree(seed) 
      }
    ) { }

    /// <summary>
    /// Generate the voxels at the given chunk
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    public override byte generateVoxelAt(Coordinate worldLocation, out ITerrainFeature feature, XZMapData xzData) {
      feature = null;
      if (worldLocation.y == xzData.surfaceHeight) {
        if (worldLocation.x + worldLocation.y % 12 == 0) {
          // make a new feature at the local chunk location
          feature = potentialFeatures[0].make(worldLocation - (Chunk.IDFromWorldLocation(worldLocation) * Chunk.Diameter));
        }
        return TerrainBlock.Types.Grass.Id;
      } else if (worldLocation.y < xzData.surfaceHeight) {
        return TerrainBlock.Types.Dirt.Id;
      } else {
        return TerrainBlock.Types.Air.Id;
      }
    }

    /// <summary>
    /// Get the surface height from sea level 
    /// </summary>
    /// <param name="currentWorldXZLocation"></param>
    /// <returns></returns>
    public override XZMapData getMapDataForXZLocation(Coordinate currentWorldXZLocation) {
      PlainSettings plainSettings = (PlainSettings)settings;
      return new XZMapData {
        surfaceHeight = seaLevel 
          + (int)noise.GetPerlin(currentWorldXZLocation.x, currentWorldXZLocation.z)
          .scale(plainSettings.maxHillHeightVariance, -plainSettings.maxValleyDephVarriance)
      };
    }

    /// <summary>
    /// The settings for a grassy plain
    /// </summary>
    public struct PlainSettings : IBiomeSettings {

      /// <summary>
      /// The maximim hill height
      /// </summary>
      public int maxHillHeightVariance;

      /// <summary>
      /// the maximum vally deph in the plains
      /// </summary>
      public int maxValleyDephVarriance;

      /// <summary>
      /// Equality with other settings
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public bool Equals(IBiomeSettings other) {
        if (other is PlainSettings) {
          PlainSettings otherPlainSettings = (PlainSettings)other;

          return maxHillHeightVariance == otherPlainSettings.maxHillHeightVariance
            && maxValleyDephVarriance == otherPlainSettings.maxValleyDephVarriance;
        }

        return false;
      }
    }
  }
}
