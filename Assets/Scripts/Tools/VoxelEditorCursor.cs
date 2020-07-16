using Evix.Terrain;
using Evix.Terrain.Collections;
using Evix.Terrain.MeshGeneration;
using UnityEditor;
using UnityEngine;

namespace Evix.Tools {

  /// <summary>
  /// Tool for editing voxels quickly from a viewport with the mouse
  /// </summary>
  [RequireComponent(typeof(MeshFilter))]
  [RequireComponent(typeof(MeshRenderer))]
  public class VoxelEditorCursor : MonoBehaviour {

    public enum Mode { Add, Remove};

    /// <summary>
    /// The center of the preview area, representing the selected block
    /// </summary>
    public static readonly Coordinate PreviewAreaCenter = (1, 1, 1);

    /// <summary>
    /// The diameter of the preview area
    /// </summary>
    const int PreviewAreaDiameter = 3;

    /// <summary>
    /// The bounds of the small edit preview area. it's a 3x3x3 with 1,1,1 being the center.
    /// </summary>
    static readonly Coordinate[] PreviewAreaBounds = new Coordinate[] {
      Coordinate.Zero,
      (PreviewAreaDiameter, PreviewAreaDiameter, PreviewAreaDiameter)
    };

    /// <summary>
    /// The mesh renderer to use to preview the changes
    /// </summary>
    MeshFilter previewMeshFilter;

    /// <summary>
    /// The mesh renderer used for previewing
    /// </summary>
    MeshRenderer previewMeshRenderer;

    /// <summary>
    /// The material to use for the remove grid
    /// </summary>
    public Material removeGridMaterial;

    /// <summary>
    /// The material to use for the add grid
    /// </summary>
    public Material addGridMaterial;

    /// <summary>
    /// The cursor mode
    /// </summary>
    Mode currentMode = Mode.Add;

    void Start() {
      previewMeshFilter = GetComponent<MeshFilter>();
      previewMeshRenderer = GetComponent<MeshRenderer>();
      previewMeshFilter.mesh = new Mesh();
      setCursorMode(currentMode);
    }

    public void previewPotentialTerrainEdit(TerrainBlock.Type voxelTypeToSet) {
      // step 1, get all the voxel positions around this voxel:
      Coordinate[] previewPoints = Coordinate.GetAllPointsBetween(
        transform.position.Round() - PreviewAreaBounds[0] - PreviewAreaCenter,
        transform.position.Round() + PreviewAreaBounds[1] - PreviewAreaCenter
      );

      // get all the voxel values from those positions
      byte[] previewVoxels = new byte[previewPoints.Length];
      PreviewAreaBounds[0].until(PreviewAreaBounds[1], localPreviewArrayPosition => {
        previewVoxels[localPreviewArrayPosition.flatten(PreviewAreaDiameter)] 
          = World.Current.activeLevel[previewPoints[localPreviewArrayPosition.flatten(PreviewAreaDiameter)]];
      });

      // set the current preview voxel
      previewVoxels[PreviewAreaCenter.flatten(PreviewAreaDiameter)] = voxelTypeToSet.Id;
      ChunkMeshData mesh = MarchingTetsMeshGenerator.GenerateMesh(previewVoxels, PreviewAreaDiameter);
      previewMeshFilter.mesh.Clear();
      previewMeshFilter.mesh.SetVertices(mesh.vertices);
      previewMeshFilter.mesh.SetTriangles(mesh.triangles, 0);
      previewMeshFilter.mesh.SetColors(mesh.colors);
      previewMeshFilter.mesh.SetUVs(0, mesh.uvs);
      previewMeshFilter.mesh.RecalculateNormals();

      // move it to the right location
      previewMeshFilter.transform.position = transform.position.Round() - PreviewAreaCenter;
      if (currentMode == Mode.Add) {
        previewMeshFilter.transform.position -= Vector3.down * 0.001f;
      }
    }

    /// <summary>
    /// Set the cursor mode
    /// </summary>
    /// <param name="newMode"></param>
    public void setCursorMode(Mode newMode) {
      previewMeshRenderer.sharedMaterial = newMode == Mode.Remove
        ? removeGridMaterial
        : addGridMaterial;
      currentMode = newMode;
    }

    #region Unity Inspector Tools

#if UNITY_EDITOR
    [CustomEditor(typeof(VoxelEditorCursor))]
    class VoxelTestEditorCustomInspector : Editor {
      public override void OnInspectorGUI() {
        EditorGUILayout.LabelField("Mesh filter to use for the preview:", "-----");
        DrawDefaultInspector();
        /// Draw the tool's current details
        EditorGUILayout.LabelField("Selected Voxel Info:", "-----");
        VoxelEditorCursor editTool = target as VoxelEditorCursor;
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(
          "Selected Voxel Type:",
          TerrainBlock.Types.Get(
            World.Current.activeLevel?[editTool.transform.position] ?? TerrainBlock.Types.Air.Id
          ).GetType().ToString()
        );
        EditorGUILayout.Vector3Field("Selected Voxel POS:", editTool.transform.position.Round());
        EditorGUILayout.Vector3Field("Current ChunkID:", Chunk.IDFromWorldLocation(editTool.transform.position));
        EditorGUI.EndDisabledGroup();
      }
    }
#endif

    #endregion
  }
}
