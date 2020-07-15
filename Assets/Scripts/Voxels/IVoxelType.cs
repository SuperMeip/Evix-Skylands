using UnityEngine;

namespace Evix.Voxels {

  /// <summary>
  /// USed for manipulating generic voxeltypes
  /// </summary>
  public interface IVoxelType {

    /// <summary>
    /// The ID of the voxel
    /// </summary>
    byte Id {
      get;
    }

    /// <summary>
    /// The name of the type of voxel
    /// </summary>
    string Name {
      get;
    }

    /// <summary>
    /// If this voxel type is solid or not
    /// </summary>
    bool IsSolid {
      get;
    }

    /// <summary>
    /// The color of this block
    /// </summary>
    Color Color {
      get;
    }
  }
}