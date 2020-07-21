namespace Evix.Terrain.Features {

  /// <summary>
  /// A feature of the terrain
  /// </summary>
  public interface ITerrainFeature {

    /// <summary>
    /// The base chunk local voxel location of this feature
    /// Where it's root is in the world on a chunk
    /// </summary>
    Coordinate chunkRoot {
      get;
    }
  }
}