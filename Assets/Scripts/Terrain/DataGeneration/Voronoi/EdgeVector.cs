using System;
using System.Collections.Generic;
using System.Linq;

namespace Evix.Terrain.DataGeneration.Voronoi {

  /// <summary>
  /// Just point of an edge, linked to the edges before and after
  /// Used to create a linked list of a polygon
  /// </summary>
  public class EdgeVector {

		/// <summary>
		///The edge going in the opposite direction
		/// </summary>
		public EdgeVector oppositeEdge;

    /// <summary>
		/// The vertex this ege points to
		/// </summary>
    public Vertex pointsTo {
			get;
			private set;
		}

		/// <summary>
		/// the next edge in the chain for a shape with the given ID.
		/// The id is 0 if this is not contained in a shape yet
		/// </summary>
		public EdgeVector nextEdge {
			get;
			private set;
		}

		/// <summary>
		/// The previous edge in the chain for a shape with the given ID.
		/// The id is 0 if this is not contained in a shape yet
		/// </summary>
		public EdgeVector prevEdge {
			get;
			private set;
		}

		/// <summary>
		/// The face/Polygon/Cel/Shape/Triangle this is a part of
		/// </summary>
		public Polygon parentShape {
			get;
			private set;
		}

		/// <summary>
		/// The start vertex of this edge
		/// </summary>
		public Vertex start {
			get => prevEdge.pointsTo;
		}

		/// <summary>
		/// The end vertex of this edge
		/// </summary>
		public Vertex end {
			get => pointsTo;
		}

		//This structure assumes we have a vertex class with a reference to a half edge going from that vertex
		//and a face (triangle) class with a reference to a half edge which is a part of this face 
		public EdgeVector(Vertex pointsTo, int parentShapeId = 0) {
      this.pointsTo = pointsTo;
			pointsTo.setIncommingVector(this, parentShapeId);
		}

		/// <summary>
		/// Make a linked shape out of a bunch of half edges
		/// </summary>
		/// <param name="edges"></param>
		/// <returns></returns>
		public static EdgeVector MakedLinkedShape(List<EdgeVector> edges) {
			EdgeVector root = null;

			/// shapes can't have less than 2 sides
			if (edges.Count > 2) {

				/// start with the root and chain the edges
				root = edges[0];
				EdgeVector previousEdge = edges[edges.Count - 1];
				for (int index = 0; index < edges.Count; index++) {
					previousEdge.setNextEdge(edges[index]);
					edges[index].setPreviousEdge(previousEdge);
					previousEdge = edges[index];
				}
			}

			return root;
		}

		#region Get and Set Members

		/// <summary>
		/// Change what vertex this points to
		/// </summary>
		/// <param name="vertex"></param>
		public void changeTarget(Vertex vertex, int polygonID = 0) {
			pointsTo = vertex;
			vertex.setIncommingVector(this, polygonID);
		}

		/// <summary>
		/// Set the next edge
		/// </summary>
		/// <param name="nextEdge"></param>
		public void setNextEdge(EdgeVector nextEdge, int polygonID = 0) {
			this.nextEdge = nextEdge;
			pointsTo.setOutgoingVector(nextEdge, polygonID);
			pointsTo.setIncommingVector(this, polygonID);
		}

		/// <summary>
		/// Set the previous edge
		/// </summary>
		/// <param name="nextEdge"></param>
		public void setPreviousEdge(EdgeVector previousEdge, int polygonID = 0) {
			prevEdge = previousEdge;
			previousEdge.pointsTo.setOutgoingVector(this, polygonID);
			prevEdge.pointsTo.setIncommingVector(prevEdge, polygonID);
		}

		/// <summary>
		/// Set the parent shape of this polygon.
		/// This function will remove values at the 0 index of the child vectors too
		/// </summary>
		/// <param name="parentShape"></param>
		public void setParentShape(Polygon parentShape) {
			this.parentShape = parentShape;
			// if this was part of an empty shape before, move it's hook ups to the new shape
			if (pointsTo.getIncommingVector(0) != null) {
				pointsTo.setIncommingVector(pointsTo.getIncommingVector(), parentShape.Id);
				pointsTo.setOutgoingVector(pointsTo.getOutgoingVector(), parentShape.Id);
				pointsTo.removeParentShape(0);
			}

			pointsTo.setParentShape(parentShape);
		}

		#endregion

		public override string ToString() {
			return $"=>{pointsTo}";
		}
	}
}
