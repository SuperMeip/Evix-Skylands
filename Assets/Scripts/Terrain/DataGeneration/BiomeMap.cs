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
      /// init randomness
      seed = level.seed;
      validBiomeTypes = biomeTypes;
      noise = new FastNoise(seed);

      /// Generate the biome map using a voronoi diagram with a random set of points
      Vector2[] randomBiomeCenters = new Vector2[0];
      Dictionary<Vertex, Polygon> voronoiCells = Delaunay.GenerateVoronoiCells(
        Delaunay.GenerateTriangulation(randomBiomeCenters)
      );

      // generate the biomes from the voronoi cells.
      foreach (Polygon voronoiCell in voronoiCells.Values) {
        assignBiomeToVoronoiCell(voronoiCell);
      }
    }

    /// <summary>
    /// Assign a biome to a voronoi cell
    /// </summary>
    /// <param name="voronoiCell"></param>
    void assignBiomeToVoronoiCell(Polygon voronoiCell) {
      IBiomeType newCellBiomeType = getBiomeType(voronoiCell.center);

      /// check if an existing neighboring biome of the same type should just extend into this one
      voronoiCell.center.forEachOutgoingVector((delaunayEdge, _) => {
        if (biomeVoronoiCenters.TryGetValue(delaunayEdge.pointsTo, out Biome neighboringCellBiome)
          && neighboringCellBiome.isOfType(newCellBiomeType)
        ) {
          // Set the biome to use and end the loop
          biomeVoronoiCenters[voronoiCell.center] = neighboringCellBiome;
          return;
        }
      });

      /// if we don't have a neighbor of the same type, make a new one.
      Biome newBiome = newCellBiomeType.make(seed);
      biomeVoronoiCenters[voronoiCell.center] = newBiome;
      biomes[newBiome.Id] = newBiome;
    }

    /// <summary>
    /// Get the biome type for the given world block location on this biomemap
    /// </summary>
    /// <param name="worldBlockLocation"></param>
    /// <returns></returns>
    IBiomeType getBiomeType(Coordinate worldBlockLocation) {
      return getBiomeTypeFor(
        worldBlockLocation,
        getBaseSurfaceHeight(worldBlockLocation),
        getTemperatureMapValue(worldBlockLocation),
        getMoistureMapValue(worldBlockLocation)
      );
    }

    /// <summary>
    /// The formula to get a biome type to generate given the height temp and humidity of the 2D biome map.
    /// </summary>
    /// <returns></returns>
    protected abstract IBiomeType getBiomeTypeFor(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity);

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
    protected virtual int getBaseSurfaceHeight(Coordinate worldLocation) {
      return (int)getBaseHeightMapValue(worldLocation)
        .scale(World.SeaLevel + maxHeightAboveSeaLevel, World.SeaLevel - maxDepthBelowSeaLevel);
    }

    /// <summary>
    /// Get the hight map value for the given world location (2D)
    /// </summary>
    protected virtual float getBaseHeightMapValue(Coordinate worldLocation) {
      return (int)noise.GetPerlin(worldLocation.x, worldLocation.z);
    }
  }
}
