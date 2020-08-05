using Evix.Terrain;
using Evix.Terrain.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Evix.Tools {

  /// <summary>
  /// Tool for editing voxels quickly from a viewport with the mouse
  /// </summary>
  class BlockEditorTool : MonoBehaviour {

    /// <summary>
    /// The model for the select block outline
    /// </summary>
    public VoxelEditorCursor selectedVoxelCursor;

    /// <summary>
    /// The camera to use as the viewport
    /// </summary>
    public Camera povCamera;

    /// <summary>
    /// Seconds to wait between updating the selected voxel
    /// </summary>
    public float selectedVoxelUpdateDelay = 0.1f;

    /// <summary>
    /// Seconds to wait between updating the selected voxel
    /// </summary>
    float selectedVoxelUpdateTimer = 50;

    /// <summary>
    /// The voxel type to set when adding a new voxel
    /// </summary>
    TerrainBlock.Type voxelTypeToSet
      = TerrainBlock.Types.Stone;

    /// <summary>
    /// The last set voxel location
    /// </summary>
    Vector3 previousSelectedVoxelLocation;

    void Update() {
      udpdateCurrentlySelectedVoxel();
      checkIfShouldChangeBlockType();
      checkIfSelectedVoxelShouldBeUpdated();
    }

    /// <summary>
    /// Highlight the currently moused over voxel
    /// </summary>
    void udpdateCurrentlySelectedVoxel() {
      if ((selectedVoxelUpdateTimer += Time.deltaTime) >= selectedVoxelUpdateDelay) {
        Ray ray = povCamera.ScreenPointToRay(new Vector3(povCamera.pixelWidth / 2, povCamera.pixelHeight / 2, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, 25)) {
          Vector3 newHitPosition = voxelTypeToSet.IsSolid
              ? (hit.point - povCamera.transform.forward).Round()
              : (hit.point + povCamera.transform.forward).Round();
          if (newHitPosition != previousSelectedVoxelLocation) {
            selectedVoxelCursor.transform.position = newHitPosition;
            previousSelectedVoxelLocation = selectedVoxelCursor.transform.position;
            selectedVoxelCursor.previewPotentialTerrainEdit(voxelTypeToSet);
          }
        }

        selectedVoxelUpdateTimer = 0;
      }
    }

    /// <summary>
    /// Remove the block and mark the chunk dirty on left click
    /// </summary>
    void checkIfSelectedVoxelShouldBeUpdated() {
      if (Input.GetMouseButtonDown(0)) {
        updateVoxel(selectedVoxelCursor.transform.position + VoxelEditorCursor.PreviewAreaCenter.vec3, voxelTypeToSet);
      }
    }

    /// <summary>
    /// Toggle between stone and air with space bar
    /// </summary>
    void checkIfShouldChangeBlockType() {
      if (Input.GetKeyDown(KeyCode.Space)) {
        setVoxelType(voxelTypeToSet == TerrainBlock.Types.Stone ? TerrainBlock.Types.Air : TerrainBlock.Types.Stone);
      }
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    void updateVoxel(Coordinate voxelToChange, TerrainBlock.Type blockType) {
      World.Current.activeLevel[voxelToChange] = blockType.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(voxelToChange));
    }

    /// <summary>
    /// Update the type of voxel we're previewing
    /// </summary>
    /// <param name="type"></param>
    void setVoxelType(TerrainBlock.Type type) {
      if ((voxelTypeToSet.IsSolid && !type.IsSolid) || (!voxelTypeToSet.IsSolid && type.IsSolid)) {
        voxelTypeToSet = type;
        udpdateCurrentlySelectedVoxel();
        selectedVoxelCursor.setCursorMode(type.IsSolid 
          ? VoxelEditorCursor.Mode.Add 
          : VoxelEditorCursor.Mode.Remove
        );
      } else {
        voxelTypeToSet = type;
      }
    }

    #region Unity Inspector Tools

#if UNITY_EDITOR
    [CustomEditor(typeof(BlockEditorTool))]
    class VoxelTestEditorCustomInspector : Editor {
      public override void OnInspectorGUI() {
        DrawDefaultInspector();

        /// Draw the tool's current details
        EditorGUILayout.LabelField("Selected Voxel Info:", "-----");
        BlockEditorTool editTool = target as BlockEditorTool;
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(
          "Selected Voxel Type:",
          TerrainBlock.Types.Get(
            World.Current.activeLevel?[editTool.selectedVoxelCursor.transform.position] ?? TerrainBlock.Types.Air.Id
          ).GetType().ToString()
        );
        EditorGUILayout.Vector3Field("Selected Voxel POS:", editTool.selectedVoxelCursor.transform.position.Round());
        EditorGUILayout.Vector3Field("Current ChunkID:", Chunk.IDFromWorldLocation(editTool.selectedVoxelCursor.transform.position));
        EditorGUI.EndDisabledGroup();

        /// draw the edit interface
        EditorGUILayout.LabelField("Edit Selected Voxel:", "-----");
        editTool.setVoxelType(TerrainBlock.Types.Get(
          (byte)EditorGUILayout.Popup(
            "Edit Terrain Type:",
            editTool.voxelTypeToSet.Id,
            TerrainBlock.Types.All.Select(block => block.Name).ToArray(),
            new GUILayoutOption[0]
          )
        ));
        if (GUILayout.Button("Update Voxel at Location")) {
          editTool.updateVoxel(editTool.selectedVoxelCursor.transform.position.Round(), editTool.voxelTypeToSet);
        }
      }
    }
#endif

    #endregion
  }
}

