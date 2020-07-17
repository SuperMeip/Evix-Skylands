using Evix;
using Evix.Events;
using Evix.Terrain.Collections;
using UnityEngine;
using UnityEditor;
using Evix.Managers;
using Evix.Controllers;

namespace Evix.Testing {
  public class LevelDataTester : MonoBehaviour {
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

    /// <summary>
    /// Print data about the specified chunk
    /// </summary>
    /// <param name="chunkID"></param>
    public static void PrintChunkDataRecords(Coordinate chunkID) {
      if (chunkID != Coordinate.Invalid) {
        Chunk testChunk = World.Current.activeLevel.getChunk(chunkID);
        World.Debug.log($"Logs for chunk with ID: {chunkID}\n"
          + $"Chunk is currently {(testChunk.isLockedForWork ? "locked" : "unlocked")} for {testChunk.adjustmentLockType}\n"
          + $"Current Resolution: {testChunk.currentResolution}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(testChunk.getRecordedEvents()));
      } else World.Debug.logError($"Tried to get data for no chunk");
    }

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
  }

  [CustomEditor(typeof(LevelDataTester))]
  public class LevelDataTesterGUI : Editor {

    /// <summary>
    /// Draw the default gui and the print button
    /// </summary>
    public override void OnInspectorGUI() {
      DrawDefaultInspector();

      LevelDataTester testData = (LevelDataTester)target;
      if (GUILayout.Button("Print Chunk History Log for ChunkID")) {
        LevelDataTester.PrintChunkDataRecords(testData.testChunkID);
      }

      if (GUILayout.Button("Print Chunk Controller Log for ControllerID")) {
        LevelDataTester.PrintChunkControllerRecords(testData.testChunkControllerID, testData.levelTerrainManager);
      }

      if (GUILayout.Button("Print Timer Log for Timer Name")) {
        LevelDataTester.PrintTestTimerLog(testData.testTimerName);
      }
    }
  }
}