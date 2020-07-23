using Evix.Terrain.DataGeneration.Biomes;
using Evix.Terrain.DataGeneration.Sources.Noise;
using System.Collections.Generic;

namespace Evix.Terrain.DataGeneration {

  /// <summary>
  /// A map used to get biome data for chunks in a level.
  /// </summary>
  public abstract class BiomeMap {

    /// <summary>
    /// The max height allowed on the heightmap above sea level.
    /// </summary>
    public int maxHeightAboveSeaLevel;

    /// <summary>
    /// The max depth allowed on the heightmap below sea level.
    /// </summary>
    public int maxDepthBelowSeaLevel;

    /// <summary>
    /// The seed this biome map uses.
    /// From the level
    /// </summary>
    protected readonly int seed;

    /// <summary>
    /// The types of biomes this BiomeMap can produce/contain
    /// </summary>
    protected readonly IBiomeType[] biomeTypes;

    /// <summary>
    /// The center points of biomes, dinoted by the center points of Voronoi polygons in our map
    /// TODO: Make this a quadtree as well for itteration searches during block gen
    /// </summary>
    protected readonly Dictionary<Coordinate, Biome> biomeVoronoiCenters;

    /// <summary>
    /// The noise generator used for this voxel source
    /// </summary>
    protected readonly FastNoise noise;

    protected BiomeMap(int seed, IBiomeType[] biomeTypes) {
      this.seed = seed;
      this.biomeTypes = biomeTypes;
      noise = new FastNoise(seed);
    }

    /// <summary>
    /// The formula to get a biome given the height temp and humidity of the 2D biome map.
    /// This should also check neighboring biomes to see if we are simply extending one or need to make a new one.
    /// </summary>
    /// <param name="biomeVoronoiCenter"></param>
    /// <param name="surfaceHeight"></param>
    /// <param name="temperature"></param>
    /// <param name="humidity"></param>
    /// <returns></returns>
    protected abstract Biome getBiomeForValues(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity);

    /// <summary>
    /// Get the biome to use for the given chunk
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    protected abstract Biome getBiomeForChunk(Coordinate chunkID);

    /// <summary>
    /// Get the temperature map value for the given world location (2D)
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <returns></returns>
    protected virtual float getTemperatureMapValue(Coordinate worldLocation) {
      // TODO: vector3cross of the axis of rotation of the sun and vec3.up should get this.
      return 0;
    }

    /// <summary>
    /// Get the moisture map value for the given world location (2D)
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <returns></returns>
    protected virtual float getMoistureMapValue(Coordinate worldLocation) {
      return 0;
    }

    /// <summary>
    /// Get the actual height of the surface, in blocks, of the given area on the map (2D)
    /// </summary>
    /// <param name="worldLocation"></param>
    /// <returns></returns>
    protected virtual int getSurfaceHeight(Coordinate worldLocation) {
      return (int)getHeightMapValue(worldLocation)
        .scale(World.SeaLevel + maxHeightAboveSeaLevel, World.SeaLevel - maxDepthBelowSeaLevel);
    }

    /// <summary>
    /// Get the hight map value for the given world location (2D)
    /// </summary>
    protected virtual float getHeightMapValue(Coordinate worldLocation) {
      return (int)noise.GetPerlin(worldLocation.x, worldLocation.z);
    }
  }
}
