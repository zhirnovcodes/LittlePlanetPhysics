using Unity.Mathematics;

namespace LittlePhysics
{
    public static partial class SpacialMapExtensions
    {
        /// <summary>
        /// Checks if a world position is within the grid bounds
        /// </summary>
        public static bool IsInGrid(this SpacialMap spacialMap, float3 position)
        {
            int3 cell = spacialMap.Grid.GetCell(position);

            return cell.x >= 0 && cell.x < spacialMap.GridSize.x &&
                   cell.y >= 0 && cell.y < spacialMap.GridSize.y &&
                   cell.z >= 0 && cell.z < spacialMap.GridSize.z;
        }

        /// <summary>
        /// Gets the cell indices range that an object with the given position and scale occupies
        /// </summary>
        /// <param name="spacialMap">The spatial map</param>
        /// <param name="position">World position of the object</param>
        /// <param name="scale">Scale/size of the object</param>
        /// <param name="startIndex">Output: Starting linear cell index</param>
        /// <param name="size">Output: Number of cells in each dimension (clamped to grid bounds)</param>
        public static void GetCellIndices(this SpacialMap spacialMap, float3 position, float scale, out int startIndex, out int3 size)
        {
            spacialMap.Grid.GetCells(position, scale, out int3 startCell, out int3 cellSize);

            int3 gridSize = spacialMap.GridSize;
            int3 minCellCoord = new int3(0, 0, 0);
            int3 maxCellCoord = gridSize - new int3(1, 1, 1);

            startCell = math.max(startCell, minCellCoord);
            int3 endCell = math.min(startCell + cellSize - new int3(1, 1, 1), maxCellCoord);
            size = endCell - startCell + new int3(1, 1, 1);

            int3 relativeStartCell = startCell - minCellCoord;
            startIndex = relativeStartCell.z * (gridSize.x * gridSize.y) +
                         relativeStartCell.y * gridSize.x +
                         relativeStartCell.x;
        }

        /// <summary>
        /// Converts a cell coordinate offset to a linear cell index
        /// </summary>
        public static int GetCellIndex(this SpacialMap spacialMap, int startIndex, int3 cellOffset)
        {
            int3 gridSize = spacialMap.GridSize;
            int offsetLinearIndex = cellOffset.z * (gridSize.x * gridSize.y) +
                                    cellOffset.y * gridSize.x +
                                    cellOffset.x;
            return startIndex + offsetLinearIndex;
        }

        /// <summary>
        /// Tries to convert a cell coordinate offset to a linear cell index.
        /// Returns false if the resulting index is out of bounds.
        /// </summary>
        public static bool TryGetCellIndex(this SpacialMap spacialMap, int startIndex, int3 cellOffset, out int cellIndex)
        {
            int3 gridSize = spacialMap.GridSize;
            int offsetLinearIndex = cellOffset.z * (gridSize.x * gridSize.y) +
                                    cellOffset.y * gridSize.x +
                                    cellOffset.x;
            cellIndex = startIndex + offsetLinearIndex;

            int totalCells = gridSize.x * gridSize.y * gridSize.z;
            return cellIndex >= 0 && cellIndex < totalCells;
        }

        /// <summary>
        /// Converts a linear cell index to cell coordinates
        /// </summary>
        public static int3 IndexToCell(this SpacialMap spacialMap, int index)
        {
            int3 gridSize = spacialMap.GridSize;
            int z = index / (gridSize.x * gridSize.y);
            int y = (index % (gridSize.x * gridSize.y)) / gridSize.x;
            int x = index % gridSize.x;
            return new int3(x, y, z);
        }

        /// <summary>
        /// Gets the world position of the cell center at the given linear index
        /// </summary>
        public static float3 GetCellPosition(this SpacialMap spacialMap, int index)
        {
            var cell = spacialMap.IndexToCell(index);
            return spacialMap.Grid.GetCellPosition(cell);
        }

        /// <summary>
        /// Checks if a cell coordinate is within the grid bounds
        /// </summary>
        public static bool IsInBounds(this SpacialMap spacialMap, int3 cell)
        {
            return cell.x >= 0 && cell.x < spacialMap.GridSize.x &&
                   cell.y >= 0 && cell.y < spacialMap.GridSize.y &&
                   cell.z >= 0 && cell.z < spacialMap.GridSize.z;
        }
    }
}
