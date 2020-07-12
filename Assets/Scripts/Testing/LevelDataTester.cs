﻿using Evix;
using Evix.Events;
using Evix.Terrain.Collections;
using UnityEngine;
using UnityEditor;

namespace Evix.Testing {
  public class LevelDataTester : MonoBehaviour {
    /// <summary>
    /// The test chunk, set by the user
    /// </summary>
    public Coordinate testChunkID
      = Coordinate.Invalid;

    /// <summary>
    /// Print data about the specified chunk
    /// </summary>
    /// <param name="chunkID"></param>
    public static void PrintChunkRecords(Coordinate chunkID) {
      if (chunkID != Coordinate.Invalid) {
        Chunk testChunk = World.Current.activeLevel.getChunk(chunkID);
        World.Debugger.log($"Logs for chunk with ID: {chunkID}\n"
          + $"Chunk is currently {(testChunk.isLockedForWork ? "locked" : "unlocked")} for {testChunk.resolutionModificationLockType}\n"
          + $"Current Resolution: {testChunk.currentResolution}\n"
          + "Edit History:\n"
          + RecordedInterfaceHelper.FormatRecordsMarkdown(testChunk.getRecordedEvents()));
      } else World.Debugger.logError($"Tried to get data for no chunk");
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
      if (GUILayout.Button("Get Test Chunk Logs")) {
        LevelDataTester.PrintChunkRecords(testData.testChunkID);
      }
    }
  }
}