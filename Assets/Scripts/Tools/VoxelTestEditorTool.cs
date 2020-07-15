using Evix.Terrain;
using Evix.Terrain.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Evix.Tools {

  /// <summary>
  /// Tool for editing voxels quickly from a viewport with the mouse
  /// </summary>
  class VoxelTestEditorTool : MonoBehaviour {

    /// <summary>
    /// The camera to use as the viewport
    /// </summary>
    public Transform testPOVPosition;

    /// <summary>
    /// The voxel type to set when adding a new voxel
    /// </summary>
    [HideInInspector]
    public TerrainBlock.Type voxelTypeToSet
      = TerrainBlock.Types.Stone;

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    public void removeBlock(Coordinate voxelToChange) {
      World.Current.activeLevel[voxelToChange] = Terrain.TerrainBlock.Types.Air.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(voxelToChange));
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    public void addBlock(Coordinate voxelToChange, TerrainBlock.Type blockType) {
      World.Current.activeLevel[voxelToChange] = blockType.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(voxelToChange));
    }

    #region Unity Inspector Tools

#if UNITY_EDITOR
    [CustomEditor(typeof(VoxelTestEditorTool))]
    class VoxelTestEditorCustomInspector : Editor {
      public override void OnInspectorGUI() {
        DrawDefaultInspector();

        /// Draw the tool's current details
        EditorGUILayout.LabelField("Selected Voxel Info:", "-----");
        VoxelTestEditorTool editTool = target as VoxelTestEditorTool;
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(
          "Selected Voxel Type:",
          TerrainBlock.Types.Get(
            World.Current.activeLevel?[editTool.transform.position] ?? TerrainBlock.Types.Air.Id
          ).GetType().ToString()
        );
        EditorGUILayout.Vector3Field("Selected Voxel POS:", new Coordinate(editTool.transform.position));
        EditorGUILayout.Vector3Field("Current ChunkID:", Chunk.IDFromWorldLocation(editTool.transform.position));
        EditorGUI.EndDisabledGroup();

        /// draw the edit interface
        EditorGUILayout.LabelField("Edit Selected Voxel:", "-----");
        editTool.voxelTypeToSet = TerrainBlock.Types.Get(
          (byte)EditorGUILayout.Popup(
            "Edit Terrain Type:",
            editTool.voxelTypeToSet.Id,
            TerrainBlock.Types.All.Select(block => block.Name).ToArray(),
            new GUILayoutOption[0]
          )
        );
        if (GUILayout.Button("Update Voxel at Location")) {
          editTool.addBlock(editTool.transform.position, editTool.voxelTypeToSet);
        }
      }
    }
#endif

    #endregion
  }
}
