using Evix.Terrain.DataGeneration.Voronoi;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evix.Controllers.Testing {

  public class VoronoiController : MonoBehaviour {

    public bool isEnabled = false;

    public int seed = 0;

    public float halfMapSize = 10f;

    public float numberOfPoints = 20;

    private void OnDrawGizmos() {
      if (!isEnabled) {
        return;
      }

      //Generate the random sites
      List<Vector2> randomSites = new List<Vector2>();

      //Generate random numbers with a seed
      Random.InitState(seed);

      float max = halfMapSize;
      float min = -halfMapSize;

      for (int i = 0; i < numberOfPoints; i++) {
        float randomX = Random.Range(min, max);
        float randomZ = Random.Range(min, max);

        randomSites.Add(new Vector3(randomX, 0f, randomZ));
      }


      //Points outside of the screen for voronoi which has some cells that are infinite
      float bigSize = halfMapSize * 5f;

      //Star shape which will give a better result when a cell is infinite large
      //When using other shapes, some of the infinite cells misses triangles
      randomSites.Add(new Vector2(0f, bigSize));
      randomSites.Add(new Vector2(0f, -bigSize));
      randomSites.Add(new Vector2(bigSize, 0f));
      randomSites.Add(new Vector2(-bigSize, 0f));


      //Generate the voronoi diagram
      List<Polygon> cells = Delaunay.GenerateVoronoiDiagram(randomSites);


      //Debug
      //Display the voronoi diagram
      DisplayVoronoiCells(cells);

      //Display the sites
      Gizmos.color = Color.white;

      for (int i = 0; i < randomSites.Count; i++) {
        float radius = 0.2f;

        Gizmos.DrawSphere(randomSites[i], radius);
      }
    }

    //Display the voronoi diagram with mesh
    private void DisplayVoronoiCells(List<Polygon> cells) {
      Random.InitState(seed);

      for (int i = 0; i < cells.Count; i++) {
        Polygon c = cells[i];

        Vector2 p1 = c.center;

        Gizmos.color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);

        List<Vector3> vertices = new List<Vector3>();

        List<int> triangles = new List<int>();

        vertices.Add(p1);

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

        Gizmos.DrawMesh(triangleMesh);
      }
    }
  }
}