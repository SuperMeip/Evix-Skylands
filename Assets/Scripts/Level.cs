using Evix.Events;
using Evix.Managers;
using Evix.Terrain.Collections;
using Evix.Terrain.DataGeneration;
using Evix.Terrain.DataGeneration.Maps;
using Evix.Terrain.Resolution;
using System;
using System.Collections.Generic;

namespace Evix {
	public class Level {

		/// <summary>
		/// The overall bounds of the level, max x y and z
		/// </summary>
		public readonly Coordinate chunkBounds;

		/// <summary>
		/// The seed the level uses for generation
		/// </summary>
		public readonly int seed;

		/// <summary>
		/// The name of the level
		/// </summary>
		public string name {
			get;
			private set;
		} = "No Man's Land";

		/// <summary>
		/// The name of the level
		/// </summary>
		public readonly string legalFileSaveName;

		/// <summary>
		/// The biome map this level uses to generate chunks
		/// </summary>
		public readonly BiomeMap biomeMap;

		/// <summary>
		/// The collection of chunks
		/// </summary>
		readonly Dictionary<Coordinate, Chunk> chunks
			= new Dictionary<Coordinate, Chunk>();

		/// <summary>
		/// The focuses in this level and the lense to use for each of them
		/// </summary>
		readonly Dictionary<ILevelFocus, IFocusLens> focalLenses
			= new Dictionary<ILevelFocus, IFocusLens>();

		/// <summary>
		/// The focuses in this level indexed by ID
		/// </summary>
		readonly Dictionary<int, ILevelFocus> fociByID
			= new Dictionary<int, ILevelFocus>();

		/// <summary>
		/// The current highest assigned focus id.
		/// </summary>
		int currentMaxFocusID = 0;

		#region Constructors

		/// <summary>
		/// Create a new level of the given size that uses the given apetures.
		/// </summary>
		/// <param name="chunkBounds"></param>
		/// <param name="apeturesByPriority"></param>
		public Level(Coordinate chunkBounds, string name = "") {
			seed = 1234;
			this.name = name == "" ? this.name : name;
			legalFileSaveName = LevelDAO.IllegalCharactersForFileName.Replace(this.name, "");
 			this.chunkBounds = chunkBounds;
			biomeMap = new TestPlainIslandMap(this);
		}

		#endregion

		#region Access Functions

		/// <summary>
		/// Get a terrain voxel based on it's world location
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns></returns>
		public byte this[int x, int y, int z] {
			get {
				Chunk chunk = getChunk(Chunk.IDFromWorldLocation(x, y, z));
				return chunk?[x & 0xF, y & 0xF, z & 0xF] ?? 0;
			}

			set {
				if (chunks.TryGetValue(Chunk.IDFromWorldLocation(x, y, z), out Chunk chunk)) {
					chunk[x & 0xF, y & 0xF, z & 0xF] = value;
					return;
				// if the chunk doesn't exist, don't make a new one for an empty value
				} else if (value == 0) {
					return;
				}

				// if no chunk exists, and it's in bounds, we need to make the new chunk
				Coordinate chunkID = (new Coordinate(x, y, z) / Chunk.Diameter);
				if (chunkID.isWithin(chunkBounds)) {
					Chunk newChunk = new Chunk(chunkID);
					chunks.Add(chunkID, newChunk);
					newChunk[x & 0xF, y & 0xF, z & 0xF] = value;
				} else {
					World.Debug.logError($"Tried to set a value in out of bounds chunk {x}, {y}, {z}");
				}
			}
		}

		/// <summary>
		/// Get a terrain voxel based on it's worldloacation
		/// </summary>
		/// <param name="worldLocation"></param>
		/// <returns></returns>
		public byte this[Coordinate worldLocation] {
			get => this[worldLocation.x, worldLocation.y, worldLocation.z];
			set {
				this[worldLocation.x, worldLocation.y, worldLocation.z] = value;
			}
		}

		/// <summary>
		/// Get the chunk at the given location.
		/// This creates a new chunk if we don't have one
		/// </summary>
		/// <param name="chunkID"></param>
		/// <returns></returns>
		public Chunk getChunk(Coordinate chunkID) {
			if (chunks.TryGetValue(chunkID, out Chunk chunk)) {
				return chunk;
			} else {
				Chunk newChunk = new Chunk(chunkID);
				chunks.Add(chunkID, newChunk);
				return newChunk;
			}
		}

		/// <summary>
		/// Get the voxel at the given world coordinate
		/// </summary>
		/// <param name="worldLocation"></param>
		/// <returns></returns>
		public byte getTerrainVoxel(Coordinate worldLocation) {
			return this[worldLocation.x, worldLocation.y, worldLocation.z];
		}

		/// <summary>
		/// Get the id for the given level focus+
		/// </summary>
		/// <param name="focus"></param>
		/// <returns></returns>
		public int getFocusID(ILevelFocus focus) {
			foreach (KeyValuePair<ILevelFocus, IFocusLens> storedFocus in focalLenses) {
				if (storedFocus.Key == focus) {
					return storedFocus.Key.id;
				}
			}

			return 0;
		}

		/// <summary>
		/// Add a focus to be managed by this level
		/// TODO: the player should be pased in eventually too, and we'll use there prefs to size the lens
		/// </summary>
		/// <param name="newFocus"></param>
		public IFocusLens addPlayerFocus(ILevelFocus newFocus) {
			// register the focus to the level
			newFocus.registerTo(this, ++currentMaxFocusID);
			fociByID[newFocus.id] = newFocus;
			// create a new lens for the focus
			IFocusLens lens = new PlayerLens(newFocus, this, 10, 5);
			// add the lens and focus to the level storage
			focalLenses.Add(newFocus, lens);

			return lens;
		}

		/// <summary>
		/// Get the lens for the given focus
		/// </summary>
		/// <param name="focus"></param>
		/// <returns></returns>
#if DEBUG
		public
#endif
		IFocusLens getLens(ILevelFocus focus) {
			return focalLenses[focus];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="v"></param>
		/// <returns></returns>
#if DEBUG
		public
#endif
		ILevelFocus getFocusByID(int focusID) {
			if (fociByID.TryGetValue(focusID, out ILevelFocus focus)) {
				return focus;
			}

			return null;
		}

		#endregion

		#region Utility Functions

		/// <summary>
		/// Do something for each focus and lens managing it
		/// </summary>
		/// <param name="action"></param>
		public void forEachFocalLens(Action<IFocusLens, ILevelFocus> action) {
			foreach (KeyValuePair<ILevelFocus, IFocusLens> focalLens in focalLenses) {
				action(focalLens.Value, focalLens.Key);
			}
		}

		/// <summary>
		/// Get the priority of the given adjustment for this level as a float value
		/// </summary>
		/// <param name="adjustment"></param>
		/// <returns></returns>
		public float getPriorityForAdjustment(ChunkResolutionAperture.Adjustment adjustment) {
			ILevelFocus focus = fociByID[adjustment.focusID];
			return focalLenses[focus].getAdjustmentPriority(adjustment, focus);
		}

		/// <summary>
		/// Mark a chunk as in need of update
		/// </summary>
		/// <param name="coordinate"></param>
		public void markChunkDirty(Coordinate chunkID) {
			foreach (IFocusLens lens in focalLenses.Values) {
				lens.notifyOf(new ChunkDirtiedEvent(chunkID));
			}
		}

		/// <summary>
		/// Mark a chunk as in need of update
		/// </summary>
		/// <param name="coordinate"></param>
		public void checkChunkFeatureBuffer(Coordinate chunkID) {
			foreach (IFocusLens lens in focalLenses.Values) {
				lens.notifyOf(new ChunkDirtiedEvent(chunkID));
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// An event to notify lenses that a chunk is dirty
		/// </summary>
		public struct ChunkDirtiedEvent : IEvent {
			/// <summary>
			/// The name of this event
			/// </summary>
			public string name {
				get;
			}

			/// <summary>
			/// The dirty chunk
			/// </summary>
			public Coordinate chunkID {
				get;
			}

			public ChunkDirtiedEvent(Coordinate chunkID) {
				name = $"Chunk {chunkID} is dirty";
				this.chunkID = chunkID;
			}
		}

		#endregion
	}
}
