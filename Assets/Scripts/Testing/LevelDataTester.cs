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
        World.Debugger.log($"Logs for chunk with ID: {chunkID}\n"
          + $"Chunk is currently {(testChunk.isLockedForWork ? "locked" : "unlocked")} for {testChunk.resolutionModificationLockType}\n"
          + $"Current Resolution: {testChunk.currentResolution}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(testChunk.getRecordedEvents()));
      } else World.Debugger.logError($"Tried to get data for no chunk");
    }

    /// <summary>
    /// Print data about the specified chunk controller
    /// </summary>
    /// <param name="chunkID"></param>
    public static void PrintChunkControllerRecords(int chunkControllerID, LevelTerrainManager terrainManager) {
      if (terrainManager.tryToGetChunkControllerByID(chunkControllerID, out ChunkController chunkController)) {
        World.Debugger.log($"Logs for chunk controller on object : {chunkController.gameObject.name}\n"
          + $"Is Active: {chunkController.isActive}\n"
          + $"Is Meshed: {chunkController.isMeshed}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(chunkController.getRecordedEvents()));
      } else World.Debugger.logError($"Tried to get data for non existant chunk controller: {chunkControllerID}");
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
      if (GUILayout.Button("Get Chunk ID Logs")) {
        LevelDataTester.PrintChunkDataRecords(testData.testChunkID);
      }

      if (GUILayout.Button("Get Chunk Controller ID Logs")) {
        LevelDataTester.PrintChunkControllerRecords(testData.testChunkControllerID, testData.levelTerrainManager);
      }
    }
  }
}