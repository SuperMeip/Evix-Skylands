using Evix.Terrain.DataGeneration.Voronoi;
using Evix.Testing;
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
      LevelDataTester.DrawShapes(cells.Values.ToArray(), Vector3.zero);
      if (showDelaunayDiagam) {
        LevelDataTester.DrawShapes(triangles.Values.ToArray(), Vector3.forward, true);
      }

      //Display the sites
      Gizmos.color = Color.white;

      for (int i = 0; i < randomSites.Count; i++) {
        float radius = 0.2f;

        Gizmos.DrawSphere(randomSites[i], radius);
      }
    }
  }
}