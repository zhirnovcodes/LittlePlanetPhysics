using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    /// <summary>
    /// Combined iterator for line traversal over a NativeParallelMultiHashMap cell map.
    /// Tracks both the spatial cell iterator and the per-cell multi-hash-map position.
    /// </summary>
    public struct CollisionMapLineIterator
    {
        internal TraverseLineIterator CellIterator;
        internal NativeParallelMultiHashMapIterator<uint> MapIterator;
        internal bool MapIteratorValid;
    }

    /// <summary>
    /// Combined iterator for cube/sphere traversal over a NativeParallelMultiHashMap cell map.
    /// </summary>
    public struct CollisionMapCubeIterator
    {
        internal TraverseCubeIterator CellIterator;
        internal NativeParallelMultiHashMapIterator<uint> MapIterator;
        internal bool MapIteratorValid;
    }

    /// <summary>
    /// Combined iterator for optimized cube/sphere traversal over a NativeParallelMultiHashMap cell map.
    /// </summary>
    public struct CollisionMapCubeOptimizedIterator
    {
        internal TraverseCubeOptimizedIterator CellIterator;
        internal NativeParallelMultiHashMapIterator<uint> MapIterator;
        internal bool MapIteratorValid;
    }

    public static partial class CollisionMapExtensions
    {
        #region Triggers

        /// <summary>
        /// Returns the next trigger entity in the cells that intersect a sphere.
        /// Iterates all entities per cell (multi-hash map).
        /// </summary>
        public static bool TraverseTriggersSphere(
            this NativeParallelMultiHashMap<uint, Entity> triggersMap,
            SpacialMap spacialMap,
            float3 center, float scale,
            int startIndex, int cubeSize,
            ref CollisionMapCubeIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && triggersMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseSphereNext(center, scale, startIndex, cubeSize, ref iterator.CellIterator, out int cellId))
            {
                if (triggersMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next trigger entity in a random-sampled subset of cells that intersect a sphere.
        /// </summary>
        public static bool TraverseTriggersSphereOptimized(
            this NativeParallelMultiHashMap<uint, Entity> triggersMap,
            SpacialMap spacialMap,
            float3 center, float scale,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref CollisionMapCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && triggersMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseSphereOptimizedNext(center, scale, startIndex, cubeSize, maxCells, ref random, ref iterator.CellIterator, out int cellId))
            {
                if (triggersMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next trigger entity in a random-sampled cube of cells.
        /// </summary>
        public static bool TraverseTriggersCubeOptimized(
            this NativeParallelMultiHashMap<uint, Entity> triggersMap,
            SpacialMap spacialMap,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref CollisionMapCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && triggersMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseCubeOptimizedNext(startIndex, cubeSize, maxCells, ref random, ref iterator.CellIterator, out int cellId))
            {
                if (triggersMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next trigger entity in cells crossed by a line segment.
        /// </summary>
        public static bool TraverseTriggersLine(
            this NativeParallelMultiHashMap<uint, Entity> triggersMap,
            SpacialMap spacialMap,
            float3 start, float3 lineVector,
            ref CollisionMapLineIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && triggersMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseLineNext(start, lineVector, ref iterator.CellIterator, out int cellId))
            {
                if (triggersMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Dynamic

        /// <summary>
        /// Returns the next dynamic entity in a random-sampled subset of cells that intersect a sphere.
        /// </summary>
        public static bool TraverseDynamicSphereOptimized(
            this NativeParallelMultiHashMap<uint, Entity> dynamicMap,
            SpacialMap spacialMap,
            float3 center, float scale,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref CollisionMapCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && dynamicMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseSphereOptimizedNext(center, scale, startIndex, cubeSize, maxCells, ref random, ref iterator.CellIterator, out int cellId))
            {
                if (dynamicMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next dynamic entity in all cells of a cube.
        /// </summary>
        public static bool TraverseDynamicCube(
            this NativeParallelMultiHashMap<uint, Entity> dynamicMap,
            SpacialMap spacialMap,
            int startIndex, int cubeSize,
            ref CollisionMapCubeIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && dynamicMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseCubeNext(startIndex, cubeSize, ref iterator.CellIterator, out int cellId))
            {
                if (dynamicMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next dynamic entity in a random-sampled cube of cells.
        /// </summary>
        public static bool TraverseDynamicCubeOptimized(
            this NativeParallelMultiHashMap<uint, Entity> dynamicMap,
            SpacialMap spacialMap,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref CollisionMapCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && dynamicMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseCubeOptimizedNext(startIndex, cubeSize, maxCells, ref random, ref iterator.CellIterator, out int cellId))
            {
                if (dynamicMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the next dynamic entity in cells crossed by a line segment.
        /// </summary>
        public static bool TraverseDynamicLine(
            this NativeParallelMultiHashMap<uint, Entity> dynamicMap,
            SpacialMap spacialMap,
            float3 start, float3 lineVector,
            ref CollisionMapLineIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            if (iterator.MapIteratorValid && dynamicMap.TryGetNextValue(out entity, ref iterator.MapIterator))
                return true;
            iterator.MapIteratorValid = false;
            while (spacialMap.TraverseLineNext(start, lineVector, ref iterator.CellIterator, out int cellId))
            {
                if (dynamicMap.TryGetFirstValue((uint)cellId, out entity, out iterator.MapIterator))
                {
                    iterator.MapIteratorValid = true;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Static (one entity per cell)

        /// <summary>
        /// Returns the next static entity in a random-sampled subset of cells that intersect a sphere.
        /// At most one entity per cell.
        /// </summary>
        public static bool TraverseStaticSphereOptimized(
            this NativeParallelHashMap<int, Entity> staticMap,
            SpacialMap spacialMap,
            float3 center, float scale,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref TraverseCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            while (spacialMap.TraverseSphereOptimizedNext(center, scale, startIndex, cubeSize, maxCells, ref random, ref iterator, out int cellId))
                if (staticMap.TryGetValue(cellId, out entity)) return true;
            return false;
        }

        /// <summary>
        /// Returns the next static entity in a random-sampled cube of cells.
        /// At most one entity per cell.
        /// </summary>
        public static bool TraverseStaticCubeOptimized(
            this NativeParallelHashMap<int, Entity> staticMap,
            SpacialMap spacialMap,
            int startIndex, int cubeSize, int maxCells,
            ref Random random,
            ref TraverseCubeOptimizedIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            while (spacialMap.TraverseCubeOptimizedNext(startIndex, cubeSize, maxCells, ref random, ref iterator, out int cellId))
                if (staticMap.TryGetValue(cellId, out entity)) return true;
            return false;
        }

        /// <summary>
        /// Returns the next static entity in cells crossed by a line segment.
        /// At most one entity per cell.
        /// </summary>
        public static bool TraverseStaticLine(
            this NativeParallelHashMap<int, Entity> staticMap,
            SpacialMap spacialMap,
            float3 start, float3 lineVector,
            ref TraverseLineIterator iterator,
            out Entity entity)
        {
            entity = Entity.Null;
            while (spacialMap.TraverseLineNext(start, lineVector, ref iterator, out int cellId))
                if (staticMap.TryGetValue(cellId, out entity)) return true;
            return false;
        }

        #endregion
    }
}
