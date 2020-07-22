using System;

namespace Evix.Terrain.Features {

  /// <summary>
  /// A Terrain feature of the tree veriety
  /// </summary>
  public class Tree : FeatureType {

    /// <summary>
    /// The seed, setable for instance grabbing
    /// </summary>
    readonly int seed;

    /// <summary>
    /// Create a tree type with the given seed
    /// </summary>
    /// <param name="seed"></param>
    public Tree(int seed) {
      this.seed = seed;
    }

    public override ITerrainFeature make(Coordinate root) {
      return new BasicTreeFeature(root, seed);
    }
  }
}
