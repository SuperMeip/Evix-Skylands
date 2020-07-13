using Evix.Terrain.Collections;
using Evix.Terrain.Resolution;
using Evix.Testing;
using UnityEditor;
using UnityEngine;

namespace Evix.Managers {

  /// <summary>
  /// Used to track and manage a focus's position in the game world
  /// </summary>
  public class FocusManager : MonoBehaviour, ILevelFocus {

    /// <summary>
    /// The id of this focus in its level
    /// </summary>
    public int id {
      get;
      private set;
    }

    /// <summary>
    /// If this player is active
    /// </summary>
    public bool isActive {
      get;
      private set;
    }

    /// <summary>
    /// The current live chunk location of this focus
    /// </summary>
    public Coordinate currentChunkID {
      get;
      private set;
    }

    /// <summary>
    /// The previously sampled chunk location of this focus
    /// </summary>
    public Coordinate previousChunkID {
      get;
      private set;
    }

    /// <summary>
    /// The world (voxel) location of this player
    /// </summary>
    public Vector3 worldLocation {
      get;
      private set;
    }

    /// <summary>
    /// the previous world location of the character
    /// </summary>
    Vector3 previousWorldLocation;

    #region Game Loop

    void Update() {
      /// check to see if we should update the chunks
      if (!isActive) {
        return;
      }

      // if this is active and the world position has changed, check if the chunk has changed
      worldLocation = transform.position;
      if (worldLocation != previousWorldLocation) {
        previousWorldLocation = worldLocation;
        currentChunkID = worldLocation / Chunk.Diameter;
      }
    }

    #endregion

    #region Focus Functions

    /// <summary>
    /// set the controller active
    /// </summary>
    public void activate() {
      isActive = true;
    }

    /// <summary>
    /// Get the new chunk id and update the previous location to the current location
    /// </summary>
    /// <returns></returns>
    public Coordinate getUpdatedChunkID() {
      previousChunkID = currentChunkID;
      return currentChunkID;
    }

    /// <summary>
    /// Set the world position of the focus. Also sets the chunk position.
    /// </summary>
    public void setPosition(Coordinate worldPosition) {
      transform.position = worldLocation = previousWorldLocation = worldPosition.vec3;
      currentChunkID = previousChunkID = worldLocation / Chunk.Diameter;
    }

    /// <summary>
    /// Register this focus' id for a given level
    /// </summary>
    /// <param name="level"></param>
    /// <param name="id"></param>
    public void registerTo(Level level, int id) {
      this.id = id;
    }

    #endregion

    #region Equality Functions

    /// <summary>
    /// Equal override
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(ILevelFocus other) {
      return id.Equals(other.id);
    }

    /// <summary>
    /// hash code override
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      return id;
    }

    #endregion

    #region Unity Gizmos
#if UNITY_EDITOR/// <summary>
    /// Draw the managed Lens around this focus
    /// </summary>
    void OnDrawGizmos() {
      // ignore gizmo if inactive
      if (!isActive) {
        return;
      }

      Level level = World.Current.activeLevel;
      Vector3 worldChunkLocation = ((currentChunkID * Chunk.Diameter) + (Chunk.Diameter / 2)).vec3;

      /// draw the chunk this focus is in
      Gizmos.color = new Color(1.0f, 0.64f, 0.0f);
      Gizmos.DrawWireCube(worldChunkLocation, new Vector3(Chunk.Diameter, Chunk.Diameter, Chunk.Diameter));
      worldChunkLocation -= new Vector3((Chunk.Diameter / 2), (Chunk.Diameter / 2), (Chunk.Diameter / 2));

      /// draw the active chunk area
      if (level.getLens(this).tryToGetAperture(Chunk.Resolution.Visible, out IChunkResolutionAperture activeAperture)) {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(worldChunkLocation, new Vector3(
          activeAperture.managedChunkRadius * 2,
          Mathf.Min(activeAperture.managedChunkHeightRadius * 2, level.chunkBounds.y),
          activeAperture.managedChunkRadius * 2
        ) * Chunk.Diameter);
      }

      /// draw the meshed chunk area
      if (level.getLens(this).tryToGetAperture(Chunk.Resolution.Meshed, out IChunkResolutionAperture meshAperture)) {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(worldChunkLocation, new Vector3(
          meshAperture.managedChunkRadius * 2,
          Mathf.Min(meshAperture.managedChunkHeightRadius * 2, level.chunkBounds.y),
          meshAperture.managedChunkRadius * 2
        ) * Chunk.Diameter);
      }

      /// draw the meshed chunk area
      if (level.getLens(this).tryToGetAperture(Chunk.Resolution.Loaded, out IChunkResolutionAperture loadedAperture)) {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldChunkLocation, new Vector3(
          loadedAperture.managedChunkRadius * 2,
          Mathf.Min(loadedAperture.managedChunkHeightRadius * 2, level.chunkBounds.y),
          loadedAperture.managedChunkRadius * 2
        ) * Chunk.Diameter);
      }
    }
#endif
    #endregion
  }

#if UNITY_EDITOR
  /// <summary>
  /// Show off the chunk ID
  /// </summary>
  [CustomEditor(typeof(FocusManager))]
  class FocusCustomInspoector : Editor {
    public override void OnInspectorGUI() {
      EditorGUILayout.LabelField("Focus Info:");

      // Just info about the chunk
      EditorGUI.BeginDisabledGroup(true);
      FocusManager focus = target as FocusManager;
      EditorGUILayout.Toggle("Is Active", focus.isActive);
      EditorGUILayout.Vector3Field("Chunk ID", focus.currentChunkID.vec3);
      EditorGUILayout.Vector3Field("World Voxel Location", focus.worldLocation * World.BlockSize);
      EditorGUI.EndDisabledGroup();

      if (GUILayout.Button("Print Current Chunk Edit History Log")) {
        LevelDataTester.PrintChunkDataRecords(focus.currentChunkID);
      }
      DrawDefaultInspector();
    }
  }
#endif
}
