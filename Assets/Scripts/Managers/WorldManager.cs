using UnityEngine;
using Evix.Terrain.Collections;

namespace Evix.Managers {

	public class WorldManager : MonoBehaviour {

		/// <summary>
		/// The current focus of the active level
		/// </summary>
		public FocusManager startingFocus;

		/// <summary>
		/// The Level manager to use for the active level
		/// </summary>
		public LevelTerrainManager levelManager;

		// Called when the node enters the scene tree for the first time.
		void Start() {
			startLevelManager();
		}

		void startLevelManager() {
			World.SetActiveLevel(new Level((1000, 20, 1000)));
			startingFocus.setPosition((World.Current.activeLevel.chunkBounds) / 2 * Chunk.Diameter);
			levelManager.initializeFor(World.Current.activeLevel, startingFocus);
		}
	}
}
