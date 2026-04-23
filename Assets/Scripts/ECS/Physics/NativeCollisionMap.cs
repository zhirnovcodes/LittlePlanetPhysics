using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct NativeCollisionMapIterator
    {
        internal int BaseIndex;
        internal int Count;
        internal int CurrentIndex;
    }

    public struct NativeCollisionMap : IDisposable
    {
        [NativeDisableParallelForRestriction]
        private NativeArray<uint> Map;

        [NativeDisableParallelForRestriction]
        private NativeArray<int> Counts;

        private readonly int3 MapSize;
        private readonly int EntitiesPerCell;
        private readonly int TotalCells;

        public NativeCollisionMap(int3 mapSize, uint entitiesPerCell, Allocator allocator)
        {
            MapSize = mapSize;
            EntitiesPerCell = (int)entitiesPerCell;
            TotalCells = MapSize.x * MapSize.y * MapSize.z;

            Map = new NativeArray<uint>(TotalCells * EntitiesPerCell, allocator);
            Counts = new NativeArray<int>(TotalCells, allocator);
        }

        public bool IsCreated => Map.IsCreated;

        // Convert 3D index to linear index
        public int GetCellIndex(uint3 cellIndex)
        {
            return (int)(cellIndex.x + cellIndex.y * MapSize.x + cellIndex.z * MapSize.x * MapSize.y);
        }

        public bool TryAdd(uint cellIndex, uint value)
        {
            if (cellIndex >= TotalCells)
                return false;

            unsafe
            {
                int* countPtr = (int*)Counts.GetUnsafePtr() + (int)cellIndex;

                int slot;
                int current;
                do
                {
                    current = Volatile.Read(ref *countPtr);
                    if (current >= EntitiesPerCell)
                        return false;
                } while (Interlocked.CompareExchange(ref *countPtr, current + 1, current) != current);
                slot = current;
                Map[(int)cellIndex * EntitiesPerCell + slot] = value;
                return true;
            }
        }

        public bool TryAdd(uint3 cellIndex, uint value)
        {
            return TryAdd((uint)GetCellIndex(cellIndex), value);
        }

        public NativeCollisionMapIterator GetCellIterator(uint cellIndex)
        {
            int count = 0;

            if (cellIndex < TotalCells)
            {
                count = math.min(Counts[(int)cellIndex], EntitiesPerCell);
            }

            return new NativeCollisionMapIterator
            {
                BaseIndex = (int)cellIndex * EntitiesPerCell,
                Count = count,
                CurrentIndex = -1
            };
        }

        public NativeCollisionMapIterator GetCellIterator(uint3 cellIndex)
        {
            return GetCellIterator((uint)GetCellIndex(cellIndex));
        }

        public bool TraverseCell(ref NativeCollisionMapIterator iterator, out uint entityIndex)
        {
            iterator.CurrentIndex++;

            if (iterator.CurrentIndex < iterator.Count)
            {
                entityIndex = Map[iterator.BaseIndex + iterator.CurrentIndex];
                return true;
            }

            entityIndex = 0;
            return false;
        }

        public int GetCellCount(uint cellIndex)
        {
            if (cellIndex >= TotalCells)
                return 0;

            return math.min(Counts[(int)cellIndex], EntitiesPerCell);
        }

        public int GetCellCount(uint3 cellIndex)
        {
            return GetCellCount((uint)GetCellIndex(cellIndex));
        }

        public void Clear()
        {
            unsafe
            {
                UnsafeUtility.MemClear(Counts.GetUnsafePtr(), Counts.Length * sizeof(int));
            }
        }

        public void Dispose()
        {
            if (Map.IsCreated)
                Map.Dispose();

            if (Counts.IsCreated)
                Counts.Dispose();
        }
    }
}
