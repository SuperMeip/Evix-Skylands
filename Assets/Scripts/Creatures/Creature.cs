namespace Evix.Creatures {

  /// <summary>
  /// A living thing that can be killed and has basic stats
  /// </summary>
  public abstract class Creature {

    /// <summary>
    /// The creature's individual name
    /// </summary>
    public string name {
      get;
      private set;
    }
  }
}
