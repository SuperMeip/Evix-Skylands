namespace Evix.Terrain.Features {

  /// <summary>
  /// A generated voxel terrain feature
  /// </summary>
  public abstract class GeneratedVoxelFeature : VoxelFeature {

    /// <summary>
    /// The base seed used to generate this feature
    /// </summary>
    protected readonly int seed;

    /// <summary>
    /// Make a new generated feature
    /// </summary>
    /// <param name="root"></param>
    /// <param name="seed"></param>
    public GeneratedVoxelFeature(Coordinate root, int seed) : base(root) {
      this.seed = seed;
      generate();
    }

    /// <summary>
    /// Generate the feature's voxels
    /// </summary>
    protected abstract void generate();
  }
}
