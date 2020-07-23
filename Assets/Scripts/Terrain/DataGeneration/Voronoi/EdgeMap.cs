using Evix.Terrain.DataGeneration.Voronoi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Terrain.DataGeneration.Voronoi {
  class EdgeMap {
    HashSet<Vertex> vertices;

    /// <summary>
    ///  doesn't work for just the triangles for delenguay tho...
    ///  hmm. does it hurt to store them by circumcenter or should i have two ways to access?
    /// 
    /// This list should just be for voronoi. Denguay will need to get it through the verticies?
    /// </summary>
    Dictionary<Vertex, Polygon> polygons;

    /// <summary>
    /// Not really needed, but here <3
    /// </summary>
    List<EdgeVector> edges;
  }
}
