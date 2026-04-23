using Unity.Mathematics;

namespace LittlePhysics
{
    public static class Grid3DExtensions
    {
        /// <summary>
        /// Gets the cell coordinates for a given world position
        /// </summary>
        public static int3 GetCell(this Grid3D grid, float3 pos)
        {
            float3 localPos = pos - grid.Position;
            float3 cellFloat = localPos / grid.CellSize;
            return new int3(
                (int)math.floor(cellFloat.x),
                (int)math.floor(cellFloat.y),
                (int)math.floor(cellFloat.z)
            );
        }

        /// <summary>
        /// Gets the cells that an object with the given position and scale will fit into
        /// </summary>
        public static void GetCells(this Grid3D grid, float3 pos, float scale, out int3 startPosition, out int3 size)
        {
            float halfScale = scale * 0.5f;
            float3 minBounds = pos - halfScale;
            float3 maxBounds = pos + halfScale;

            int3 minCell = grid.GetCell(minBounds);
            int3 maxCell = grid.GetCell(maxBounds);

            startPosition = minCell;
            size = maxCell - minCell + new int3(1, 1, 1);
        }

        /// <summary>
        /// Gets the center position of the specified cell
        /// </summary>
        public static float3 GetCellPosition(this Grid3D grid, int3 cell)
        {
            return grid.Position + ((float3)cell + 0.5f) * grid.CellSize;
        }

        public static int GridCellToIndex(int3 size, int3 cell)
        {
            return cell.z * (size.x * size.y) + cell.y * size.x + cell.x;
        }

        public static int3 IndexToGridCell(int3 size, int index)
        {
            int z = (index / (size.x * size.y)) % size.z;
            int y = (index % (size.x * size.y)) / size.x;
            int x = index % size.x;
            return new int3(x, y, z);
        }

        /// <summary>
        /// Checks if a sphere collides with a cell of the grid
        /// </summary>
        /// <param name="grid">The grid</param>
        /// <param name="gridSize">Size of the grid in cells</param>
        /// <param name="cellIndex">Linear cell index</param>
        /// <param name="spherePosition">World position of the sphere center</param>
        /// <param name="sphereScale">Diameter/scale of the sphere</param>
        /// <returns>True if the sphere collides with the cell</returns>
        public static bool IsCellHasSphere(this Grid3D grid, int3 gridSize, uint cellIndex, float3 spherePosition, float sphereScale)
        {
            int3 cell = IndexToGridCell(gridSize, (int)cellIndex);
            float3 cellCenter = grid.GetCellPosition(cell);

            float halfCellSize = grid.CellSize * 0.5f;
            float3 cellMin = cellCenter - halfCellSize;
            float3 cellMax = cellCenter + halfCellSize;

            float3 closestPoint = math.clamp(spherePosition, cellMin, cellMax);
            float3 diff = spherePosition - closestPoint;
            float distanceSq = math.lengthsq(diff);

            float sphereRadius = sphereScale * 0.5f;
            float radiusSq = sphereRadius * sphereRadius;

            return distanceSq <= radiusSq;
        }
    }
}
