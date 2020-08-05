using Evix;
using Evix.Events;
using Evix.Terrain.Collections;
using UnityEngine;
using UnityEditor;
using Evix.Managers;
using Evix.Controllers;
using Evix.Terrain.DataGeneration.Voronoi;
using System.Collections.Generic;
using Evix.Terrain.Resolution;

namespace Evix.Testing {

  /// <summary>
  /// Test debugger for level and other data
  /// </summary>
  public class LevelDataTester : MonoBehaviour {
#if UNITY_EDITOR
    /// <summary>
    /// The test chunk, set by the user
    /// </summary>
    public Coordinate testChunkID
      = Coordinate.Invalid;

    /// <summary>
    /// The test chunk, set by the user
    /// </summary>
    public int testChunkControllerID
      = -1;

    /// <summary>
    /// The test timer name to get a log for
    /// </summary>
    public string testTimerName
      = "";

    /// <summary>
    /// If we should draw the biome boundaries
    /// </summary>
    public bool drawBiomeBoundaries;

    /// <summary>
    /// Transparency to draw biomes at
    /// </summary>
    public float biomeTransparency = 0.3f;

    /// <summary>
    /// How far we should zoom out around the current rendered area to show the biomes, in chunks
    /// </summary>
    public int biomeZoomRadius = 50;

    /// <summary>
    /// the child terrain manager if there is one
    /// </summary>
    public LevelTerrainManager levelTerrainManager {
      get;
      private set;
    }

    /// <summary>
    /// Init
    /// </summary>
    void Start() {
      levelTerrainManager = GetComponentInChildren<LevelTerrainManager>();
    }

    #region Built In Gizmos
    private void OnDrawGizmos() {
      /// Draw biome boundaries over the world4
      if (drawBiomeBoundaries) {
        var cells = levelTerrainManager.level.biomeMap.getBiomeCellsAround(
          (levelTerrainManager.level.getFocusByID(1).currentChunkID * Chunk.Diameter).xz,
          biomeZoomRadius
        );
        DrawShapes(cells, new Vector3(0, World.SeaLevel + 50, 0), false, biomeTransparency);
      }
    }

    #endregion

    #region Log Print Functions

    /// <summary>
    /// Print data about the specified chunk controller
    /// </summary>
    /// <param name="chunkID"></param>
    public static void PrintChunkControllerRecords(int chunkControllerID, LevelTerrainManager terrainManager) {
      if (terrainManager.tryToGetChunkControllerByID(chunkControllerID, out ChunkController chunkController)) {
        World.Debug.log($"Logs for chunk controller on object : {chunkController.gameObject.name}\n"
          + $"Is Active: {chunkController.isActive}\n"
          + $"Is Meshed: {chunkController.isMeshed}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(chunkController.getRecordedEvents()));
      } else World.Debug.logError($"Tried to get data for non existant chunk controller: {chunkControllerID}");
    }

    /// <summary>
    /// Print the result log of the timer with the given name
    /// </summary>
    /// <param name="timerName"></param>
    public static void PrintTestTimerLog(string timerName) {
      World.Debug.log(World.Debug.Timer.getResultsLog(timerName));
    }

    /// <summary>
    /// Print data about the specified chunk
    /// </summary>
    /// <param name="chunkID"></param>
    static void PrintChunkDataRecords(Coordinate chunkID, int? lastXMessages = null) {
      if (chunkID != Coordinate.Invalid) {
        Chunk testChunk = World.Current.activeLevel.getChunk(chunkID);
        World.Debug.log($"Logs for chunk with ID: {chunkID}\n"
          + $"Chunk is currently {(testChunk.isLockedForWork ? "locked" : "unlocked")} for {testChunk.adjustmentLockType}\n"
          + $"Current Resolution: {testChunk.currentResolution}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(testChunk.getRecordedEvents(lastXMessages)));
      } else World.Debug.logError($"Tried to get data for no chunk");
    }

    #endregion

    #region Unity Inspector GUI Objects

    /// <summary>
    /// Draw a gui chunk 
    /// </summary>
    /// <param name="chunkID"></param>
    public static int InspectorDrawChunkHistoryGUIButton(Coordinate chunkID, int fieldValue) {
      GUILayout.BeginHorizontal();
      int? trimToXMessages = EditorGUILayout.IntField("Last X Messages:", fieldValue);
      trimToXMessages = trimToXMessages == 0 ? null : trimToXMessages;
      if (GUILayout.Button("Print Chunk History Logs")) {
        PrintChunkDataRecords(chunkID, trimToXMessages);
      }
      GUILayout.EndHorizontal();

      return trimToXMessages ?? 0;
    }

    #endregion

    #region Gizmo Draw Utility Functions

    /// <summary>
    /// Draw voronoi shapes for debugging purposes
    /// </summary>
    /// <param name="shapes"></param>
    /// <param name="positionOffset"></param>
    /// <param name="drawWire"></param>
    public static void DrawShapes(Polygon[] shapes, Vector3 positionOffset, bool drawWire = false, float transpacecy = 1.0f) {
      Random.InitState(1234);

      for (int i = 0; i < shapes.Length; i++) {
        Polygon c = shapes[i];

        Gizmos.color = drawWire
          ? new Color(0, 0, 0, transpacecy)
          : new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), transpacecy);

        List<Vector3> vertices = new List<Vector3>();

        List<int> triangles = new List<int>();

        if (c.isVoronoi) {
          Vector3 p1 = c.center;
          vertices.Add(p1);
        }

        c.forEachEdge(edge => {
          Vector3 p3 = edge.start;
          Vector3 p2 = edge.end;

          vertices.Add(p2);
          vertices.Add(p3);

          triangles.Add(0);
          triangles.Add(vertices.Count - 1);
          triangles.Add(vertices.Count - 2);
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

    #endregion
  }

  [CustomEditor(typeof(LevelDataTester))]
  public class LevelDataTesterGUI : Editor {

    /// <summary>
    /// Last X mesages to get from history
    /// </summary>
    int chunkHistoryItemsToGet = 0;

    /// <summary>
    /// Draw the default gui and the print button
    /// </summary>
    public override void OnInspectorGUI() {
      DrawDefaultInspector();

      LevelDataTester testData = (LevelDataTester)target;
      chunkHistoryItemsToGet = LevelDataTester.InspectorDrawChunkHistoryGUIButton(testData.testChunkID, chunkHistoryItemsToGet);

      if (GUILayout.Button("Print Chunk Controller Log for ControllerID")) {
        LevelDataTester.PrintChunkControllerRecords(testData.testChunkControllerID, testData.levelTerrainManager);
      }

      if (GUILayout.Button("Print Timer Log for Timer Name")) {
        LevelDataTester.PrintTestTimerLog(testData.testTimerName);
      }
    }
  }
#endif
}