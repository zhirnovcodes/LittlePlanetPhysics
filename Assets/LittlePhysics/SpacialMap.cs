using Unity.Mathematics;

namespace LittlePhysics
{
    /// <summary>
    /// Represents a spatial map with a 3D grid and grid dimensions
    /// </summary>
    public struct SpacialMap
    {
        public Grid3D Grid;
        public int3 GridSize;

        public int GetCellsCount()
        {
            return GridSize.x * GridSize.y * GridSize.z;
        }
    }
}
