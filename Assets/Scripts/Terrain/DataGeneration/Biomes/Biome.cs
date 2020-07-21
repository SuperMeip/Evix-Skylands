using Evix.Terrain.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evix.Terrain.DataGeneration.Biomes {
  public class Biome  {

    protected readonly ITerrainFeature[] potentialFeatures;
    
    protected Biome(ITerrainFeature[] potentialFeatures) {
      this.potentialFeatures = potentialFeatures;
    }

    public byte generate(Coordinate worldLocation) {

    }
  }
}
