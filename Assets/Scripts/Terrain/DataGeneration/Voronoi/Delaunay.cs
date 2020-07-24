﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//https://gamedev.stackexchange.com/questions/71328/how-can-i-add-and-subtract-convex-polygons
namespace Evix.Terrain.DataGeneration.Voronoi {
	public static class Delaunay {

		#region Trianglulation

		/// <summary>
		/// Get the triangles, sorted by circumcenter.
		/// </summary>
		/// <param name="points"></param>
		/// <returns></returns>
		public static (HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) GenerateTriangulation(IEnumerable<Vector2> points) {
			/// Step -1. We can't do this with less than 3 points.
			if (points.Count() < 3) {
				Debug.LogWarning("Can make a delaunay with less than 3 points");
				return (null, null);
			}

			/// Step 0. Set up the return data.
			(HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) delaunayData 
				= (new HashSet<Vertex>(), new Dictionary<int, Polygon>());

			/// Step 1. Create the super triangle to surround the area.
			// The super triangle should be bigger than any other thing, and contain all points by a large margin.
			Polygon superTriangle = new Polygon(EdgeVector.MakedLinkedShape(new List<EdgeVector> {
				new EdgeVector((-2000000, 2000000)),
				new EdgeVector((2000000, 2000000)),
				new EdgeVector((0, -2500000))
			}));

			delaunayData.triangles.Add(superTriangle.Id, superTriangle);
			superTriangle.forEachVertex(vertex => delaunayData.vertices.Add(vertex));

			/// Step 2. Go through each point and add it into the existing triangulation
			foreach (Vertex pointToAdd in points) {
				// 2a. Add the new vert to our hashmap data
				delaunayData.vertices.Add(pointToAdd);

				// 2b. try to get an existing triangle that's around this point
				Polygon surroundingTriangle = TriangulationWalk(pointToAdd, delaunayData.triangles);
				if (surroundingTriangle == null) {
					World.Debug.logAndThrowError<System.ArgumentOutOfRangeException>($"No surrounding triangle found for attempted added point {pointToAdd}. Must be outside of supertriangle range! Oh No");
				}

				// 2c. Try to split that triangle using the point we want to add
				SplitTriangleAtPoint(surroundingTriangle, pointToAdd, delaunayData);

				// 2d. Initialize stack. Place all triangles which are adjacent to the edges opposite p on a LIFO stack
				//   The report says we should place triangles, but it's easier to place edges with our data structure
				Stack<EdgeVector> trianglesToInvestigate = new Stack<EdgeVector>();

				// Rotate around the PointToAdd, triangle-by-triangle, to find all opposite edges
				pointToAdd.forEachOutgoingVector((edge, parentShapeID) => {
					EdgeVector oppositeTrianglesEdge = edge.nextEdge.oppositeEdge;
					if (oppositeTrianglesEdge != null && !trianglesToInvestigate.Contains(oppositeTrianglesEdge)) {
						trianglesToInvestigate.Push(oppositeTrianglesEdge);
					}
				});

				// Step 3. Restore the delaunay triangulation
				int safetyCounter = 100000;
				do {
					EdgeVector triangleBaseToTest = trianglesToInvestigate.Pop();
					// 3a. Go through the stack and find triangles that need to be flipped
					if (ShouldFlipTriangleEdgeForDelaunay(
						triangleBaseToTest.pointsTo,
						triangleBaseToTest.prevEdge.pointsTo,
						triangleBaseToTest.nextEdge.pointsTo,
						pointToAdd
					)) {
						// 3b. Flip the triangle
						FlipTriangleEdge(triangleBaseToTest);

						// 3c. Place any triangles which are now opposite pointToAdd on the stack
						pointToAdd.forEachOutgoingVector((edge, parentShapeID) => {
							EdgeVector oppositeTrianglesEdge = edge.nextEdge.oppositeEdge;
							if (oppositeTrianglesEdge != null && !trianglesToInvestigate.Contains(oppositeTrianglesEdge)) {
								trianglesToInvestigate.Push(oppositeTrianglesEdge);
							}
						});
					}
				// continue while we have a stack or until the safety triggers
				} while (trianglesToInvestigate.Count < 0 && safetyCounter-- > 0);
			}

			// Step 4. Remove the super triangle.
			RemoveSuperTriangle(superTriangle, delaunayData);

			return delaunayData;
		}

		/// <summary>
		/// Generate a voronoi diagram from delaunay data
		/// </summary>
		/// <returns>Voronoi cells indexed by their vertex</returns>
		public static Dictionary<Vertex, Polygon> GenerateVoronoiCells((HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) delaunayData) {
			Dictionary<Vertex, Polygon> voronoiCells = new Dictionary<Vertex, Polygon>();
			Dictionary<Vertex, Dictionary<Vertex, Dictionary <Vertex, Vertex>>> circumcenterCache = new Dictionary<Vertex, Dictionary<Vertex, Dictionary<Vertex, Vertex>>>();
			
			/// We create a voronoi polygon cell for every delanuay vertex.
			foreach(Vertex delaunayVertex in delaunayData.vertices) {
				Polygon voronoiCell = new Polygon(delaunayVertex);
				voronoiCells[delaunayVertex] = voronoiCell;

				// these are edges we create that then need to be hooked up to an edge pointing at their start point. (as prev)
				//    indexed by the needed start point, or the would be edge.prevEdge.pointsTo
				Dictionary<Vertex, EdgeVector> incompleEdgesByNeededOriginPoint = new Dictionary<Vertex, EdgeVector>();
				// these are edges that have been hooked up to their prev (start) neighbor already, and can be used to hook up straglers.
				//    indexed by the pointsTo of the completeEdge.
				Dictionary<Vertex, EdgeVector> completeEdges = new Dictionary<Vertex, EdgeVector>();

				// we'll use outpointing vectors to check each line once, as each vector radiating
				//    from this point should have both sides. (in and outgoing) for the multuple triangles.
				//    TODO: hopefully keeping them all outgoing will keep them all facing the same way
				delaunayVertex.forEachOutgoingVector((outgoingVector, polygonID) => {
					Polygon forwardTriangle = outgoingVector.parentShape;
					Polygon behindTriangle = outgoingVector.oppositeEdge.parentShape;

					// Get the two voronoi points we use to create our new edge (and index it by what we want to hook it up to)
					Vertex newEdgePointsTo = CalculateTriangleCircumcenter(forwardTriangle, circumcenterCache);
					Vertex newEdgeOriginPoint = CalculateTriangleCircumcenter(behindTriangle, circumcenterCache);

					// make out new edge for this ray.
					EdgeVector newEdge = new EdgeVector(newEdgePointsTo);
					newEdge.setParentShape(voronoiCell);

					/// Check if there's an incomplete edge waiting to be hooked up to this as it's start (prev.pointsTo) point.
					if (incompleEdgesByNeededOriginPoint.ContainsKey(newEdgePointsTo)) {
						EdgeVector edgeWithThisEdgeAsPrevious = incompleEdgesByNeededOriginPoint[newEdgePointsTo];
						newEdge.setNextEdge(edgeWithThisEdgeAsPrevious, voronoiCell.Id);
						edgeWithThisEdgeAsPrevious.setPreviousEdge(newEdge, voronoiCell.Id);
						// set it in the complete list to where it points, so we can find it later.
						completeEdges[edgeWithThisEdgeAsPrevious.pointsTo] = edgeWithThisEdgeAsPrevious;
						incompleEdgesByNeededOriginPoint.Remove(newEdgePointsTo);
					}

					// add this to the incomplete edges, indexed by the start point we need an edge pointing to.
					if (incompleEdgesByNeededOriginPoint.ContainsKey(newEdgeOriginPoint)) {
						World.Debug.logAndThrowError<AccessViolationException>($"Two vectors must be facing different ways while making a voronoi cell around {new Coordinate(delaunayVertex.position)}");
					} else {
						incompleEdgesByNeededOriginPoint[newEdgeOriginPoint] = newEdge;
					}
				});

				// close up the remaining incomplete edges to form the shape.
				foreach(KeyValuePair<Vertex, EdgeVector> incompleteEdgeAndNeededOrigin in incompleEdgesByNeededOriginPoint) {
					/// if the complete edges dic contains a vector pointing to the start we need
					if (completeEdges.TryGetValue(incompleteEdgeAndNeededOrigin.Key, out EdgeVector originPointingVector)) {
						// grab it and set it correctly.
						incompleteEdgeAndNeededOrigin.Value.setPreviousEdge(originPointingVector, voronoiCell.Id);
						originPointingVector.setNextEdge(incompleteEdgeAndNeededOrigin.Value, voronoiCell.Id);
					}
				}

				/// get the edge count
				voronoiCell.countEdges();
			}

			return voronoiCells;
		}

		#endregion

		#region Helper Functions

		static Polygon TriangulationWalk(Vertex point, Dictionary<int, Polygon> triangles) {
			/// declare return
			Polygon intersectingTriangle = null;

			/// Get a random start triangle
			Polygon currentTriangle = triangles.Values.ToArray()[UnityEngine.Random.Range(0, triangles.Count)];

			//Start the triangulation walk to find the intersecting triangle
			int safety = 0;

			while (true) {
				safety += 1;

				if (safety > 1000000) {
					Debug.Log("Stuck in endless loop when walking in triangulation");

					break;
				}

				//Is the point intersecting with the current triangle?
				//We need to do 3 tests where each test is using the triangles edges
				//If the point is to the right of all edges, then it's inside the triangle
				//If the point is to the left we jump to that triangle instead
				EdgeVector e1 = currentTriangle.firstEdge;
				EdgeVector e2 = e1.nextEdge;
				EdgeVector e3 = e2.nextEdge;


				//Test 1
				if (IsPointToTheRightOrOnLine(e1.prevEdge.pointsTo.position, e1.pointsTo.position, point)) {
					//Test 2
					if (IsPointToTheRightOrOnLine(e2.prevEdge.pointsTo.position, e2.pointsTo.position, point)) {
						//Test 3
						if (IsPointToTheRightOrOnLine(e3.prevEdge.pointsTo.position, e3.pointsTo.position, point)) {
							//We have found the triangle the point is in
							intersectingTriangle = currentTriangle;

							break;
						}
						//If to the left, move to this triangle
						else {
							currentTriangle = e3.oppositeEdge.parentShape;
						}
					}
					//If to the left, move to this triangle
					else {
						currentTriangle = e2.oppositeEdge.parentShape;
					}
				}
				//If to the left, move to this triangle
				else {
					currentTriangle = e1.oppositeEdge.parentShape;
				}
			}

			return intersectingTriangle;
		}

		/// <summary>
		/// Split one triangle into 3 at a given point
		/// </summary>
		/// <param name="shape"></param>
		/// <param name="splitPosition"></param>
		/// <param name="triangles"></param>
		static void SplitTriangleAtPoint(Polygon shape, Vertex splitPosition, (HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) delaunayData) {
			// collect all the new edges
			List<EdgeVector> allNewEdges = new List<EdgeVector>();
			/// foreach edge in the original shape, we want to create a new triangle using the split position.
			shape.forEachEdge(edge => {
				Polygon newTriangle = CreateNewTriangleFromExistingEdge(edge, splitPosition, out List<EdgeVector> generatedTriangleEdges);
				// add data to our output arrays
				delaunayData.triangles.Add(newTriangle.Id, newTriangle);
				allNewEdges.AddRange(generatedTriangleEdges);
			});

			//Find the opposite connections
			foreach (EdgeVector newEdge in allNewEdges) {
				// If we have already found a opposite, skip
				if (newEdge.oppositeEdge != null) {
					continue;
				}

				foreach (EdgeVector potentialOppositeEdge in allNewEdges) {
					// can't have an opposite that's this edge, and can't have an opposite with an opposite already assigned.
					if (newEdge == potentialOppositeEdge || potentialOppositeEdge.oppositeEdge != null) {
						continue;
					}

					// if the end points are opposite of eachother, we've found the opposite
					if (newEdge.end.Equals(potentialOppositeEdge.start) && newEdge.start.Equals(potentialOppositeEdge.end)) {
						newEdge.oppositeEdge = potentialOppositeEdge;
						//Connect it from the other way as well
						potentialOppositeEdge.oppositeEdge = newEdge;
					}
				}
			}

			//Delete the old triangle
			DeletePolygon(shape, delaunayData);
		}

		/// <summary>
		/// Add a new triangle to the data given a split point and an existing triangle's edge
		/// </summary>
		/// <param name="parentEdge">The parent edge, the existing one this triangle is budding off of</param>
		static Polygon CreateNewTriangleFromExistingEdge(EdgeVector parentEdge, Vertex splitPosition, out List<EdgeVector> newTriangleEdges) {
			/// make the new edges from these points
			newTriangleEdges = new List<EdgeVector> {
				new EdgeVector(parentEdge.pointsTo), // previous vertex
				new EdgeVector(splitPosition), // splitting vertex
				new EdgeVector(parentEdge.prevEdge.pointsTo) // next vertex
			};

			/// Make the new triangle from the new edges
			Polygon triangle = new Polygon(EdgeVector.MakedLinkedShape(newTriangleEdges));

			/// create all the connections
			// The first new edge has the same opposite as the parented edge
			newTriangleEdges[0].oppositeEdge = parentEdge.oppositeEdge;
			// Then that opposite edge needs a new reference to this new edge if its not an empty border
			if (newTriangleEdges[0].oppositeEdge != null) {
				parentEdge.oppositeEdge.oppositeEdge = newTriangleEdges[0];
			}

			return triangle;
		}

		/// <summary>
		/// Delete a polygon from a set of delauanayData
		/// </summary>
		/// <param name="polygonToDelete"></param>
		/// <param name="delaunayData"></param>
		static void DeletePolygon(Polygon polygonToDelete, (HashSet<Vertex> vertices, Dictionary<int, Polygon> shapes) delaunayData, bool shouldSetOppositeToNull = false) {
			/// disconnect this shape from all other shapes and their edges
			polygonToDelete.forEachEdge(edge => {
				if (shouldSetOppositeToNull) {
					if (edge.oppositeEdge != null) {
						edge.oppositeEdge.oppositeEdge = null;
					}
				}

				/// remove references from the parent shape from the vertexes
				polygonToDelete.forEachVertex(vertex => {
					vertex.removeParentShape(polygonToDelete.Id);
					// remove the vertex if it's not connected to anything else
					if (!vertex.isConnectedToAnything) {
						delaunayData.vertices.Remove(vertex);
					}
				});

				/// If this is a voronoi shape, remove the center point too
				if (polygonToDelete.isVoronoi) {
					delaunayData.vertices.Remove(polygonToDelete.center);
					polygonToDelete.clearVoronoiCenter();
				}
			});

			delaunayData.shapes.Remove(polygonToDelete.Id);
		}

		/// <summary>
		/// Flip the connecting base of two triangles 90 degrees.
		/// </summary>
		/// <param name="triangleBaseToTest"></param>
		private static void FlipTriangleEdge(EdgeVector triangleBaseToTest) {
			/// The data we need											 
			// This edge's triangle edges
			EdgeVector tri1_edge1 = triangleBaseToTest;
			EdgeVector tri1_edge2 = tri1_edge1.nextEdge;
			EdgeVector tri1_edge3 = tri1_edge1.prevEdge;
			Polygon    triangle1  = tri1_edge1.parentShape;

			// The opposite edge's triangle edges
			EdgeVector tri2_edge1 = tri1_edge1.oppositeEdge;
			EdgeVector tri2_edge2 = tri2_edge1.nextEdge;
			EdgeVector tri2_edge3 = tri2_edge1.prevEdge;
			Polygon    triangle2  = tri2_edge1.parentShape;

			// Move each edge to it's new triangle and target
			tri1_edge1.changeTarget(tri1_edge2.pointsTo, triangle1.Id);
			tri1_edge1.setNextEdge(tri1_edge3, triangle1.Id);
			tri1_edge1.setPreviousEdge(tri2_edge2, triangle1.Id);

			tri1_edge2.setParentShape(triangle2);
			tri1_edge2.setNextEdge(tri2_edge1, triangle2.Id);
			tri1_edge2.setPreviousEdge(tri2_edge3, triangle2.Id);

			tri1_edge3.setNextEdge(tri2_edge2, triangle1.Id);
			tri1_edge3.setPreviousEdge(tri1_edge1, triangle1.Id);

			tri2_edge1.changeTarget(tri2_edge2.pointsTo, triangle2.Id);
			tri2_edge1.setNextEdge(tri2_edge3, triangle2.Id);
			tri2_edge1.setPreviousEdge(tri1_edge2, triangle2.Id);

			tri2_edge2.setParentShape(triangle1);
			tri2_edge2.setNextEdge(tri1_edge1, triangle1.Id);
			tri2_edge2.setPreviousEdge(tri1_edge3, triangle1.Id);

			tri2_edge3.setNextEdge(tri1_edge2, triangle2.Id);
			tri2_edge3.setPreviousEdge(tri2_edge1, triangle2.Id);

			// Make sure we don't delete any firstEdge connection.
			triangle1.checkAndSetEdgeList(tri1_edge3);
			triangle2.checkAndSetEdgeList(tri2_edge1);
		}

		/// <summary>
		/// Remove remnant triangles from the supertriangle.
		/// </summary>
		static void RemoveSuperTriangle(Polygon superTriangle, (HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) delaunayData) {
			//The super triangle doesnt exists anymore because we have split it into many new triangles
			//But we can use its vertices to figure out which new triangles (or faces belonging to the triangle) 
			//we should delete

			HashSet<Polygon> triangleFacesToDelete = new HashSet<Polygon>();

			//Loop through all vertices belongin to the triangulation
			foreach (Polygon triangle in delaunayData.triangles.Values) {
				//If the face attached to this vertex already exists in the list of faces we want to delete
				//Then dont add it again
				if (triangleFacesToDelete.Contains(triangle)) {
					continue;
				}

				/// check to see if any of the triangle vertexes match the super triangle
				triangle.forEachVertexUntil(vertex => {
					bool @continue = true;
					superTriangle.forEachVertexUntil(superTriangleVertex => {
						// if so, add it to the triangles to remove
						if (superTriangleVertex.Equals(vertex)) {
							triangleFacesToDelete.Add(triangle);
							@continue = false;
							return false;
						}

						return true;
					});

					return @continue;
				});
			}

			//Delete the new triangles with vertices attached to the super triangle
			foreach (Polygon triangle in triangleFacesToDelete) {
				DeletePolygon(triangle, delaunayData, true);
			}
		}

		/// <summary>
		/// From "A fast algortihm for generating constrained delaunay..."
		/// Is numerically stable
		/// v1, v2 should belong to the edge we ant to flip
		/// v1, v2, v3 are counter-clockwise
		/// Is also checking if the edge can be swapped
		/// </summary>
		static bool ShouldFlipTriangleEdgeForDelaunay(Vertex v1_flipEdgeStart, Vertex v2_FlipEdgeEnd, Vertex v3_NextPointCounterClockwise, Vertex referencePoint) {
			float x_13 = v1_flipEdgeStart.x - v3_NextPointCounterClockwise.x;
			float x_23 = v2_FlipEdgeEnd.x - v3_NextPointCounterClockwise.x;
			float x_1p = v1_flipEdgeStart.x - referencePoint.x;
			float x_2p = v2_FlipEdgeEnd.x - referencePoint.x;

			float y_13 = v1_flipEdgeStart.y - v3_NextPointCounterClockwise.y;
			float y_23 = v2_FlipEdgeEnd.y - v3_NextPointCounterClockwise.y;
			float y_1p = v1_flipEdgeStart.y - referencePoint.y;
			float y_2p = v2_FlipEdgeEnd.y - referencePoint.y;

			float cos_a = x_13 * x_23 + y_13 * y_23;
			float cos_b = x_2p * x_1p + y_2p * y_1p;

			if (cos_a >= 0f && cos_b >= 0f) {
				return false;
			}
			if (cos_a < 0f && cos_b < 0) {
				return true;
			}

			float sin_ab = (x_13 * y_23 - x_23 * y_13) * cos_b + (x_2p * y_1p - x_1p * y_2p) * cos_a;

			if (sin_ab < 0) {
				return true;
			}

			return false;
		}

		#region Triangulation Walk Requirements

		enum LeftOnRight { Left, On, Right }

		//Help method to make code smaller
		//Is p to the right or on the line a-b
		static bool IsPointToTheRightOrOnLine(Vector2 a, Vector2 b, Vector2 p) {
			bool isToTheRight = false;

			LeftOnRight pointPos = GetPointPositionComparedToVector(a, b, p);

			if (pointPos == LeftOnRight.Right || pointPos == LeftOnRight.On) {
				isToTheRight = true;
			}

			return isToTheRight;
		}

		/// <summary>
		/// Get if a value is on the line or to the left or right of it
		/// </summary>
		static LeftOnRight GetPointPositionComparedToVector(Vector2 a, Vector2 b, Vector2 p) {
			float relationValue = GetPointInRelationToVectorValue(a, b, p);

			//To avoid floating point precision issues we can add a small value
			float epsilon = Mathf.Epsilon;

			//To the right
			if (relationValue < -epsilon) {
				return LeftOnRight.Right;
			}
			//To the left
			else if (relationValue > epsilon) {
				return LeftOnRight.Left;
			}
			//= 0 -> on the line
			else {
				return LeftOnRight.On;
			}
		}

		/// <summary>
		/// Get a point in relation to the vector values
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="p"></param>
		/// <returns></returns>
		static float GetPointInRelationToVectorValue(Vector2 a, Vector2 b, Vector2 p) {
			float x1 = a.x - p.x;
			float x2 = a.y - p.y;
			float y1 = b.x - p.x;
			float y2 = b.y - p.y;

			float determinant = GetDeterminant(x1, x2, y1, y2);

			return determinant;
		}

		/// <summary>
		/// // Returns the determinant of the 2x2 matrix defined as
		/// | x1 x2 |
		/// | y1 y2 |
		/// </summary>
		/// <returns></returns>
		static float GetDeterminant(float x1, float x2, float y1, float y2) {
			return (x1 * y2 - y1 * x2);
		}

		/// <summary>
		/// Calculate the center of a circle using 3 points on it's circumfrence
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		/// <returns></returns>
		static Vertex CalculateTriangleCircumcenter(Polygon triangle, Dictionary<Vertex, Dictionary<Vertex, Dictionary<Vertex, Vertex>>> circumcenterCache) {
			Vertex a = triangle.firstEdge.pointsTo;
			Vertex b = triangle.firstEdge.nextEdge.pointsTo;
			Vertex c = triangle.firstEdge.nextEdge.nextEdge.pointsTo;

			/// first lets check if we already calculated this circumcenter
			List<Vertex> remainingVerticesToBeFound = new List<Vertex> {
				a,b,c
			};

			// for each of the 3 values, we need to check the first cache layer
			for (int index = 0; index < remainingVerticesToBeFound.Count; index++) {
				if (circumcenterCache.TryGetValue(remainingVerticesToBeFound[index], out var cacheLayer2)) {
					// if we find one on the first layer (X), remove it from the remaining list, then search the remaining two
					remainingVerticesToBeFound.RemoveAt(index);
					for (int index2 = 0; index2 < remainingVerticesToBeFound.Count; index2++) {
						if (cacheLayer2.TryGetValue(remainingVerticesToBeFound[index2], out var finalCacheLayer)) {
							// if we find it in the second cache layer, we just need to remove the value we found, and check if the last one matches.
							remainingVerticesToBeFound.RemoveAt(index2);
							if (finalCacheLayer.TryGetValue(remainingVerticesToBeFound[0], out Vertex cachedCircumcenter)) {
								return cachedCircumcenter;
							} else {
								break;
							}
						}
					}

					// if we didn't find any of the remaining ones in the second layer we're done.
					break;
				}
			}

			float ma = (b.y - a.y) / (b.x - a.x);
			float mb = (c.y - b.y) / (c.x - b.x);

			float centerX = (ma * mb * (a.y - c.y) + mb * (a.x + b.x) - ma * (b.x + c.x)) / (2 * (mb - ma));
			float centerY = (-1f / ma) * (centerX - (a.x + b.x) / 2f) + (a.y + b.y) / 2f;

			Vertex circumcenter = (centerX, centerY);

			/// Add the value to the cache
			if (!circumcenterCache.ContainsKey(a)) {
				circumcenterCache[a] = new Dictionary<Vertex, Dictionary<Vertex, Vertex>> { { b, new Dictionary<Vertex, Vertex>() { { c, circumcenter } } } };
			} else if (!circumcenterCache[a].ContainsKey(b)) {
				circumcenterCache[a][b] = new Dictionary<Vertex, Vertex>() { { c, circumcenter } };
			} else {
				circumcenterCache[a][b][c] = circumcenter;
			}


			return circumcenter;
		}

		#endregion

		#endregion

	}
}