using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.Features {

  /// <summary>
  /// Represents and helps create object with interface ITerrainFeature.
  /// </summary>
  public abstract class FeatureType : IFeatureType {
    protected FeatureType() {}

    public abstract ITerrainFeature getInstance(Coordinate root);
  }
}
