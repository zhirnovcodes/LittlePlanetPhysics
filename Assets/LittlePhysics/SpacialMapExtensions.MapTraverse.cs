using Unity.Mathematics;

namespace LittlePhysics
{
    /// <summary>
    /// Iterator structure for line traversal through the grid using DDA algorithm
    /// </summary>
    public struct TraverseLineIterator
    {
        public int3 CurrentCell;
        public int3 Step;

        // Distance along line to next cell boundary for each axis (t=1 is end)
        public float3 TMax;

        // Distance along line to cross one cell for each axis
        public float3 TDelta;

        public float MaxT;
        public float CurrentT;
        public bool Started;
        public bool IsValid;
    }

    /// <summary>
    /// Iterator structure for cube traversal through the grid
    /// </summary>
    public struct TraverseCubeIterator
    {
        // Actual size after clamping to spatial map boundaries
        public int3 ActualObjectSize;

        internal int3 StartPoint;
        internal int CurrentIndex;
        internal int3 GridSize;
        internal bool IsValid;
        internal bool IsComplete;
    }

    /// <summary>
    /// Iterator structure for optimized cube traversal through the grid with random sampling
    /// </summary>
    public struct TraverseCubeOptimizedIterator
    {
        // Actual size after clamping to spatial map boundaries
        public int3 ActualObjectSize;

        internal int3 StartPoint;
        internal int CurrentIndex;
        internal int MinIndex;
        internal int MaxIndex;
        internal int MaxStep;
        internal int MinStep;
        internal int3 GridSize;
        internal int MaxCells;
        internal int CellsFound;
        internal bool IsValid;
        internal bool IsComplete;
    }

    public static partial class SpacialMapExtensions
    {
        private static bool isCellInBounds(int3 cell, int3 gridSize)
        {
            return cell.x >= 0 && cell.x < gridSize.x &&
                   cell.y >= 0 && cell.y < gridSize.y &&
                   cell.z >= 0 && cell.z < gridSize.z;
        }

        #region Line Traversal

        /// <summary>
        /// Traverses to the next cell along a line segment through the grid using 3D DDA.
        /// Handles both initialization (first call) and subsequent iterations.
        /// </summary>
        /// <param name="spacialMap">The spatial map</param>
        /// <param name="start">Line start position</param>
        /// <param name="lineVector">Line vector (end = start + lineVector)</param>
        /// <param name="iterator">Iterator state</param>
        /// <param name="cellIndex">Output: Linear cell index of the current cell</param>
        /// <returns>True if a valid cell was found, false if traversal is complete or invalid</returns>
        public static bool TraverseLineNext(
            this SpacialMap spacialMap,
            float3 start,
            float3 lineVector,
            ref TraverseLineIterator iterator,
            out int cellIndex)
        {
            cellIndex = -1;

            if (!iterator.Started)
            {
                initializeIterator(ref spacialMap, start, lineVector, ref iterator);
                iterator.Started = true;

                if (!iterator.IsValid)
                    return false;

                cellIndex = Grid3DExtensions.GridCellToIndex(spacialMap.GridSize, iterator.CurrentCell);
                return true;
            }

            if (!iterator.IsValid)
                return false;

            while (iterator.CurrentT < iterator.MaxT)
            {
                if (iterator.TMax.x < iterator.TMax.y)
                {
                    if (iterator.TMax.x < iterator.TMax.z)
                    {
                        if (iterator.TMax.x > iterator.MaxT) break;
                        iterator.CurrentCell.x += iterator.Step.x;
                        iterator.CurrentT = iterator.TMax.x;
                        iterator.TMax.x += iterator.TDelta.x;
                    }
                    else
                    {
                        if (iterator.TMax.z > iterator.MaxT) break;
                        iterator.CurrentCell.z += iterator.Step.z;
                        iterator.CurrentT = iterator.TMax.z;
                        iterator.TMax.z += iterator.TDelta.z;
                    }
                }
                else
                {
                    if (iterator.TMax.y < iterator.TMax.z)
                    {
                        if (iterator.TMax.y > iterator.MaxT) break;
                        iterator.CurrentCell.y += iterator.Step.y;
                        iterator.CurrentT = iterator.TMax.y;
                        iterator.TMax.y += iterator.TDelta.y;
                    }
                    else
                    {
                        if (iterator.TMax.z > iterator.MaxT) break;
                        iterator.CurrentCell.z += iterator.Step.z;
                        iterator.CurrentT = iterator.TMax.z;
                        iterator.TMax.z += iterator.TDelta.z;
                    }
                }

                if (isCellInBounds(iterator.CurrentCell, spacialMap.GridSize))
                {
                    cellIndex = Grid3DExtensions.GridCellToIndex(spacialMap.GridSize, iterator.CurrentCell);
                    return true;
                }

                return false;
            }

            return false;
        }

        private static void initializeIterator(
            ref SpacialMap spacialMap,
            float3 start,
            float3 lineVector,
            ref TraverseLineIterator iterator)
        {
            float cellSize = spacialMap.Grid.CellSize;
            float3 gridOrigin = spacialMap.Grid.Position;
            int3 gridSize = spacialMap.GridSize;

            float3 gridMin = gridOrigin;
            float3 gridMax = gridOrigin + (float3)gridSize * cellSize;

            if (!lineAABBIntersection(start, lineVector, gridMin, gridMax, out float tMin, out float tMax))
            {
                iterator.IsValid = false;
                return;
            }

            tMin = math.max(tMin, 0f);
            tMax = math.min(tMax, 1f);

            if (tMin > tMax)
            {
                iterator.IsValid = false;
                return;
            }

            iterator.IsValid = true;
            iterator.MaxT = tMax;
            iterator.CurrentT = tMin;

            float3 startPos = start + lineVector * tMin;
            float3 localPos = startPos - gridOrigin;

            iterator.CurrentCell = (int3)math.floor(localPos / cellSize);
            iterator.CurrentCell = math.clamp(iterator.CurrentCell, int3.zero, gridSize - 1);

            iterator.Step = new int3(
                lineVector.x >= 0 ? 1 : -1,
                lineVector.y >= 0 ? 1 : -1,
                lineVector.z >= 0 ? 1 : -1
            );

            float3 absLineVector = math.abs(lineVector);
            iterator.TDelta = new float3(
                absLineVector.x > 1e-8f ? cellSize / absLineVector.x : float.MaxValue,
                absLineVector.y > 1e-8f ? cellSize / absLineVector.y : float.MaxValue,
                absLineVector.z > 1e-8f ? cellSize / absLineVector.z : float.MaxValue
            );

            float3 cellMin = (float3)iterator.CurrentCell * cellSize;
            float3 cellMax = cellMin + cellSize;

            iterator.TMax = new float3(
                absLineVector.x > 1e-8f
                    ? tMin + (lineVector.x >= 0 ? (cellMax.x - localPos.x) : (localPos.x - cellMin.x)) / absLineVector.x
                    : float.MaxValue,
                absLineVector.y > 1e-8f
                    ? tMin + (lineVector.y >= 0 ? (cellMax.y - localPos.y) : (localPos.y - cellMin.y)) / absLineVector.y
                    : float.MaxValue,
                absLineVector.z > 1e-8f
                    ? tMin + (lineVector.z >= 0 ? (cellMax.z - localPos.z) : (localPos.z - cellMin.z)) / absLineVector.z
                    : float.MaxValue
            );
        }

        private static bool lineAABBIntersection(
            float3 start,
            float3 lineVector,
            float3 boxMin,
            float3 boxMax,
            out float tMin,
            out float tMax)
        {
            float3 invDir = new float3(
                math.abs(lineVector.x) > 1e-8f ? 1f / lineVector.x : (lineVector.x >= 0 ? float.MaxValue : float.MinValue),
                math.abs(lineVector.y) > 1e-8f ? 1f / lineVector.y : (lineVector.y >= 0 ? float.MaxValue : float.MinValue),
                math.abs(lineVector.z) > 1e-8f ? 1f / lineVector.z : (lineVector.z >= 0 ? float.MaxValue : float.MinValue)
            );

            float3 t1 = (boxMin - start) * invDir;
            float3 t2 = (boxMax - start) * invDir;

            float3 tMinVec = math.min(t1, t2);
            float3 tMaxVec = math.max(t1, t2);

            tMin = math.max(math.max(tMinVec.x, tMinVec.y), tMinVec.z);
            tMax = math.min(math.min(tMaxVec.x, tMaxVec.y), tMaxVec.z);

            return tMax >= tMin;
        }

        #endregion

        #region Cube Traversal

        /// <summary>
        /// Initializes the cube traversal iterator and returns the first cell.
        /// </summary>
        public static bool InitializeCubeTraverse(this SpacialMap spacialMap, int startIndex, int cubeSize, ref TraverseCubeIterator iterator, out int cellId)
        {
            cellId = -1;

            int3 startCell = spacialMap.IndexToCell(startIndex);
            int3 clampedStartCell = math.max(startCell, new int3(0, 0, 0));

            int3 actualSize = math.min(
                new int3(cubeSize, cubeSize, cubeSize),
                spacialMap.GridSize - clampedStartCell
            );

            if (actualSize.x <= 0 || actualSize.y <= 0 || actualSize.z <= 0)
            {
                iterator = default;
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            iterator.ActualObjectSize = actualSize;
            iterator.StartPoint = clampedStartCell;
            iterator.CurrentIndex = 0;
            iterator.GridSize = spacialMap.GridSize;
            iterator.IsValid = true;
            iterator.IsComplete = false;

            cellId = Grid3DExtensions.GridCellToIndex(spacialMap.GridSize, clampedStartCell);
            return true;
        }

        /// <summary>
        /// Traverses to the next cell in a cube through the grid.
        /// Initializes the iterator on the first call.
        /// </summary>
        public static bool TraverseCubeNext(this SpacialMap spacialMap, int startIndex, int cubeSize, ref TraverseCubeIterator iterator, out int cellId)
        {
            cellId = -1;

            if (!iterator.IsValid)
                return spacialMap.InitializeCubeTraverse(startIndex, cubeSize, ref iterator, out cellId);

            iterator.CurrentIndex++;

            int totalCells = iterator.ActualObjectSize.x * iterator.ActualObjectSize.y * iterator.ActualObjectSize.z;

            if (iterator.CurrentIndex >= totalCells)
            {
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            int3 localOffset = Grid3DExtensions.IndexToGridCell(iterator.ActualObjectSize, iterator.CurrentIndex);
            int3 currentCellCoord = iterator.StartPoint + localOffset;

            cellId = Grid3DExtensions.GridCellToIndex(iterator.GridSize, currentCellCoord);
            return true;
        }

        /// <summary>
        /// Initializes the optimized cube traversal iterator with random sampling.
        /// </summary>
        public static bool InitializeCubeOptimizedTraverse(this SpacialMap spacialMap, int startIndex, int cubeSize, int maxCells, ref Unity.Mathematics.Random random, ref TraverseCubeOptimizedIterator iterator, out int cellId)
        {
            cellId = -1;

            int3 startCell = spacialMap.IndexToCell(startIndex);
            int3 clampedStartCell = math.max(startCell, new int3(0, 0, 0));

            int3 actualSize = math.min(
                new int3(cubeSize, cubeSize, cubeSize),
                spacialMap.GridSize - clampedStartCell
            );

            if (actualSize.x <= 0 || actualSize.y <= 0 || actualSize.z <= 0)
            {
                iterator = default;
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            int totalCells = actualSize.x * actualSize.y * actualSize.z;
            int maxIndex = totalCells - 1;

            int maxStep = math.max(1, (int)math.ceil((float)totalCells / (float)maxCells));
            int minStep = math.max(1, maxStep - 1);

            int randomStartIndex = random.NextInt(0, maxStep + 1);

            iterator.ActualObjectSize = actualSize;
            iterator.StartPoint = clampedStartCell;
            iterator.CurrentIndex = randomStartIndex;
            iterator.MinIndex = 0;
            iterator.MaxIndex = maxIndex;
            iterator.MaxStep = maxStep;
            iterator.MinStep = minStep;
            iterator.GridSize = spacialMap.GridSize;
            iterator.MaxCells = maxCells;
            iterator.CellsFound = 1;
            iterator.IsValid = true;
            iterator.IsComplete = false;

            int3 localOffset = Grid3DExtensions.IndexToGridCell(actualSize, randomStartIndex);
            int3 firstCellCoord = clampedStartCell + localOffset;
            cellId = Grid3DExtensions.GridCellToIndex(spacialMap.GridSize, firstCellCoord);

            return true;
        }

        /// <summary>
        /// Traverses to the next cell in a cube with random sampling.
        /// Limits returned cells to maxCells.
        /// </summary>
        public static bool TraverseCubeOptimizedNext(this SpacialMap spacialMap, int startIndex, int cubeSize, int maxCells, ref Unity.Mathematics.Random random, ref TraverseCubeOptimizedIterator iterator, out int cellId)
        {
            cellId = -1;

            if (!iterator.IsValid)
                return spacialMap.InitializeCubeOptimizedTraverse(startIndex, cubeSize, maxCells, ref random, ref iterator, out cellId);

            if (iterator.CellsFound >= iterator.MaxCells)
            {
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            int remainingIndices = iterator.MaxIndex - iterator.CurrentIndex;

            if (remainingIndices <= 0)
            {
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            int maxStep = math.min(iterator.MaxStep, remainingIndices);
            int minStep = math.min(iterator.MinStep, maxStep);

            int stepRange = maxStep - minStep + 1;
            int randomStep = minStep + random.NextInt(0, stepRange);

            iterator.CurrentIndex += randomStep;

            if (iterator.CurrentIndex > iterator.MaxIndex)
            {
                iterator.IsComplete = true;
                iterator.IsValid = false;
                return false;
            }

            int3 localOffset = Grid3DExtensions.IndexToGridCell(iterator.ActualObjectSize, iterator.CurrentIndex);
            int3 currentCellCoord = iterator.StartPoint + localOffset;

            cellId = Grid3DExtensions.GridCellToIndex(iterator.GridSize, currentCellCoord);
            iterator.CellsFound++;
            return true;
        }

        #endregion

        #region Sphere Traversal

        /// <summary>
        /// Traverses to the next cell in a sphere through the grid.
        /// Iterates the bounding cube and skips cells not intersecting the sphere.
        /// </summary>
        public static bool TraverseSphereNext(this SpacialMap spacialMap, float3 center, float scale, int startIndex, int cubeSize, ref TraverseCubeIterator iterator, out int cellId)
        {
            cellId = -1;

            while (spacialMap.TraverseCubeNext(startIndex, cubeSize, ref iterator, out int cubeCellId))
            {
                if (spacialMap.Grid.IsCellHasSphere(spacialMap.GridSize, (uint)cubeCellId, center, scale))
                {
                    cellId = cubeCellId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Traverses to the next cell in a sphere through the grid with random sampling.
        /// </summary>
        public static bool TraverseSphereOptimizedNext(this SpacialMap spacialMap, float3 center, float scale, int startIndex, int cubeSize, int maxCells, ref Unity.Mathematics.Random random, ref TraverseCubeOptimizedIterator iterator, out int cellId)
        {
            cellId = -1;

            while (spacialMap.TraverseCubeOptimizedNext(startIndex, cubeSize, maxCells, ref random, ref iterator, out int cubeCellId))
            {
                if (spacialMap.Grid.IsCellHasSphere(spacialMap.GridSize, (uint)cubeCellId, center, scale))
                {
                    cellId = cubeCellId;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
