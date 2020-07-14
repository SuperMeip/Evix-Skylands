namespace Evix.Terrain.DataGeneration.Sources {

  public class PlainsSource : VoxelSource {

    /// <summary>
    /// The base sea level for the plain
    /// </summary>
    public int seaLevel = 160;

    /// <summary>
    /// The maximim hill height
    /// </summary>
    public int maxHillHeightVariance = 10;

    /// <summary>
    /// the maximum vally deph in the plains
    /// </summary>
    public int maxValleyDephVarriance = 10;

    public PlainsSource(int seed = 1234) : base(seed) { }

    protected override float getNoiseValueAt(Coordinate location) {
      return noise.GetPerlin(location.x, location.z);
    }

    protected override byte getVoxelTypeFor(float noiseValue, Coordinate location) {
      int surfaceHeightForXZ = seaLevel + (int)noiseValue.scale(maxHillHeightVariance, -maxValleyDephVarriance);
      if (location.y == surfaceHeightForXZ) {
        return TerrainBlock.Types.Grass.Id;
      } else if (location.y < surfaceHeightForXZ) {
        return TerrainBlock.Types.Dirt.Id;
      } else {
        return TerrainBlock.Types.Air.Id;
      }
    }
  }
}
