using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.Features {

  /// <summary>
  /// Used to denote and hold a type of feature that can be generated
  /// </summary>
  public interface IFeatureType {

    /// <summary>
    /// Get an instance of the type of Terrain Feature this feature type represents
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    ITerrainFeature getInstance(Coordinate root);
  }
}
