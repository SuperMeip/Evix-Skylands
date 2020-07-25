using Evix.Terrain.DataGeneration.Voronoi;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Evix.Controllers.Testing {

  public class VoronoiController : MonoBehaviour {

    public bool isEnabled = false;

    public int seed = 0;

    public int mapRadius = 100;

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

      //Debug
      //Display the voronoi diagram
      //DisplayVoronoiCells(triangles.Values.ToList());
      DisplayVoronoiCells(triangles.Values.ToList(), Vector3.zero);
      DisplayVoronoiCells(cells.Values.ToList(), Vector3.forward, true);

      //Display the sites
      Gizmos.color = Color.white;

      for (int i = 0; i < randomSites.Count; i++) {
        float radius = 0.2f;

        Gizmos.DrawSphere(randomSites[i], radius);
      }
    }

    //Display the voronoi diagram with mesh
    private void DisplayVoronoiCells(List<Polygon> cells, Vector3 positionOffset, bool drawWire = false) {
      Random.InitState(seed);

      for (int i = 0; i < cells.Count; i++) {
        Polygon c = cells[i];

        Gizmos.color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);

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