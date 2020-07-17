using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.Features {
  public class Tree : GeneratedVoxelFeature {
    public Tree(Coordinate root, int seed) : base(root, seed) {}

    /// <summary>
    /// Generate a basic tree
    /// </summary>
    protected override void generate() {
      throw new NotImplementedException();
    }
  }
}
