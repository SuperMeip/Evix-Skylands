using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evix.Terrain.DataGeneration.Voronoi {

  /// <summary>
  /// A vertex/corner/point in a voronoi-delenguay diagram
  /// </summary>
  public class Vertex : IEquatable<Vertex> {

    /// <summary>
    /// The 2D world position of this vertex
    /// </summary>
    public Vector2 position {
      get;
    }

    /// <summary>
    /// The x of this vertex's position
    /// </summary>
    public float x {
      get => position.x;
    }

    /// <summary>
    /// The y of this vertex's position
    /// </summary>
    public float y {
      get => position.y;
    }

    /// <summary>
    /// The parent shapes this vertex is part of
    /// </summary>
    public Dictionary<int, Polygon> parentShapes {
      get;
    } = new Dictionary<int, Polygon>();

    /// <summary>
    /// The polygon this vertex marks the center of if this vertex is for a voronoi shape's centerpoint.
    /// </summary>
    public Polygon centerPointOf;

    /// <summary>
    /// Get if this vertex is connected to anything anymore
    /// </summary>
    public bool isConnectedToAnything {
      get => incommingVectors.Count > 0 || centerPointOf != null;
    }

    /// <summary>
    /// The number of vectors emerging from this point
    /// </summary>
    public int outgoingVectorCount 
      => outgoingVectors.Count;

    /// <summary>
    /// The outgoing half edge if this vertex is in a half-edge linked list
    /// The half edge originating from this vertex
    /// </summary>
    Dictionary<int, EdgeVector> outgoingVectors
      = new Dictionary<int, EdgeVector>();

    /// <summary>
    /// The edge pointing to this vertex.
    /// </summary>
    Dictionary<int, EdgeVector> incommingVectors
      = new Dictionary<int, EdgeVector>();

    #region Constructors and Inplicit Conversions

    /// <summary>
    /// Make a new vertex at the given position
    /// </summary>
    /// <param name="position"></param>
    public Vertex(Vector3 position) {
      this.position = new Vector2(position.x, position.z);
    }

    /// <summary>
    /// Make a new vertex at the given position
    /// </summary>
    /// <param name="position"></param>
    public Vertex(Vector2 position) {
      this.position = position;
    }

    /// <summary>
    /// Make a new vertex with two values
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public Vertex(float x, float y) {
      position = new Vector2(x, y);
    }

    /// <summary>
    /// Turn a vector2 into a Vertex.
    /// </summary>
    /// <param name="coordinates"></param>
    public static implicit operator Vertex(Vector2 vertexLocation) {
      return new Vertex(vertexLocation);
    }

    /// <summary>
    /// Turn a set of coordinates into a Vertex.
    /// </summary>
    /// <param name="coordinates"></param>
    public static implicit operator Vertex((float x, float z) coordinates) {
      return new Vertex(new Vector2(coordinates.x, coordinates.z));
    }

    /// <summary>
    /// Turn a set of coordinates into a Vertex.
    /// </summary>
    /// <param name="coordinates"></param>
    public static implicit operator Vertex((int x, int z) coordinates) {
      return new Vertex(new Vector2(coordinates.x, coordinates.z));
    }

    /// <summary>
    /// Turn a vector2 into a Vertex.
    /// </summary>
    /// <param name="coordinates"></param>
    public static implicit operator Vector2(Vertex vertex) {
      return vertex.position;
    }

    /// <summary>
    /// Turn a vector2 into a Vertex.
    /// </summary>
    /// <param name="coordinates"></param>
    public static implicit operator Vector3(Vertex vertex) {
      return new Vector3(vertex.x, 0, vertex.y);
    }

    #endregion

    #region Get and Set Members

    /// <summary>
    /// Set a parent shape for this vector. A shape this vector is part of
    /// </summary>
    /// <param name="parentShape"></param>
    public void setParentShape(Polygon parentShape) {
      parentShapes[parentShape.Id] = parentShape;
    }

    /// <summary>
    /// Get the parent shape with the given polygon id
    /// </summary>
    public Polygon getParentShape(int polygonID) {
      return parentShapes[polygonID];
    }

    /// <summary>
    /// Remove this point's relation to a specific parent shape
    /// </summary>
    /// <param name="polygonIDToRemove"></param>
    internal void removeParentShape(int polygonIDToRemove) {
      /// 0 doesn't get set
      if (polygonIDToRemove != 0) {
        parentShapes.Remove(polygonIDToRemove);
      }
      incommingVectors.Remove(polygonIDToRemove);
      outgoingVectors.Remove(polygonIDToRemove);
    }

    /// <summary>
    /// Set the outgoing vector for the given polygon this is part of
    /// </summary>
    public void setOutgoingVector(EdgeVector outgoingVector, int polygonID = 0) {
      outgoingVectors[polygonID] = outgoingVector;
    }

    /// <summary>
    /// Preform an action on each pair of edges for this vector
    /// </summary>
    /// <param name="action"></param>
    public void forEachVectorPair(Action<EdgeVector, EdgeVector> action) {
      foreach(KeyValuePair<int, EdgeVector> outgoingVectorKeyValyePair in outgoingVectors) {
        action(incommingVectors[outgoingVectorKeyValyePair.Key], outgoingVectorKeyValyePair.Value);
      }
    }

    /// <summary>
    /// Get the outgoing vector for the given shape that this vertex is a part of
    /// </summary>
    public EdgeVector getOutgoingVector(int polygonID = 0) {
      if (outgoingVectors.TryGetValue(polygonID, out EdgeVector outgoingVector)) {
        return outgoingVector;
      } else if (polygonID != 0 && outgoingVectors.TryGetValue(0, out outgoingVector)) {
        return outgoingVector;
      }

      return null;
    }

    /// <summary>
    /// Set the incomming vector for the given polygon this is part of
    /// </summary>
    public void setIncommingVector(EdgeVector incommingVector, int polygonID = 0) {
      incommingVectors[polygonID] = incommingVector;
    }

    /// <summary>
    /// Get the outgoing vector for the given shape that this vertex is a part of
    /// </summary>
    /// <param name="polygonID"></param>
    public EdgeVector getIncommingVector(int polygonID = 0) {
      if (incommingVectors.TryGetValue(polygonID, out EdgeVector incommingVector)) {
        return incommingVector;
      } else if (polygonID != 0 && incommingVectors.TryGetValue(0, out incommingVector)) {
        return incommingVector;
      }

      return null;
    }

    #endregion

    /// <summary>
    /// Do something for each parent shape attached to this vertex
    /// </summary>
    /// <param name="action"></param>
    public void forEachParentShape(Action<Polygon> action) {
      foreach(Polygon parentShape in parentShapes.Values) {
        action(parentShape);
      }
    }
    
    /// <summary>
    /// Do something for each parent shape attached to this vertex
    /// </summary>
    /// <param name="action"></param>
    public void forEachOutgoingVector(Action<EdgeVector, int> action) {
      foreach(KeyValuePair<int, EdgeVector> edgeValuePair in outgoingVectors) {
        action(edgeValuePair.Value, edgeValuePair.Key);
      }
    }

    #region Equality Overrides

    public override bool Equals(object obj) {
      return Equals((Vertex)obj);
    }

    public bool Equals(Vertex other) {
      return other.position == position;
    }

    /// <summary>
    /// Hash it by using the value to the tenths with the coord hash
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      return new Coordinate(
        position * 10
      ).GetHashCode();
    }

    #endregion

    public override string ToString() {
      return $"({x}, {y})";
    }
  }
}
