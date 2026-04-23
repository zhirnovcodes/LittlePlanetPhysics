using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

public struct CollisionPairSingleIterator
{
    internal int CurrentIndex;
    internal int Row;
}

public struct CollisionPairIterator
{
    internal int CurrentIndex;
}

public struct CollisionPairHashMap : IDisposable
{
    [NativeDisableParallelForRestriction]
    private NativeArray<uint> Pairs;

    [NativeDisableParallelForRestriction]
    private NativeArray<int> Counts;

    [NativeDisableParallelForRestriction]
    private NativeArray<int> Locks;

    private readonly int MaxEntities;
    private readonly int MaxPairsPerEntity;

    public CollisionPairHashMap(int maxEntities, int maxPairsPerEntity, Allocator allocator)
    {
        MaxEntities = maxEntities;
        MaxPairsPerEntity = maxPairsPerEntity;

        Pairs = new NativeArray<uint>(maxEntities * maxPairsPerEntity, allocator);
        Counts = new NativeArray<int>(maxEntities, allocator);
        Locks = new NativeArray<int>(maxEntities, allocator);
    }

    public bool IsCreated => Pairs.IsCreated;

    public bool TryAdd(uint entityA, uint entityB)
    {
        // Normalize order: A < B
        if (entityA > entityB)
            (entityA, entityB) = (entityB, entityA);

        if (entityA == entityB)
            return false;

        if (entityA >= MaxEntities)
            return false;

        int Row = (int)entityA;

        SpinLock(Row);
        bool Result = AddPairUnsafe(Row, entityB);
        SpinUnlock(Row);

        return Result;
    }

    public bool CanAdd(uint entityA)
    {
        if (entityA >= MaxEntities)
            return false;

        int Row = (int)entityA;

        SpinLock(Row);
        bool Result = (Counts[Row] < MaxPairsPerEntity);
        SpinUnlock(Row);

        return Result;
    }

    private void SpinLock(int row)
    {
        unsafe
        {
            int* LockPtr = (int*)Locks.GetUnsafePtr() + row;
            while (Interlocked.CompareExchange(ref *LockPtr, 1, 0) != 0)
            {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }
    }

    private void SpinUnlock(int row)
    {
        unsafe
        {
            int* LockPtr = (int*)Locks.GetUnsafePtr() + row;
            Interlocked.Exchange(ref *LockPtr, 0);
        }
    }

    private bool AddPairUnsafe(int row, uint entityB)
    {
        int Count = Counts[row];

        if (Count >= MaxPairsPerEntity)
            return false;

        int BaseIndex = row * MaxPairsPerEntity;

        // Check for duplicate
        for (int i = 0; i < Count; i++)
        {
            if (Pairs[BaseIndex + i] == entityB)
                return false;
        }

        // Add new pair
        Pairs[BaseIndex + Count] = entityB;
        Counts[row] = Count + 1;

        return true;
    }

    public bool Contains(uint entityA, uint entityB)
    {
        if (entityA > entityB)
            (entityA, entityB) = (entityB, entityA);

        if (entityA >= MaxEntities)
            return false;

        int Row = (int)entityA;
        int Count = Counts[Row];
        int BaseIndex = Row * MaxPairsPerEntity;

        for (int i = 0; i < Count; i++)
        {
            if (Pairs[BaseIndex + i] == entityB)
                return true;
        }

        return false;
    }

    public CollisionPairIterator GetIterator()
    {
        return new CollisionPairIterator
        {
            CurrentIndex = 0
        };
    }

    public CollisionPairSingleIterator GetSingleIterator(int row)
    {
        return new CollisionPairSingleIterator
        {
            CurrentIndex = 0,
            Row = row
        };
    }

    public bool Traverse(ref CollisionPairIterator iterator, out (uint, uint) pair)
    {
        while (iterator.CurrentIndex < Pairs.Length)
        {
            int Row = iterator.CurrentIndex / MaxPairsPerEntity;
            int Slot = iterator.CurrentIndex % MaxPairsPerEntity;

            if (Row < MaxEntities && Slot < Counts[Row])
            {
                pair = ((uint)Row, Pairs[iterator.CurrentIndex]);
                iterator.CurrentIndex++;
                return true;
            }

            iterator.CurrentIndex++;
        }

        pair = (0, 0);
        return false;
    }

    public bool Traverse(ref CollisionPairSingleIterator iterator, out (uint, uint) pair)
    {
        var count = Counts[iterator.Row];
        
        if (iterator.CurrentIndex >= count)
        {
            pair = (0, 0);
            return false;
        }
        
        int index = iterator.Row * MaxPairsPerEntity + iterator.CurrentIndex;
        iterator.CurrentIndex++;

        pair = ((uint)iterator.Row, Pairs[index]);
        return true;
    }

    public int GetPairCount()
    {
        int Total = 0;
        for (int i = 0; i < MaxEntities; i++)
        {
            Total += Counts[i];
        }
        return Total;
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
        if (Pairs.IsCreated)
            Pairs.Dispose();

        if (Counts.IsCreated)
            Counts.Dispose();

        if (Locks.IsCreated)
            Locks.Dispose();
    }
}