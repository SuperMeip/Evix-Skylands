using System.Collections.Generic;
using UnityEngine;

namespace Evix.Terrain.DataGeneration.Voronoi {
  public class Polygon {
    Corner center;
    List<Edge> edges;
    List<Corner> corners;
  }
}
