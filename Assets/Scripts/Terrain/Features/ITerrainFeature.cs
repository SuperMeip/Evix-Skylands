namespace Evix.Terrain.Features {
  public interface ITerrainFeature {

    /// <summary>
    /// The base world voxel location of this feature
    /// Where it's root is in the world
    /// </summary>
    Coordinate worldRoot {
      get;
    }
  }
}