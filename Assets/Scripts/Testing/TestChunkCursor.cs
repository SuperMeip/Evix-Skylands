using Evix.Terrain.Collections;
using Evix.Testing;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Simple cursor you can move to check chunk data at a given location
/// </summary>
public class TestChunkCursor : MonoBehaviour {
    /// draw the chunk the cursor is in
  private void OnDrawGizmosSelected() {
    Gizmos.color = new Color(1.0f, 0.64f, 0.0f);
    Vector3 worldChunkLocation = ((Chunk.IDFromWorldLocation(transform.position) * Chunk.Diameter) + (Chunk.Diameter / 2)).vec3;
    Gizmos.DrawWireCube(worldChunkLocation, new Vector3(Chunk.Diameter, Chunk.Diameter, Chunk.Diameter));
  }
}

[CustomEditor(typeof(TestChunkCursor))]
class FocusCustomInspoector : Editor {
  public override void OnInspectorGUI() {
    /// Just info about the chunk
    EditorGUILayout.LabelField("Chunk Info:", "-----");
    EditorGUI.BeginDisabledGroup(true);
    TestChunkCursor cursor = target as TestChunkCursor;
    EditorGUILayout.Vector3Field("Chunk ID", Chunk.IDFromWorldLocation(cursor.transform.position));
    EditorGUI.EndDisabledGroup();

    if (GUILayout.Button("Print Chunk History Log")) {
      LevelDataTester.PrintChunkDataRecords(Chunk.IDFromWorldLocation(cursor.transform.position));
    }
    DrawDefaultInspector();
  }
}
