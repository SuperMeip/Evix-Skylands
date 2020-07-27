using Evix.Terrain.DataGeneration.Biomes;
using Evix.Terrain.DataGeneration.Sources.Noise;
using Evix.Terrain.DataGeneration.Voronoi;
using System;
using System.Collections.Generic;
using UnityEngine;

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
    protected readonly IBiomeType[] validBiomeTypes;

    /// <summary>
    /// The individual biomes in this map, indexed by ID
    /// </summary>
    protected readonly Dictionary<int, Biome> biomes
      = new Dictionary<int, Biome>();

    /// <summary>
    /// The center points of biomes, dinoted by the center points of Voronoi polygons in our map
    /// TODO: Make this a quadtree as well for itteration searches during block gen
    /// </summary>
    protected readonly Dictionary<Vertex, Biome> biomeVoronoiCenters
       = new Dictionary<Vertex, Biome>();

    /// <summary>
    /// The noise generator used for this voxel source
    /// </summary>
    protected readonly FastNoise noise;

    /// <summary>
    /// Make a new biome map for a given level:
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="biomeTypes"></param>
    protected BiomeMap(Level level, IBiomeType[] biomeTypes) {
      /// Generate the biome map using a voronoi diagram with a random set of points
      Vector2[] randomBiomeCenters = new Vector2[0];
      Dictionary<Vertex, Polygon> voronoiCells = Delaunay.GenerateVoronoiCells(Delaunay.GenerateTriangulation(randomBiomeCenters));
      foreach(Vertex voronoiCenter in voronoiCells.Keys) {
        biomeVoronoiCenters[voronoiCenter] = getBiomeType(voronoiCenter.position);
      }

      seed = level.seed;
      validBiomeTypes = biomeTypes;
      noise = new FastNoise(seed);
    }

    /// <summary>
    /// Get the biome type for the given location on this biomemap
    /// </summary>
    /// <param name="biomeVoronoiCenter"></param>
    /// <returns></returns>
    Biome getBiomeType(Vector2 biomeVoronoiCenter) {
      return getBiomeTypeFor(
        biomeVoronoiCenter,
        getSurfaceHeight(biomeVoronoiCenter),
        getTemperatureMapValue(biomeVoronoiCenter),
        getMoistureMapValue(biomeVoronoiCenter)
      );
    }

    /// <summary>
    /// The formula to get a biome type to generate given the height temp and humidity of the 2D biome map.
    /// </summary>
    /// <returns></returns>
    protected abstract Biome getBiomeTypeFor(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity);

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
