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
    /// the side count of this shape
    /// </summary>
    public int sides {
      get;
      private set;
    }

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
      private set;
    }

    /// <summary>
    /// Check if this is a voronoi shape as opposed to a delenguay tri
    /// </summary>
    public bool isVoronoi {
      get => center != null;
    }

    /// <summary>
    /// If this shape obj is empty/doesn't have edges
    /// </summary>
    public bool isEmpty {
      get => firstEdge == null;
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
    /// If the first edge is no onger connected to this polygon, use the replacement
    /// </summary>
    /// <param name="rootEdge"></param>
    public void checkAndSetEdgeList(EdgeVector rootEdge) {
      if (firstEdge == null || firstEdge.parentShape != this) {
        firstEdge = rootEdge;
      }
    }

    /// <summary>
    /// Clear the center point of this voronoi shape
    /// </summary>
    public void clearVoronoiCenter() {
      center.centerPointOf = null;
      center = null;
    }

    /// <summary>
    /// Check if all the edges are connected correctly and this is a complete shape
    /// </summary>
    /// <returns></returns>
    public bool checkIsComplete() {
      bool chainIsUnbroken = true;
      forEachEdgeUntil(edge => {
        chainIsUnbroken = edge != null;

        return chainIsUnbroken;
      });

      return chainIsUnbroken;
    }

    /// <summary>
    /// Preform an action on each edge of this polygon
    /// </summary>
    /// <param name="action"></param>
    public void forEachEdge(Action<EdgeVector> action) {
      EdgeVector currentEdge = firstEdge;
        if (currentEdge == null) {
          var x = 1;
        }
      do {
        action(currentEdge);
        currentEdge = currentEdge.nextEdge;
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
        currentEdge = currentEdge.nextEdge;
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
    /// count up all the edges
    /// </summary>
    public void countEdges() {
      sides = 0;
      forEachEdge(_ => sides++);
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

    /// <summary>
    /// Override tostring shape
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
      return $"{{{Id}}}";
    }
  }
}
