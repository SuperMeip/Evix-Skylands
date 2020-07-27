using Evix.Terrain.DataGeneration.Voronoi;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Evix.Controllers.Testing {

  public class VoronoiController : MonoBehaviour {

    /// <summary>
    /// If the whole preview generation is enabled.
    /// </summary>
    public bool isEnabled = false;

    /// <summary>
    /// If we should preview the delanuay diagram too
    /// </summary>
    public bool showDelaunayDiagam = false;

    /// <summary>
    /// The random seed to use
    /// </summary>
    public int seed = 0;

    /// <summary>
    /// The map radius
    /// </summary>
    public int mapRadius = 100;

    /// <summary>
    /// The number of points/voronoi cells to generate
    /// </summary>
    public int numberOfPoints = 20;

    private void OnDrawGizmos() {
      if (!isEnabled) {
        return;
      }

      //Generate the random sites
      List<Vector2> randomSites = new List<Vector2>();

      //Generate random numbers with a seed
      Random.InitState(seed);

      int max = mapRadius;
      int min = -mapRadius;

      for (int i = 0; i < numberOfPoints; i++) {
        int randomX = Random.Range(min, max);
        int randomZ = Random.Range(min, max);

        randomSites.Add(new Vector2(randomX, randomZ));
      }


      //Points outside of the screen for voronoi which has some cells that are infinite
      float bigSize = mapRadius * 5f;

      //Star shape which will give a better result when a cell is infinite large
      //When using other shapes, some of the infinite cells misses triangles
      randomSites.Add(new Vector2(0f, bigSize));
      randomSites.Add(new Vector2(0f, -bigSize));
      randomSites.Add(new Vector2(bigSize, 0f));
      randomSites.Add(new Vector2(-bigSize, 0f));

      //Generate the voronoi diagram
      var (vertices, triangles) = Delaunay.GenerateTriangulation(randomSites);
      var cells = Delaunay.GenerateVoronoiCells((vertices, triangles));

      //Display the voronoi diagram
      DrawShapes(cells.Values.ToList(), Vector3.zero);
      if (showDelaunayDiagam) {
        DrawShapes(triangles.Values.ToList(), Vector3.forward, true);
      }

      //Display the sites
      Gizmos.color = Color.white;

      for (int i = 0; i < randomSites.Count; i++) {
        float radius = 0.2f;

        Gizmos.DrawSphere(randomSites[i], radius);
      }
    }

    /// <summary>
    /// Draw voronoi shapes for debugging purposes
    /// </summary>
    /// <param name="shapes"></param>
    /// <param name="positionOffset"></param>
    /// <param name="drawWire"></param>
    void DrawShapes(List<Polygon> shapes, Vector3 positionOffset, bool drawWire = false) {
      Random.InitState(seed);

      for (int i = 0; i < shapes.Count; i++) {
        Polygon c = shapes[i];

        Gizmos.color = drawWire 
          ? Color.black 
          : new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);

        List<Vector3> vertices = new List<Vector3>();

        List<int> triangles = new List<int>();

        if (c.isVoronoi) {
          Vector2 p1 = c.center;
          vertices.Add(p1);
        }

        c.forEachEdge(edge => {
          Vector2 p3 = edge.start;
          Vector2 p2 = edge.end;

          vertices.Add(p2);
          vertices.Add(p3);

          triangles.Add(0);
          triangles.Add(vertices.Count - 2);
          triangles.Add(vertices.Count - 1);
        });

        Mesh triangleMesh = new Mesh();

        triangleMesh.vertices = vertices.ToArray();

        triangleMesh.triangles = triangles.ToArray();

        triangleMesh.RecalculateNormals();

        if (drawWire == true) {
          Gizmos.DrawWireMesh(triangleMesh, positionOffset);
        } else {
          Gizmos.DrawMesh(triangleMesh, positionOffset);
        }
      }
    }
  }
}