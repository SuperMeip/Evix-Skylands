using UnityEngine;
using System.Collections.Generic;

namespace Evix.Terrain.MeshGeneration {

  /// <summary>
  /// A mesh of tris and verts
  /// </summary>
  public struct ChunkMeshData {

    /// <summary>
    /// the vertices
    /// </summary>
    public List<Vector3> vertices;

    /// <summary>
    /// the vertices
    /// </summary>
    public List<Color> colors;

    /// <summary>
    ///  the triangles
    /// </summary>
    public List<int> triangles;

    /// <summary>
    /// if this mesh is empty
    /// </summary>
    public bool isEmpty
      => triangles == null || triangles.Count == 0;

    /// <summary>
    /// Get the # of triangles in this mesh
    /// </summary>
    public int triangleCount 
      => (triangles?.Count / 3) ?? 0;
  }
}