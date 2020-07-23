using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evix.Terrain.DataGeneration.Voronoi {

  /// <summary>
  /// A voronoi polygon, or cell
  /// </summary>
  public class Polygon : IEquatable<Polygon> {

    /// <summary>
    /// The current universal incremented polygon id being used
    /// </summary>
    static int CurrentMaxPolygonID = 0;

    /// <summary>
    /// The universal id of this polygon
    /// </summary>
    public int Id {
      get;
    }

    /// <summary>
    /// The sides count of this polygon
    /// </summary>
    public int sides {
      get;
      private set;
    } = 0;

    /// <summary>
    /// The center of this voronoi polygon/cell 
    /// All positions within a voronoi cell is closer to this position than any other position in the diagram
    /// </summary>
    public Vertex center {
      get;
      private set;
    }

    /// <summary>
    /// The linked list of edges that make up this polygon/cell.
    /// You can use them to get the corner verticies
    /// </summary>
    public EdgeVector firstEdge {
      get;
    }

    /// <summary>
    /// Check if this is a voronoi shape as opposed to a delenguay tri
    /// </summary>
    public bool isVoronoi {
      get => center != null;
    }

    /// <summary>
    /// Make a polygon centered on the given cell
    /// </summary>
    /// <param name="center"></param>
    public Polygon(Vertex center) {
      Id = System.Threading.Interlocked.Increment(ref CurrentMaxPolygonID);
      this.center = center;
      center.centerPointOf = this;
    }

    /// <summary>
    /// Make a polygon out of the given edge link shape
    /// </summary>
    /// <param name="center"></param>
    public Polygon(EdgeVector oneOfTheEdges) {
      Id = System.Threading.Interlocked.Increment(ref CurrentMaxPolygonID);
      firstEdge = oneOfTheEdges;
      forEachEdge(currentEdge => {
        currentEdge.setParentShape(this);
        currentEdge = currentEdge.nextEdge;
        sides++;
      });
    }

    /// <summary>
    /// Clear the center point of this voronoi shape
    /// </summary>
    public void clearVoronoiCenter() {
      center.centerPointOf = null;
      center = null;
    }

    /// <summary>
    /// Preform an action on each edge of this polygon
    /// </summary>
    /// <param name="action"></param>
    public void forEachEdge(Action<EdgeVector> action) {
      EdgeVector currentEdge = firstEdge;
      do {
        action(currentEdge);
      } while (currentEdge != firstEdge);
    }

    /// <summary>
    /// Preform an action on each edge of this polygon
    /// </summary>
    /// <param name="action"></param>
    public void forEachEdgeUntil(Func<EdgeVector, bool> action) {
      EdgeVector currentEdge = firstEdge;
      do {
        if (!action(currentEdge)) {
          break;
        }
      } while (currentEdge != firstEdge);
    }

    /// <summary>
    /// Get all the verticies in this polygon
    /// </summary>
    public void forEachVertex(Action<Vertex> action) {
      forEachEdge(edge => action(edge.pointsTo));
    }

    /// <summary>
    /// Get all the verticies in this polygon
    /// </summary>
    public void forEachVertexUntil(Func<Vertex, bool> action) {
      forEachEdgeUntil(edge => action(edge.pointsTo));
    }

    /// <summary>
    /// Equals override
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj) {
      return Equals((Polygon)obj);
    }

    /// <summary>
    /// Equals override
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(Polygon other) {
      return Id == other.Id;
    }

    /// <summary>
    /// Hash code is polygon id
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      return Id;
    }
  }
}
