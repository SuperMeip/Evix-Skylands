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
      udpdateCurrentlySelectedBlock(out RaycastHit hit);
      checkIfBlockShouldBeDeleted(hit.normal/* - camera.transform.forward*/);
      checkIfBlockShouldBeAdded();
    }

    /// <summary>
    /// Highlight the currently moused over voxel
    /// </summary>
    void udpdateCurrentlySelectedBlock(out RaycastHit hit) {
      Ray ray = camera.ScreenPointToRay(new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2, 0));

      if (Physics.Raycast(ray, out hit, 25)) {
        selectedBlockOutlineObject.transform.position = currentlySelectedVoxelPosition = new Coordinate(hit.point - camera.transform.forward);
      }
    }

    /// <summary>
    /// Remove the block and mark the chunk dirty on left click
    /// </summary>
    void checkIfBlockShouldBeDeleted(Vector3 addOnDirecton = default) {
      if (Input.GetMouseButtonDown(0)) {
        removeBlock(currentlySelectedVoxelPosition.vec3 + addOnDirecton);
      }
    }

    /// <summary>
    /// Remove the block and mark the chunk dirty on left click
    /// </summary>
    void checkIfBlockShouldBeAdded(Vector3 addOnDirecton = default) {
      if (Input.GetMouseButtonDown(1)) {
        addBlock(currentlySelectedVoxelPosition.vec3 + addOnDirecton, TerrainBlock.Types.Stone);
      }
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    void removeBlock(Coordinate voxelToChange) {
      World.Current.activeLevel[voxelToChange] = Terrain.TerrainBlock.Types.Air.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(voxelToChange.x, voxelToChange.y, voxelToChange.z));
    }

    /// <summary>
    /// Remove a block on a button press
    /// </summary>
    /// <param name="hitBlock"></param>
    void addBlock(Coordinate voxelToChange, TerrainBlock.Type blockType) {
      World.Current.activeLevel[voxelToChange] = blockType.Id;
      World.Current.activeLevel.markChunkDirty(Chunk.IDFromWorldLocation(voxelToChange.x, voxelToChange.y, voxelToChange.z));
    }
  }
}
