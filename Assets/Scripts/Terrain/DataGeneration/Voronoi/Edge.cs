namespace Evix.Terrain.DataGeneration.Voronoi {

  /// <summary>
  /// A voronoi edge connecting two voronoi shape corners
  /// </summary>
  class Edge {

    /// <summary>
    /// The IDs of the start and end corner points of this edge in the Delanuay Diagram
    /// </summary>
    public Corner start, end;

    public Edge(Corner start, Corner end) {
      this.start = start;
      this.end = end;
    }

    public bool ContainsVertex(Corner point) {
      return (start == point || end == point);
    }

    #region Equality Opperators

    public override bool Equals(object obj) {
      return this == (Edge)obj;
    }

    public static bool operator ==(Edge a, Edge b) {
      if (a == b) {
        return true;
      }

      if (a == null || b == null) {
        return false;
      }

      return ((a.start == b.start && a.end == b.end) ||
               (a.start == b.end && a.end == b.start));
    }

    public static bool operator !=(Edge a, Edge b) {
      return !(a == b);
    }

    public override int GetHashCode() {
      return start.GetHashCode() ^ end.GetHashCode();
    }

    #endregion
  }
}
