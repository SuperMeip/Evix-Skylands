using UnityEngine;

namespace Evix.Terrain {

  /// <summary>
  /// Extention to Voxel, for terrain 'block' types
  /// </summary>
  public abstract class TerrainBlock : Voxels.Voxel {

    /// <summary>
    /// A terrain voxel type
    /// </summary>
    public new abstract class Type : Voxels.Voxel.Type {

      /// <summary>
      /// How hard/solid this block is. 0 is air.
      /// </summary>
      public byte Density {
        get;
        protected set;
      } = 64;

      /// <summary>
      /// Make a new type
      /// </summary>
      /// <param name="id"></param>
      internal Type(byte id) : base(id) { }
    }

    /// <summary>
    /// A class for manipulating block types
    /// </summary>
    public new class Types {

      /// <summary>
      /// Air, an empty block
      /// </summary>
      public static Type Air {
        get;
      } = new Air();

      /// <summary>
      /// Stone, a solid rock block
      /// </summary>
      public static Type Stone {
        get;
      } = new Stone();

      /// <summary>
      /// Stone, a solid rock block
      /// </summary>
      public static Type Dirt {
        get;
      } = new Dirt();

      /// <summary>
      /// Stone, a solid rock block
      /// </summary>
      public static Type Grass {
        get;
      } = new Grass();

      /// <summary>
      /// Stone, a solid rock block
      /// </summary>
      public static Type Placeholder {
        get;
      } = new Placeholder();

      /// <summary>
      /// Leaf blocks
      /// </summary>
      public static Type Leaves {
        get;
      } = new Leaves();

      /// <summary>
      /// Leaf blocks
      /// TODO: hardwoood vs softwood
      /// </summary>
      public static Type Wood {
        get;
      } = new Wood();

      /// <summary>
      /// All block types by id
      /// </summary>
      public static Type[] All {
        get;
      } = {
        Air,
        Placeholder,
        Stone,
        Dirt,
        Grass,
        Leaves,
        Wood
      };

      /// <summary>
      /// Get the type by id
      /// </summary>
      /// <param name="id"></param>
      /// <returns></returns>
      public static Type Get(byte id) {
        return All[id];
      }
    }
  }

  /// <summary>
  /// An air block, empty
  /// </summary>
  internal class Air : TerrainBlock.Type {
    internal Air() : base(0) {
      Density = 0;
      IsSolid = false;
    }
  }

  /// <summary>
  /// An empty block that's not air.
  /// Counts as solid but doesn't render
  /// </summary>
  internal class Placeholder : TerrainBlock.Type {
    internal Placeholder() : base(1) {
      Color = new Color(0.8745f, 0.498f, 0.9176f);
    }
  }

  /// <summary>
  /// Stone, a solid rock block
  /// </summary>
  internal class Stone : TerrainBlock.Type {
    internal Stone() : base(2) {
      Density = 128;
      Color = Color.gray;
    }
  }

  /// <summary>
  /// Dirt. a sort of solid block
  /// </summary>
  internal class Dirt : TerrainBlock.Type {
    internal Dirt() : base(3) {
      Density = 54;
      Color = new Color(0.5686f, 0.3176f, 0.0941f);
    }
  }

  /// <summary>
  /// Stone, a solid rock block
  /// </summary>
  internal class Grass : TerrainBlock.Type {
    internal Grass() : base(4) {
      Density = 40;
      Color = Color.green;
    }
  }

  /// <summary>
  /// Stone, a solid rock block
  /// </summary>
  internal class Leaves : TerrainBlock.Type {
    internal Leaves() : base(5) {
      Density = 10;
      Color = Color.green;
    }
  }

  /// <summary>
  /// Stone, a solid rock block
  /// </summary>
  internal class Wood : TerrainBlock.Type {
    internal Wood() : base(6) {
      Density = 75;
      Color = new Color(0.38f, 0.2f, 0.07f);
    }
  }
}
