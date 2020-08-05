using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//https://gamedev.stackexchange.com/questions/71328/how-can-i-add-and-subtract-convex-polygons
namespace Evix.Terrain.DataGeneration.Voronoi {
	public static class Delaunay {

		/// <summary>
		/// Epsilon value for floating point comparison
		/// </summary>
		const float Epsilon = 0.5f;

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
				new EdgeVector((-20000000, 20000000)),
				new EdgeVector((20000000, 20000000)),
				new EdgeVector((0, -25000000))
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
				while (trianglesToInvestigate.Count > 0 && safetyCounter-- > 0) {
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
				}
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
			foreach (Vertex delaunayVertex in delaunayData.vertices) {
				// make the new cell
				Polygon voronoiCell = new Polygon(delaunayVertex);

				// these are edges we create that then need to be hooked up to an edge pointing at their start point. (as prev)
				//    indexed by the needed start point, or the would be edge.prevEdge.pointsTo
				Dictionary<Vertex, EdgeVector> incompleEdgesByNeededOriginPoint = new Dictionary<Vertex, EdgeVector>();
				// these are edges that have been hooked up to their prev (start) neighbor already, and can be used to hook up straglers.
				//    indexed by the pointsTo of the completeEdge.
				Dictionary<Vertex, EdgeVector> completeEdges = new Dictionary<Vertex, EdgeVector>();

				// we'll use outpointing vectors to check each line once, as each vector radiating
				//    from this point should have both sides. (in and outgoing) for the multuple triangles.
				delaunayVertex.forEachOutgoingVector((outgoingVector, polygonID) => {
					Polygon forwardTriangle = outgoingVector.parentShape;
					Polygon behindTriangle = outgoingVector.oppositeEdge?.parentShape;

					if (forwardTriangle != null && behindTriangle != null) {
						// Get the two voronoi points we use to create our new edge (and index it by what we want to hook it up to)
						Vertex newEdgePointsTo = CalculateTriangleCircumcenter(forwardTriangle, circumcenterCache);
						Vertex newEdgeOriginPoint = CalculateTriangleCircumcenter(behindTriangle, circumcenterCache);

						// make out new edge for this ray.
						EdgeVector newEdge = new EdgeVector(newEdgePointsTo);
						newEdge.setParentShape(voronoiCell);
						voronoiCell.checkAndSetEdgeList(newEdge);

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
					}
				});

				/// Loop over the still incomplete edges and try to connect them.
				//  This can take multiple runs through if edges rely on incomplete edges, but shouldn't take more than the sides possible,
				//// so we use a safety limit for the loop
				int safetyCount = 10;
				while (incompleEdgesByNeededOriginPoint.Count > 0 && safetyCount-- > 0) {
					var itemsToRemove = incompleEdgesByNeededOriginPoint.Where((i) => {
						// if we find the complete edge we're looking for hook it up and return it for the list to remove.
						var (incompleteEdgeNeededOrigin, incompleteEdge) = i;
						if (completeEdges.TryGetValue(incompleteEdgeNeededOrigin, out EdgeVector originPointingVector)) {
							// grab it and set it correctly.
							incompleteEdge.setPreviousEdge(originPointingVector, voronoiCell.Id);
							originPointingVector.setNextEdge(incompleteEdge, voronoiCell.Id);
							return true;
						}

						return false;
					}).ToArray();

					foreach (var (completedEdgeKey, completedEdgeToAdd) in itemsToRemove) {
						// remove the newly completed edges from the incomplete edge hash to the complete one.
						incompleEdgesByNeededOriginPoint.Remove(completedEdgeKey);
						completeEdges[completedEdgeToAdd.pointsTo] = completedEdgeToAdd;
					}
				}

				/// get the edge count
				if (!voronoiCell.isEmpty && voronoiCell.checkIsComplete()) {
					voronoiCell.countEdges();
					voronoiCells[delaunayVertex] = voronoiCell;
				} else {
					//World.Debug.log($"Cell #{voronoiCell.Id} failed to be created: {(voronoiCell.isEmpty ? "isEmpty" : "isIncomplete")}");
				}
			}

			return voronoiCells;
		}

		#endregion

		#region Helper Functions

		/// <summary>
		/// Get if the line intersects the bounds of the given rectangle.
		/// </summary>
		/// <param name="rectangleBounds">min inclusive, max exclusive</param>
		/// <param name="line"></param>
		/// <returns></returns>
		public static bool LineIntersectsRectangle((Coordinate min, Coordinate max) rectangleBounds, (Coordinate a, Coordinate b) line) {
			/// Return true if all the points are in the square
			if (line.a.isWithin(rectangleBounds) && line.b.isWithin(rectangleBounds)) {
				return true;
			}

			// if both points are beyond the square in the same direction, return false.
			if ((line.a.x >= rectangleBounds.max.x 
					&& line.b.x >= rectangleBounds.max.x)
				|| (line.a.y >= rectangleBounds.max.y
					&& line.b.y >= rectangleBounds.max.y)
				|| (line.a.x < rectangleBounds.min.x 
					&& line.b.x < rectangleBounds.min.x)
				|| (line.a.y < rectangleBounds.min.y
					&& line.b.y < -rectangleBounds.min.y)
			) {
				return false;
			}

			/// Return true if one side intersects
			Coordinate a = (rectangleBounds.min.x, rectangleBounds.max.y);
			Coordinate b = rectangleBounds.max;
			Coordinate c = (rectangleBounds.min.x, rectangleBounds.min.y);
			Coordinate d = rectangleBounds.min;

			(Vertex a, Vertex b)[] squareSides = new (Vertex a, Vertex b)[] {
				(a, b),
				(b, c),
				(c, d),
				(d, a)
			};

			foreach((Vertex a, Vertex b) squareSide in squareSides) {
				if (LinesIntersect(squareSide, line)) {
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Check if two lines intersect
		/// </summary>
		public static bool LinesIntersect((Vertex a, Vertex b) lineA, (Vertex a, Vertex b) lineB) {
			float s1_x = lineA.b.x - lineA.a.x;
			float s1_y = lineA.b.y - lineA.a.y;
			float s2_x = lineB.b.x - lineB.a.x;
			float s2_y = lineB.b.y - lineB.a.y;

			float s = (-s1_y * (lineA.a.x - lineB.a.x) + s1_x * (lineA.a.y - lineB.a.y)) / (-s2_x * s1_y + s1_x * s2_y);
			float t = (s2_x * (lineA.a.y - lineB.a.y) - s2_y * (lineA.a.x - lineB.a.x)) / (-s2_x * s1_y + s1_x * s2_y);

			return s >= 0 && s <= 1 && t >= 0 && t <= 1;
		}

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

			/// update the triangle parents of edges and points that change
			tri1_edge2.setParentShape(triangle2);
			tri2_edge2.setParentShape(triangle1);
			tri1_edge3.pointsTo.removeParentShape(triangle2.Id);
			tri2_edge3.pointsTo.removeParentShape(triangle1.Id);

			// update targets of the two rotated vectors
			tri1_edge1.changeTarget(tri1_edge2.pointsTo, triangle1.Id);
			tri2_edge1.changeTarget(tri2_edge2.pointsTo, triangle2.Id);

			/// Update associations

			// 1
			tri1_edge1.setNextEdge(tri1_edge3, triangle1.Id);
			tri1_edge1.setPreviousEdge(tri2_edge2, triangle1.Id);

			// 2
			tri1_edge2.setNextEdge(tri2_edge1, triangle2.Id);
			tri1_edge2.setPreviousEdge(tri2_edge3, triangle2.Id);

			// 3
			tri1_edge3.setNextEdge(tri2_edge2, triangle1.Id);
			tri1_edge3.setPreviousEdge(tri1_edge1, triangle1.Id);

			//4
			tri2_edge1.setNextEdge(tri2_edge3, triangle2.Id);
			tri2_edge1.setPreviousEdge(tri1_edge2, triangle2.Id);

			//5
			tri2_edge2.setNextEdge(tri1_edge1, triangle1.Id);
			tri2_edge2.setPreviousEdge(tri1_edge3, triangle1.Id);

			//6
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
			/// Get the triangle's A B and C
			var (a, (b, (c, _))) = triangle;

			/// first lets check if we already calculated this circumcenter
			// keep a list of the cached verts of the triangle we haven't found yet.
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

			/// get the slopes. We need to make sure the 2 slopes we pick are not infinite or 0
			// Slope of AB
			float ab_slope = (b.y - a.y) / (b.x - a.x);
			// Slope of BC
			float bc_slope = (c.y - b.y) / (c.x - b.x);

			// if we detect one of the slopes we calculated is -infinity or 0 (is strait on the grid), rotate some points and try again
			if (ab_slope == 0 || float.IsInfinity(ab_slope)) {
				Vertex temp = a;
				a = c;
				c = temp;

				// Slope of AB
				ab_slope = (b.y - a.y) / (b.x - a.x);
				// Slope of BC
				bc_slope = (c.y - b.y) / (c.x - b.x);
			}

			//// Note: it's rare but sometimes we only need to swap once.
			if (bc_slope == 0 || float.IsInfinity(bc_slope)) {
				Vertex temp = b;
				b = a;
				a = temp;

				// Slope of AB
				ab_slope = (b.y - a.y) / (b.x - a.x);
				// Slope of BC
				bc_slope = (c.y - b.y) / (c.x - b.x);
			}

			/// get the circumcenter from the slopes
			Vertex circumcenter;

			/// if one of the slopes is still strait then we probably have a rigth triangle aligned with the grid
			//    This is true if two of the slopes out of 3 are strait lines.
			bool ab_slope_isStrait = ab_slope == 0 || float.IsInfinity(ab_slope);
			bool bc_slope_isStrait = bc_slope == 0 || float.IsInfinity(bc_slope);
			if (ab_slope_isStrait || bc_slope_isStrait) {
				// we need the hypotenuse if this is a right triangle angled with the grid:
				(Vertex a, Vertex b) hypotenuse;

				// if the two slopes we have now are not both strait, calculate the third slope
				if (!(ab_slope_isStrait && bc_slope_isStrait)) {
					float ca_slope = (c.y - a.y) / (c.x - a.x);
					// if the third slope isn't strait, something is up; we only have one strait slope and this isn't a right triangle aligned to grid.
					if (ca_slope != 0 && !float.IsInfinity(ca_slope)) {
						World.Debug.logAndThrowError<ArgumentOutOfRangeException>($"Danger! Calculated circumcenter divided by zero");
					}

					// the hypotenuse is the line that had a slope that wasn't strait
					hypotenuse = ab_slope_isStrait ? (b, c) : (a, b);
					// if both ab and bc are strait lines, ac must be the hypotenuse
				} else {
					hypotenuse = (a, c);
				}

				// The circumcenter is the midpoint of the hypotenuse
				circumcenter = ((hypotenuse.a.x + hypotenuse.b.x) / 2, (hypotenuse.a.y + hypotenuse.b.y) / 2);
			// if the slopes are both fine, use them to calculare the center
			} else {
				float centerX = (ab_slope * bc_slope * (a.y - c.y) + bc_slope * (a.x + b.x) - ab_slope * (b.x + c.x)) / (2 * (bc_slope - ab_slope));
				float centerY = (-1f / ab_slope) * (centerX - (a.x + b.x) / 2f) + (a.y + b.y) / 2f;

				circumcenter = (centerX, centerY);
			}

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

		/// <summary>
		/// Check if it's a right triangle, and get the hypotonose if it is
		/// </summary>
		/// <param name="triangle"></param>
		/// <param name="hypotenuse"></param>
		/// <returns></returns>
		static bool IsRightTriangle(Polygon triangle, out (Vertex a, Vertex b) hypotenuse) {
			/// Get the triangle's 3 verticies
			var (e, (f, (g, _))) = triangle;

			// calculate the side lengths
			(float length, Vertex a, Vertex b)[] sides = new (float, Vertex, Vertex)[] {
				(Vector2.Distance(e.position, f.position), e , f),
				(Vector2.Distance(f.position, g.position), f , g),
				(Vector2.Distance(g.position, e.position), g , e)
			};

			// sort them, smallest first
			sides = sides.OrderBy(side => side.length).ToArray();

			// the longest side is the hypotenuse if it's a right triangle.
			hypotenuse = (sides[2].a, sides[2].b);

			/// A squared + b squared = c squared on a right triangle.
			double a_squared = Math.Pow(sides[0].length, 2);
			double b_squared = Math.Pow(sides[1].length, 2);
			double c_squared = Math.Pow(sides[2].length, 2);

			return Math.Abs(a_squared + b_squared - c_squared) < Epsilon;
		}

		#endregion

		#endregion

	}
}
