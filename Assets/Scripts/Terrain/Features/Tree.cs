namespace Evix.Terrain.Features {
  public class Tree : GeneratedVoxelFeature {
    public Tree(Coordinate root, Coordinate parentChunkID, int seed) 
      : base(root, parentChunkID, seed) {}

    /// <summary>
    /// Generate a basic tree
    /// </summary>
    protected override void generate() {
      float trunkRadius = 1.0f;
      float leafRadius = 3.5f;
      float trunkHeight = 10.0f;

      localRoot = (10, 0, 10);
      bounds = (20, 30, 20);
      voxels = new byte[20 * 30 * 20];

      /// build leaves
      Coordinate leafTuffRoot = localRoot.replaceY((int)trunkHeight + 1);
      (leafTuffRoot - (int)leafRadius).until((leafTuffRoot + (int)leafRadius), voxelLocation => {
        if (voxelLocation.distance(leafTuffRoot) <= leafRadius) {
          voxels[voxelLocation.flatten(30, 20)] = TerrainBlock.Types.Leaves.Id;
        }
      });

      /// build trunk
      Coordinate trunkSweepOffset = new Coordinate((int)trunkRadius, 0, (int)trunkRadius) + (1, 0, 1);
      (localRoot - trunkSweepOffset).until((localRoot + trunkSweepOffset.replaceY((int)trunkHeight)), voxelLocation => {
        if (voxelLocation.distance(localRoot.replaceY(voxelLocation.y)) <= trunkRadius) {
          voxels[voxelLocation.flatten(30, 20)] = TerrainBlock.Types.Wood.Id;
        }
      });
    }
  }
}
