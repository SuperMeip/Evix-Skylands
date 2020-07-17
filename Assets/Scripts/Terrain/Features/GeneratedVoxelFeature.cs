using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.Features {
  public abstract class GeneratedVoxelFeature : ITerrainFeature {

    /// <summary>
    /// The base world voxel location of this feature
    /// Where it's root is in the world
    /// </summary>
    public Coordinate worldRoot {
      get;
    }

    /// <summary>
    /// The base seed used to generate this feature
    /// </summary>
    protected readonly int seed;

    /// <summary>
    /// The voxels this stores the generated feature in
    /// </summary>
    protected byte[] voxels;

    /// <summary>
    /// Make a new generated feature
    /// </summary>
    /// <param name="root"></param>
    /// <param name="seed"></param>
    public GeneratedVoxelFeature(Coordinate root, int seed) {
      worldRoot = root;
      this.seed = seed;
      generate();
    }

    /// <summary>
    /// Generate the feature's voxels
    /// </summary>
    protected abstract void generate();
  }
}
