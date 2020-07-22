using System.Collections.Generic;
using UnityEngine;

namespace Evix.Terrain.DataGeneration.Voronoi {

  /// <summary>
  /// A vertex in a voroni diagram.
  /// A corner on a voroni polygon
  /// </summary>
  public class Corner {
    Vector2 vertex;
    List<Edge> edges;
    List<Polygon> shapes;
  }
}
