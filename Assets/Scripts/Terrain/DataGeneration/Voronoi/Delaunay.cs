using System;
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
			(HashSet<Vertex> vertices, Dictionary<int, Polygon> triangles) delaunayData = (new HashSet<Vertex>(), new Dictionary<int, Polygon>());

			/// Step 1. Create the super triangle to surround the area.
			// The super triangle should be bigger than any other thing, and contain all points by a large margin.
			Polygon superTriangle = new Polygon(EdgeVector.MakedLinkedShape(new List<EdgeVector> {
				new EdgeVector((-1000000, 1000000)),
				new EdgeVector((1000000, 1000000)),
				new EdgeVector((0, 1000000))
			}));

			delaunayData.triangles.Add(superTriangle.Id, superTriangle);
			superTriangle.forEachVertex(vertex => delaunayData.vertices.Add(vertex));

			/// Step 2. Go through each point and add it into the existing triangulation
			foreach (Vertex pointToAdd in points) {
				// 2a. Add the new vert to our hashmap data
				delaunayData.vertices.Add(pointToAdd);

				// 2b. try to get an existing triangle that's around this point
				Polygon surroundingTriangle = TriangulationWalk(pointToAdd, delaunayData.triangles);
				// 2c. Try to split that triangle using the point we want to add
				if (surroundingTriangle != null) {
					SplitTriangleAtPoint(surroundingTriangle, pointToAdd, delaunayData);
				}

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


			return delaunayData;
		}

		#endregion

		#region Helper Functions

		static Polygon TriangulationWalk(Vertex point, Dictionary<int, Polygon> triangles) {
			/// declare return
			Polygon intersectingTriangle = null;

			/// Get a random start triangle
			Polygon currentTriangle = triangles[UnityEngine.Random.Range(0, triangles.Count)];

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
			DeletePolygon(shape, delaunayData, false);
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
		static void DeletePolygon(Polygon polygonToDelete, (HashSet<Vertex> vertices, Dictionary<int, Polygon> shapes) delaunayData, bool shouldSetOppositeToNull = true) {
			
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
		/// 
		/// </summary>
		/// <param name="triangleBaseToTest"></param>
		private static void FlipTriangleEdge(EdgeVector triangleBaseToTest) {
			Polygon triangle = triangleBaseToTest.parentShape;
			Polygon oppositeTriangle = triangleBaseToTest.oppositeEdge.parentShape;

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

		#endregion

		#endregion


		/*#region Trianglulation Old

		public static List<Polygon> GenerateVoronoiDiagram(List<Vector2> sites) {
			//First generate the delaunay triangulation
			List<Triangle> triangles = TriangulateByFlippingEdges(sites);

			//Generate the voronoi diagram

			//Step 1. For every delaunay edge, compute a voronoi edge
			//The voronoi edge is the edge connecting the circumcenters of two neighboring delaunay triangles
			List<Edge> voronoiEdges = new List<Edge>();

			for (int i = 0; i < triangles.Count; i++) {
				Triangle t = triangles[i];

				//Each triangle consists of these edges
				HalfEdge e1 = t.halfEdge;
				HalfEdge e2 = e1.nextEdge;
				HalfEdge e3 = e2.nextEdge;

				//Calculate the circumcenter for this triangle
				Vector2 v1 = e1.pointsTo.position;
				Vector2 v2 = e2.pointsTo.position;
				Vector2 v3 = e3.pointsTo.position;

				//The circumcenter is the center of a circle where the triangles corners is on the circumference of that circle
				Vector2 center2D = CalculateCircleCenter(v1, v2, v3);

				//The circumcenter is also known as a voronoi vertex, which is a position in the diagram where we are equally
				//close to the surrounding sites
				Vector2 voronoiVertex = new Vector2(center2D.x, center2D.y);

				TryAddVoronoiEdgeFromTriangleEdge(e1, voronoiVertex, voronoiEdges);
				TryAddVoronoiEdgeFromTriangleEdge(e2, voronoiVertex, voronoiEdges);
				TryAddVoronoiEdgeFromTriangleEdge(e3, voronoiVertex, voronoiEdges);
			}

			//Step 2. Find the voronoi cells where each cell is a list of all edges belonging to a site
			List<Polygon> voronoiCells = new List<Polygon>();

			for (int i = 0; i < voronoiEdges.Count; i++) {
				Edge e = voronoiEdges[i];

				//Find the position in the list of all cells that includes this site
				int cellPos = TryFindCellPos(e, voronoiCells);

				//No cell was found so we need to create a new cell
				if (cellPos == -1) {
					Polygon newCell = new Polygon(e.polygonCenter);

					voronoiCells.Add(newCell);

					newCell.edges.Add(e);
				} else {
					voronoiCells[cellPos].edges.Add(e);
				}
			}


			return voronoiCells;
		}

		/// <summary>
		/// Triangulate with some algorithm - then flip edges until we have a delaunay triangulation
		/// </summary>
		public static List<Triangle> TriangulateByFlippingEdges(List<Vector2> sites) {
			//Step 1. Triangulate the points with some algorithm
			//Vector3 to vertex
			List<Vertex> vertices = new List<Vertex>();

			for (int i = 0; i < sites.Count; i++) {
				vertices.Add(new Vertex(sites[i]));
			}

			//Triangulate the convex hull of the sites
			List<Triangle> triangles = IncrementalTriangulation(vertices);

			//Step 2. Change the structure from triangle to half-edge to make it faster to flip edges
			List<HalfEdge> halfEdges = TransformFromTriangleToHalfEdge(triangles);

			//Step 3. Flip edges until we have a delaunay triangulation
			int flippedEdges = 0;
			int safetyTimer = 0;

			while (true) {
				safetyTimer += 1;
				if (safetyTimer > 100000) {
					World.Debug.logAndThrowError<StackOverflowException>($"Delaunay.TriangulateByFlippingEdges caught in endless loop");

					break;
				}

				bool hasFlippedEdge = false;

				//Search through all edges to see if we can flip an edge
				for (int i = 0; i < halfEdges.Count; i++) {
					HalfEdge thisEdge = halfEdges[i];

					//Is this edge sharing an edge, otherwise its a border, and then we cant flip the edge
					if (thisEdge.oppositeEdge == null) {
						continue;
					}

					//The vertices belonging to the two triangles, c-a are the edge vertices, b belongs to this triangle
					Vertex a = thisEdge.pointsTo;
					Vertex b = thisEdge.nextEdge.pointsTo;
					Vertex c = thisEdge.prevEdge.pointsTo;
					Vertex d = thisEdge.oppositeEdge.nextEdge.pointsTo;

					//Use the circle test to test if we need to flip this edge
					if (IsPointInsideOutsideOrOnCircle(a, b, c, d) < 0f) {
						//Are these the two triangles that share this edge forming a convex quadrilateral?
						//Otherwise the edge cant be flipped
						if (IsQuadrilateralConvex(a, b, c, d)) {
							//If the new triangle after a flip is not better, then dont flip
							//This will also stop the algoritm from ending up in an endless loop
							if (IsPointInsideOutsideOrOnCircle(b, c, d, a) < 0f) {
								continue;
							}

							//Flip the edge
							flippedEdges += 1;

							hasFlippedEdge = true;

							thisEdge.flipEdgeForTriangle();
						}
					}
				}

				//We have searched through all edges and havent found an edge to flip, so we have a Delaunay triangulation!
				if (!hasFlippedEdge) {

					break;
				}
			}

			//Dont have to convert from half edge to triangle because the algorithm will modify the objects, which belongs to the 
			//original triangles, so the triangles have the data we need

			return triangles;
		}

		/// <summary>
		/// Preform a simple incremental triangulation
		/// </summary>
		/// <param name="points"></param>
		/// <returns></returns>
		static List<Triangle> IncrementalTriangulation(List<Vertex> points) {
			List<Triangle> triangles = new List<Triangle>();

			//Sort the points along x-axis
			//OrderBy is always soring in ascending order - use OrderByDescending to get in the other order
			points = points.OrderBy(n => n.position.x).ToList();

			//The first 3 vertices are always forming a triangle
			Triangle newTriangle = new Triangle(points[0].position, points[1].position, points[2].position);

			triangles.Add(newTriangle);

			//All edges that form the triangles, so we have something to test against
			List<Edge> edges = new List<Edge>();

			edges.Add(new Edge(newTriangle.v1, newTriangle.v2));
			edges.Add(new Edge(newTriangle.v2, newTriangle.v3));
			edges.Add(new Edge(newTriangle.v3, newTriangle.v1));

			//Add the other triangles one by one
			//Starts at 3 because we have already added 0,1,2
			for (int i = 3; i < points.Count; i++) {
				Vector2 currentPoint = points[i].position;

				//The edges we add this loop or we will get stuck in an endless loop
				List<Edge> newEdges = new List<Edge>();

				//Is this edge visible? We only need to check if the midpoint of the edge is visible 
				for (int j = 0; j < edges.Count; j++) {
					Edge currentEdge = edges[j];

					Vector2 midPoint = (currentEdge.start.position + currentEdge.end.position) / 2f;

					Edge edgeToMidpoint = new Edge(currentPoint, midPoint);

					//Check if this line is intersecting
					bool canSeeEdge = true;

					for (int k = 0; k < edges.Count; k++) {
						//Dont compare the edge with itself
						if (k == j) {
							continue;
						}

						if (AreEdgesIntersecting(edgeToMidpoint, edges[k])) {
							canSeeEdge = false;

							break;
						}
					}

					//This is a valid triangle
					if (canSeeEdge) {
						Edge edgeToPoint1 = new Edge(currentEdge.start, new Vertex(currentPoint));
						Edge edgeToPoint2 = new Edge(currentEdge.end, new Vertex(currentPoint));

						newEdges.Add(edgeToPoint1);
						newEdges.Add(edgeToPoint2);

						Triangle newTri = new Triangle(edgeToPoint1.start, edgeToPoint1.end, edgeToPoint2.start);

						triangles.Add(newTri);
					}
				}


				for (int j = 0; j < newEdges.Count; j++) {
					edges.Add(newEdges[j]);
				}
			}


			return triangles;
		}

		#endregion

		#region Geometry Functions

		/// <summary>
		/// Are the two lines made of the points intersecting?
		/// </summary>
		/// <returns></returns>
		static bool AreLinesIntersecting(Vector2 l1_p1, Vector2 l1_p2, Vector2 l2_p1, Vector2 l2_p2, bool shouldIncludeEndPoints) {
			bool isIntersecting = false;

			float denominator = (l2_p2.y - l2_p1.y) * (l1_p2.x - l1_p1.x) - (l2_p2.x - l2_p1.x) * (l1_p2.y - l1_p1.y);

			//Make sure the denominator is > 0, if not the lines are parallel
			if (denominator != 0f) {
				float u_a = ((l2_p2.x - l2_p1.x) * (l1_p1.y - l2_p1.y) - (l2_p2.y - l2_p1.y) * (l1_p1.x - l2_p1.x)) / denominator;
				float u_b = ((l1_p2.x - l1_p1.x) * (l1_p1.y - l2_p1.y) - (l1_p2.y - l1_p1.y) * (l1_p1.x - l2_p1.x)) / denominator;

				//Are the line segments intersecting if the end points are the same
				if (shouldIncludeEndPoints) {
					//Is intersecting if u_a and u_b are between 0 and 1 or exactly 0 or 1
					if (u_a >= 0f && u_a <= 1f && u_b >= 0f && u_b <= 1f) {
						isIntersecting = true;
					}
				} else {
					//Is intersecting if u_a and u_b are between 0 and 1
					if (u_a > 0f && u_a < 1f && u_b > 0f && u_b < 1f) {
						isIntersecting = true;
					}
				}

			}

			return isIntersecting;
		}

		/// <summary>
		/// Check if the edges are intersecting
		/// </summary>
		/// <param name="edge1"></param>
		/// <param name="edge2"></param>
		/// <returns></returns>
		static bool AreEdgesIntersecting(Edge edge1, Edge edge2) {
			Vector2 l1_p1 = new Vector2(edge1.start.position.x, edge1.start.position.y);
			Vector2 l1_p2 = new Vector2(edge1.end.position.x, edge1.end.position.y);

			Vector2 l2_p1 = new Vector2(edge2.start.position.x, edge2.start.position.y);
			Vector2 l2_p2 = new Vector2(edge2.end.position.x, edge2.end.position.y);

			bool isIntersecting = AreLinesIntersecting(l1_p1, l1_p2, l2_p1, l2_p2, true);

			return isIntersecting;
		}

		/// <summary>
		///Is a point d inside, outside or on the same circle as a, b, c
		/// </summary>
		/// <returns>Positive if inside, negative if outside, and 0 if on the circle</returns>
		static float IsPointInsideOutsideOrOnCircle(Vector2 pointA, Vector2 pointB, Vector2 pointC, Vector2 pointD) {
			//This first part will simplify how we calculate the determinant
			float a = pointA.x - pointD.x;
			float d = pointB.x - pointD.x;
			float g = pointC.x - pointD.x;

			float b = pointA.y - pointD.y;
			float e = pointB.y - pointD.y;
			float h = pointC.y - pointD.y;

			float c = a * a + b * b;
			float f = d * d + e * e;
			float i = g * g + h * h;

			float determinant = (a * e * i) + (b * f * g) + (c * d * h) - (g * e * c) - (h * f * a) - (i * d * b);

			return determinant;
		}

		/// <summary>
		/// Is a quadrilateral convex? Assume no 3 points are colinear and the shape doesnt look like an hourglass
		/// </summary>
		static bool IsQuadrilateralConvex(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
			bool isConvex = false;

			bool abc = IsTriangleOrientedClockwise(a, b, c);
			bool abd = IsTriangleOrientedClockwise(a, b, d);
			bool bcd = IsTriangleOrientedClockwise(b, c, d);
			bool cad = IsTriangleOrientedClockwise(c, a, d);

			if (abc && abd && bcd & !cad) {
				isConvex = true;
			} else if (abc && abd && !bcd & cad) {
				isConvex = true;
			} else if (abc && !abd && bcd & cad) {
				isConvex = true;
			}
				//The opposite sign, which makes everything inverted
				else if (!abc && !abd && !bcd & cad) {
				isConvex = true;
			} else if (!abc && !abd && bcd & !cad) {
				isConvex = true;
			} else if (!abc && abd && !bcd & !cad) {
				isConvex = true;
			}


			return isConvex;
		}

		/// <summary>
		/// Calculate a cicle center given 3 points
		/// </summary>
		/// <returns></returns>
	 static Vector2 CalculateCircleCenter(Vector2 p1, Vector2 p2, Vector2 p3) {
			Vector2 center = new Vector2();

			float ma = (p2.y - p1.y) / (p2.x - p1.x);
			float mb = (p3.y - p2.y) / (p3.x - p2.x);

			center.x = (ma * mb * (p1.y - p3.y) + mb * (p1.x + p2.x) - ma * (p2.x + p3.x)) / (2 * (mb - ma));

			center.y = (-1 / ma) * (center.x - (p1.x + p2.x) / 2) + (p1.y + p2.y) / 2;

			return center;
		}

		#region Triangle Manipulation

		/// <summary>
		/// From triangle where each triangle has one vertex to half edge
		/// </summary>
		public static List<HalfEdge> TransformFromTriangleToHalfEdge(List<Triangle> triangles) {
			//Make sure the triangles have the same orientation
			OrientTrianglesClockwise(triangles);

			//First create a list with all possible half-edges
			List<HalfEdge> halfEdges = new List<HalfEdge>(triangles.Count * 3);

			for (int i = 0; i < triangles.Count; i++) {
				Triangle t = triangles[i];

				HalfEdge he1 = new HalfEdge(t.v1);
				HalfEdge he2 = new HalfEdge(t.v2);
				HalfEdge he3 = new HalfEdge(t.v3);

				he1.nextEdge = he2;
				he2.nextEdge = he3;
				he3.nextEdge = he1;

				he1.prevEdge = he3;
				he2.prevEdge = he1;
				he3.prevEdge = he2;

				//The vertex needs to know of an edge going from it
				he1.pointsTo.outgoingEdge = he2;
				he2.pointsTo.outgoingEdge = he3;
				he3.pointsTo.outgoingEdge = he1;

				//The face the half-edge is connected to
				t.halfEdge = he1;

				he1.parentShape= t;
				he2.parentShape = t;
				he3.parentShape = t;

				//Add the half-edges to the list
				halfEdges.Add(he1);
				halfEdges.Add(he2);
				halfEdges.Add(he3);
			}

			//Find the half-edges going in the opposite direction
			for (int i = 0; i < halfEdges.Count; i++) {
				HalfEdge he = halfEdges[i];

				Vertex goingToVertex = he.pointsTo;
				Vertex goingFromVertex = he.prevEdge.pointsTo;

				for (int j = 0; j < halfEdges.Count; j++) {
					//Dont compare with itself
					if (i == j) {
						continue;
					}

					HalfEdge heOpposite = halfEdges[j];

					//Is this edge going between the vertices in the opposite direction
					if (goingFromVertex.position == heOpposite.pointsTo.position && goingToVertex.position == heOpposite.prevEdge.pointsTo.position) {
						he.oppositeEdge = heOpposite;

						break;
					}
				}
			}


			return halfEdges;
		}

    /// <summary>
    /// Orient all the given triangles clockwise
    /// </summary>
    /// <param name="triangles"></param>
    /// <returns></returns>
    static List<Triangle> OrientTrianglesClockwise(List<Triangle> triangles) {
			for (int i = 0; i < triangles.Count; i++) {
				Triangle t = triangles[i];

				if (!IsTriangleOrientedClockwise(t.v1, t.v2, t.v3)) {
					t.changeOrientation();

					triangles[i] = t;
				}
			}

			return triangles;
		}


		/// <summary>
		// Is a triangle in 2d space oriented clockwise or counter-clockwise
		///
	 static bool IsTriangleOrientedClockwise(Vector2 p1, Vector2 p2, Vector2 p3) {
			bool isClockWise = true;

			float determinant = p1.x * p2.y + p3.x * p1.y + p2.x * p3.y - p1.x * p3.y - p3.x * p2.y - p2.x * p1.y;

			if (determinant > 0f) {
				isClockWise = false;
			}

			return isClockWise;
		}

		#endregion

		#endregion

		#region Voronoi Helper Functions


		//Find the position in the list of all cells that includes this site
		//Returns -1 if no cell is found
		static int TryFindCellPos(Edge e, List<Polygon> voronoiCells) {
			for (int i = 0; i < voronoiCells.Count; i++) {
				if (e.polygonCenter == voronoiCells[i].center) {
					return i;
				}
			}

			return -1;
		}

		//Try to add a voronoi edge. Not all edges have a neighboring triangle, and if it hasnt we cant add a voronoi edge
		static void TryAddVoronoiEdgeFromTriangleEdge(HalfEdge e, Vector2 voronoiVertex, List<Edge> allEdges) {
			//Ignore if this edge has no neighboring triangle
			if (e.oppositeEdge == null) {
				return;
			}

			//Calculate the circumcenter of the neighbor
			HalfEdge eNeighbor = e.oppositeEdge;

			Vector2 v1 = eNeighbor.pointsTo.position;
			Vector2 v2 = eNeighbor.nextEdge.pointsTo.position;
			Vector2 v3 = eNeighbor.nextEdge.nextEdge.pointsTo.position;

			Vector2 center = CalculateCircleCenter(v1, v2, v3);

			Vector2 voronoiVertexNeighbor = new Vector3(center.x, 0f, center.y);

			//Create a new voronoi edge between the voronoi vertices
			Edge edge = new Edge(voronoiVertex, voronoiVertexNeighbor, e.prevEdge.pointsTo.position);

			allEdges.Add(edge);
		}

    #endregion*/
	}
}
