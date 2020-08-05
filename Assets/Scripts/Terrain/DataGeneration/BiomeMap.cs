using Evix.Terrain.Collections;
using Evix.Terrain.DataGeneration.Biomes;
using Evix.Terrain.DataGeneration.Sources.Noise;
using Evix.Terrain.DataGeneration.Voronoi;
using Evix.Terrain.Resolution;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Evix.Terrain.DataGeneration {

  /// <summary>
  /// A map used to get biome data for chunks in a level.
  /// </summary>
  public abstract class BiomeMap {

    /// <summary>
    /// The maximum amount of biomes one chunk can be sliced into
    /// </summary>
    const int MaxBiomesPerSlicedChunk = 4;

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
    protected BiomeMap(Level level, int numberOfVoronoiPoints = 250) {
      /// init randomness
      seed = level.seed;
      noise = new FastNoise(seed);

      /// Generate the biome map using a voronoi diagram with a random set of points
      //Generate the random sites
      List<Vector2> randomBiomeCenters = new List<Vector2>();
      int max = level.chunkBounds.x * Chunk.Diameter;
      int bigSize = max * 5;

      Random.InitState(seed);
      for (int i = 0; i < numberOfVoronoiPoints; i++) {
        int randomX = Random.Range(0, max);
        int randomZ = Random.Range(0, max);
        randomBiomeCenters.Add(new Vector2(randomX, randomZ));
      }

      // add a star around the random points to clean them up
      randomBiomeCenters.Add(new Vector2(0f, bigSize));
      randomBiomeCenters.Add(new Vector2(0f, -bigSize));
      randomBiomeCenters.Add(new Vector2(bigSize, 0f));
      randomBiomeCenters.Add(new Vector2(-bigSize, 0f));

      // generate the cells based on the centers
      Dictionary<Vertex, Polygon> voronoiCells = Delaunay.GenerateVoronoiCells(
        Delaunay.GenerateTriangulation(randomBiomeCenters)
      );

      // generate the biomes from the voronoi cells.
      foreach (Polygon voronoiCell in voronoiCells.Values) {
        assignBiomeToVoronoiCell(voronoiCell);
      }
    }

    /// <summary>
    /// Get the biome to use for the given chunk
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    public Biome getBiomeForChunk(Coordinate chunkID) {
      /// get the closest centers by distance that may be intersecting this chunk
      List<Vertex> closestBiomeCenters = biomeVoronoiCenters.Keys
        .OrderBy(vertex => ((Coordinate)vertex).distance(chunkID.xz))
        .Take(MaxBiomesPerSlicedChunk).ToList();

      // get the bounds of this chunk from above as a rectangle
      Coordinate chunkMin = chunkID * Chunk.Diameter;
      Coordinate chunkMax = chunkMin + Chunk.Diameter;
      var chunkBounds = (chunkMax, chunkMin);

      /// find all intersecting cells by their edges
      List<Vertex> intersectingVoronoiCellCenters = new List<Vertex>();
      foreach (Vertex vornoiCenter in closestBiomeCenters) {
        vornoiCenter.centerPointOf.forEachEdgeUntil((edge) => {
          if (Delaunay.LineIntersectsRectangle(chunkBounds, (edge.start, edge.end))) {
            intersectingVoronoiCellCenters.Add(vornoiCenter);

            return false;
          }

          return true;
        });
      }

      // if there are no intersecting cells, we're in the cell
      if (intersectingVoronoiCellCenters.Count == 0) {
        return biomeVoronoiCenters[closestBiomeCenters[0]];
      } else if (intersectingVoronoiCellCenters.Count == 1) {
        World.Debug.logAndThrowError<System.MissingMemberException>(
          $"Only one cell found to intersect this chunk, that shouldn't happen as all half edges are two sided"
        );
        return default;
        // if we have multiple biomes, return a weighted combined biome
      } else {
        List<(Coordinate center, Biome biome)> weightedBiomes = new List<(Coordinate center, Biome biome)>();
        foreach (Coordinate vertex in intersectingVoronoiCellCenters) {
          weightedBiomes.Add((chunkID + Chunk.Diameter / 2, biomeVoronoiCenters[vertex]));
        }

        return new CombinedEdgeBiome(seed, weightedBiomes.ToArray());
      }
    }

    /// <summary>
    /// The formula to get a biome type to generate given the height temp and humidity of the 2D biome map.
    /// </summary>
    /// <returns></returns>
    protected abstract IBiomeType getBiomeTypeFor(Coordinate biomeVoronoiCenter, int surfaceHeight, float temperature, float humidity);

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

#if DEBUG
    public Polygon[] getBiomeCellsAround(Coordinate location, int blockRadius) {
      return biomeVoronoiCenters.Keys.Select(vertex => {
        if (location.distance(vertex) <= blockRadius * Chunk.Diameter) {
          return vertex.centerPointOf;
        } else return null;
      }).Where(x => x != null).ToArray();
    }
#endif
  }

  /// <summary>
  /// A job to generate chunks from biomes
  /// </summary>
  public struct GenerateChunkVoxelsFromBiomeJob : ChunkResolutionAperture.IAdjustmentJob {

    /// <summary>
    /// The adjustment
    /// </summary>
    public ChunkResolutionAperture.Adjustment adjustment {
      get;
    }

    /// <summary>
    /// The level to get the chunk from
    /// </summary>
    readonly Level level;

    /// <summary>
    /// Make a new biome chunk gen job
    /// </summary>
    /// <param name="adjustment"></param>
    /// <param name="level"></param>
    public GenerateChunkVoxelsFromBiomeJob(ChunkResolutionAperture.Adjustment adjustment, Level level) {
      this.adjustment = adjustment;
      this.level = level;
    }

    /// <summary>
    /// Generate a chunk from the biome
    /// </summary>
    public void doWork() {
      Biome biome = level.biomeMap.getBiomeForChunk(adjustment.chunkID);
      Chunk chunk = level.getChunk(adjustment.chunkID);
      biome.generateVoxelsFor(chunk);
      chunk.unlock((adjustment.resolution, adjustment.type));
    }
  }
}
