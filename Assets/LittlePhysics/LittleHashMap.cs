using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

public struct LittleHashMap<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    [NativeDisableParallelForRestriction]
    private NativeArray<T> Values;

    [NativeDisableParallelForRestriction]
    private NativeArray<int> Counts;

    [NativeDisableParallelForRestriction]
    private NativeArray<int> Locks;

    private readonly int MaxEntities;
    private readonly int MaxPairsPerEntity;

    public LittleHashMap(int maxEntities, int maxPairsPerEntity, Allocator allocator)
    {
        MaxEntities = maxEntities;
        MaxPairsPerEntity = maxPairsPerEntity;

        Values = new NativeArray<T>(maxEntities * maxPairsPerEntity, allocator);
        Counts = new NativeArray<int>(maxEntities, allocator);
        Locks = new NativeArray<int>(maxEntities, allocator);
    }

    public bool IsCreated => Values.IsCreated;

    public bool TryAdd(uint entityA, T value)
    {
        if (entityA >= MaxEntities)
            return false;

        int Row = (int)entityA;

        SpinLock(Row);
        bool Result = AddPairUnsafe(Row, value);
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

    private bool AddPairUnsafe(int row, T value)
    {
        int Count = Counts[row];

        if (Count >= MaxPairsPerEntity)
            return false;

        int BaseIndex = row * MaxPairsPerEntity;

        // Check for duplicate using IEquatable<T>
        for (int i = 0; i < Count; i++)
        {
            if (Values[BaseIndex + i].Equals(value))
                return false;
        }

        // Add new value
        Values[BaseIndex + Count] = value;
        Counts[row] = Count + 1;

        return true;
    }

    public bool Contains(uint entityA, T value)
    {
        if (entityA >= MaxEntities)
            return false;

        int Row = (int)entityA;
        int Count = Counts[Row];
        int BaseIndex = Row * MaxPairsPerEntity;

        for (int i = 0; i < Count; i++)
        {
            if (Values[BaseIndex + i].Equals(value))
                return true;
        }

        return false;
    }

    public Iterator GetIterator()
    {
        return new Iterator
        {
            CurrentIndex = 0
        };
    }

    public SingleRowIterator GetSingleIterator(int row)
    {
        return new SingleRowIterator
        {
            CurrentIndex = 0,
            Row = row
        };
    }

    public bool Traverse(ref Iterator iterator, out (uint, T) pair)
    {
        while (iterator.CurrentIndex < Values.Length)
        {
            int Row = iterator.CurrentIndex / MaxPairsPerEntity;
            int Slot = iterator.CurrentIndex % MaxPairsPerEntity;

            if (Row < MaxEntities && Slot < Counts[Row])
            {
                pair = ((uint)Row, Values[iterator.CurrentIndex]);
                iterator.CurrentIndex++;
                return true;
            }

            iterator.CurrentIndex++;
        }

        pair = (0, default);
        return false;
    }

    public bool Traverse(ref SingleRowIterator iterator, out (uint, T) pair)
    {
        var count = Counts[iterator.Row];

        if (iterator.CurrentIndex >= count)
        {
            pair = (0, default);
            return false;
        }

        int index = iterator.Row * MaxPairsPerEntity + iterator.CurrentIndex;
        iterator.CurrentIndex++;

        pair = ((uint)iterator.Row, Values[index]);
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
        if (Values.IsCreated)
            Values.Dispose();

        if (Counts.IsCreated)
            Counts.Dispose();

        if (Locks.IsCreated)
            Locks.Dispose();
    }
    public struct SingleRowIterator
    {
        internal int CurrentIndex;
        internal int Row;
    }

    public struct Iterator
    {
        internal int CurrentIndex;
    }
}