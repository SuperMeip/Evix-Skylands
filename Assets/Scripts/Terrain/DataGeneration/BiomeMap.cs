using Evix.Terrain.DataGeneration.Sources.Noise;

namespace Evix.Terrain.DataGeneration {

  /// <summary>
  /// A map used to get biome data for chunks in a level.
  /// </summary>
  public class BiomeMap {

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
    readonly int seed;

    /// <summary>
    /// The noise generator used for this voxel source
    /// </summary>
    protected readonly FastNoise noise;

    public BiomeMap(int seed) {
      this.seed = seed;
      noise = new FastNoise(seed);
    }

    /// <summary>
    /// Gets the 4 height values for the corners of the given chunk.
    /// Ordered starting with South West (-,-) and going clockwise
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    float[] getChunkCornerHeights(Coordinate chunkID) {
      float[] cornerHeightsClockwiseFromSouthWest = new float[4];
      /// south west (current chunkID)
      Coordinate currentChunkLocation = chunkID;
      cornerHeightsClockwiseFromSouthWest[Corners.SouthWest.Value] = getHeightmapValue(currentChunkLocation);
      /// north west
      currentChunkLocation += Directions.North.Offset;
      cornerHeightsClockwiseFromSouthWest[Corners.NorthWest.Value] = getHeightmapValue(currentChunkLocation);
      /// north east
      currentChunkLocation += Directions.East.Offset;
      cornerHeightsClockwiseFromSouthWest[Corners.NorthEast.Value] = getHeightmapValue(currentChunkLocation);
      // south east
      currentChunkLocation += Directions.South.Offset;
      cornerHeightsClockwiseFromSouthWest[Corners.SouthEast.Value] = getHeightmapValue(currentChunkLocation);

      return cornerHeightsClockwiseFromSouthWest;
    }

    /// <summary>
    /// The heightmap is based on a grid of points made at the corners where chunks meet.
    /// </summary>
    /// <param name="chunkCornerLocation"></param>
    /// <returns></returns>
    float getHeightmapValue(Coordinate chunkCornerLocation) {
      return noise.GetPerlin(chunkCornerLocation.x, chunkCornerLocation.z)
        .scale(maxHeightAboveSeaLevel, -maxDepthBelowSeaLevel);
    }
  }
}
