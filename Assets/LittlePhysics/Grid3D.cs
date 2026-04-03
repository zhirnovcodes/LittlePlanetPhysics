using Unity.Mathematics;

namespace LittlePhysics
{
    /// <summary>
    /// Represents a 3D cubic grid with a minimum corner position and cell size
    /// </summary>
    public struct Grid3D
    {
        public float CellSize;
        public float3 Position; // Minimum corner position of cell (0,0,0)
    }
}
