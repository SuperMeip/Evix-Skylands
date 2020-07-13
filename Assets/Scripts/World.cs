using Evix.EventSystems;
using UnityEngine;

namespace Evix {

	/// <summary>
	/// Za warudo
	/// </summary>
	public class World {

		/// <summary>
		/// The size of a voxel 'block', in world
		/// </summary>
		public const float BlockSize = 1.0f;

		/// <summary>
		/// The current level's save path.
		/// </summary>
		public static readonly string GameSaveFilePath;

		/// <summary>
		/// The current world
		/// </summary>
		public static World Current {
			get;
		} = new World();

		/// <summary>
		/// The debugger used to interface with unity debugging.
		/// </summary>
		public static UnityDebugger Debugger {
			get;
		} = new UnityDebugger();

		/// <summary>
		/// The debugger used to interface with unity debugging.
		/// </summary>
		public static WorldEventSystem EventSystem {
			get;
		} = new WorldEventSystem();

		/// <summary>
		/// The currently loaded level
		/// </summary>
		public Level activeLevel {
			get;
			protected set;
		}

		/// <summary>
		/// Set up the save path
		/// </summary>
		static World() {
			GameSaveFilePath = Application.persistentDataPath;
		}

		/// <summary>
		/// Set a level as the active level of the current world
		/// </summary>
		/// <param name="level"></param>
		public static void SetActiveLevel(Level level) {
			Current.activeLevel = level;
		}
	}
}
