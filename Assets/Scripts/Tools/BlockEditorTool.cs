using Evix.Terrain;
using Evix.Terrain.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Evix.Tools {

  /// <summary>
  /// Tool for editing voxels quickly from a viewport with the mouse
  /// </summary>
  class BlockEditorTool : MonoBehaviour {

    /// <summary>
    /// The model for the select block outline
    /// </summary>
    public GameObject selectedBlockOutlineObject;

    /// <summary>
    /// The camera to use as the viewport
    /// </summary>
    public Camera camera;

    /// <summary>
    /// Tracks the position hilighted for the current voxel by the mouse
    /// </summary>
    Coordinate currentlySelectedVoxelPosition;

    void Start() {
      camera = camera ?? GetComponent<Camera>();
      // set up the object we use as a tooltip
      /*if (selectedBlockOutlineObject != null) {
        selectedBlockOutlineObject.transform.localScale = new Vector3(
          World.BlockSize + 0.01f,
          World.BlockSize + 0.01f,
          World.BlockSize + 0.01f
        );
      }*/
    }

    void Update() {
      udpdateCurrentlySelectedBlock();
    }

    /// <summary>
    /// Highlight the currently moused over voxel
    /// </summary>
    void udpdateCurrentlySelectedBlock() {
      Ray ray = camera.ScreenPointToRay(new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2, 0));

      if (Physics.Raycast(ray, out RaycastHit hit, 25)) {
        currentlySelectedVoxelPosition = hit.point/* + hit.normal*/;
        selectedBlockOutlineObject.transform.position = currentlySelectedVoxelPosition;
      }
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    void removeBlock(Coordinate hitCoordinate) {
      World.Current.activeLevel[hitCoordinate] = Terrain.TerrainBlock.Types.Air.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(hitCoordinate.x, hitCoordinate.y, hitCoordinate.z));
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    void addBlock(Coordinate hitCoordinate, TerrainBlock.Type blockType) {
      World.Current.activeLevel[hitCoordinate] = blockType.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(hitCoordinate.x, hitCoordinate.y, hitCoordinate.z));
    }
  }
}
