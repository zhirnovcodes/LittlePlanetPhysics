using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

namespace LittleAI.Collections
{
    public struct LittleParallelArray<T> : IDisposable where T : unmanaged
    {
        [NativeDisableParallelForRestriction]
        private NativeArray<T> Values;

        [NativeDisableParallelForRestriction]
        private NativeArray<int> Locks;

        public int Length => Values.Length;
        public bool IsCreated => Values.IsCreated;

        public LittleParallelArray(int length, Allocator allocator)
        {
            Values = new NativeArray<T>(length, allocator);
            Locks = new NativeArray<int>(length, allocator);
        }

        public T this[int index]
        {
            get => GetValue(index);
            set => SetValue(index, value);
        }

        public T GetValue(int index)
        {
            SpinLock(index);
            var value = Values[index];
            SpinUnlock(index);
            return value;
        }

        public void SetValue(int index, T value)
        {
            SpinLock(index);
            Values[index] = value;
            SpinUnlock(index);
        }

        public bool TryGetValue(int index, out T value)
        {
            if (!TrySpinLock(index))
            {
                value = default;
                return false;
            }

            value = Values[index];
            SpinUnlock(index);
            return true;
        }

        public bool TrySetValue(int index, T value)
        {
            if (!TrySpinLock(index))
            {
                return false;
            }

            Values[index] = value;
            SpinUnlock(index);
            return true;
        }

        public void Clear()
        {
            unsafe
            {
                UnsafeUtility.MemClear(Values.GetUnsafePtr(), Values.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        private void SpinLock(int index)
        {
            unsafe
            {
                int* lockPtr = (int*)Locks.GetUnsafePtr() + index;
                while (Interlocked.CompareExchange(ref *lockPtr, 1, 0) != 0)
                {
                    Unity.Burst.Intrinsics.Common.Pause();
                }
            }
        }

        private bool TrySpinLock(int index)
        {
            unsafe
            {
                int* lockPtr = (int*)Locks.GetUnsafePtr() + index;
                return Interlocked.CompareExchange(ref *lockPtr, 1, 0) == 0;
            }
        }

        private void SpinUnlock(int index)
        {
            unsafe
            {
                int* lockPtr = (int*)Locks.GetUnsafePtr() + index;
                Interlocked.Exchange(ref *lockPtr, 0);
            }
        }

        public void Dispose()
        {
            if (Values.IsCreated)
            {
                Values.Dispose();
            }

            if (Locks.IsCreated)
            {
                Locks.Dispose();
            }
        }
    }
}