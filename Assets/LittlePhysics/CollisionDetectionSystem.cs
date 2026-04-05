using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CollisionMapUpdateSystem))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        [NoAlias] public NativeParallelMultiHashMap<Entity, CollisionItem> Collisions;
        [NoAlias] public NativeReference<int> CollisionsCount;

        [NoAlias] public NativeParallelHashSet<BodiesPair> Pairs;
        [NoAlias] public NativeReference<int> PairsCount;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (Collisions.IsCreated) Collisions.Dispose();
            if (CollisionsCount.IsCreated) CollisionsCount.Dispose();
            if (Pairs.IsCreated) Pairs.Dispose();
            if (PairsCount.IsCreated) PairsCount.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var mapSystemHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<CollisionMapUpdateSystem>();
            ref var mapSystem = ref state.WorldUnmanaged.GetUnsafeSystemRef<CollisionMapUpdateSystem>(mapSystemHandle);

            if (!mapSystem.DynamicMap.IsCreated)
                return;

            if (!Pairs.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                InitCollections(ref settings.BlobRef.Value);
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            var spacialMapSettings = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                Collisions = Collisions,
                CollisionsCount = CollisionsCount,
                Pairs = Pairs,
                PairsCount = PairsCount,
            }.Schedule(physicsHandle);

            int totalCells = spacialMapSettings.SpacialMap.GetCellsCount();

            var pairsCheckJob = new PairsCheckJob
            {
                DynamicMap = mapSystem.DynamicMap,
                TriggersMap = mapSystem.TriggersMap,
                StaticMap = mapSystem.StaticMap,
                Pairs = Pairs.AsParallelWriter(),
                PairsCount = PairsCount,
                MaxPairs = physicsSettings.BlobRef.Value.GetSumEntitiesXPairs(),
                Bodies = physicsSingleton.Bodies,
                Collisions = Collisions.AsParallelWriter(),
                CollisionsCount = CollisionsCount,
                MaxCollisions = physicsSettings.BlobRef.Value.GetSumEntitiesXCollisions(),
            }.Schedule(totalCells, 16, clearJob);

            state.Dependency = pairsCheckJob;
            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void InitCollections(ref PhysicsSettingsBlobAsset blob)
        {
            Collisions = new NativeParallelMultiHashMap<Entity, CollisionItem>(
                blob.GetSumEntitiesXCollisions(), Allocator.Persistent);

            CollisionsCount = new NativeReference<int>(0, Allocator.Persistent);

            Pairs = new NativeParallelHashSet<BodiesPair>(
                blob.GetSumEntitiesXPairs(), Allocator.Persistent);

            PairsCount = new NativeReference<int>(0, Allocator.Persistent);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeParallelMultiHashMap<Entity, CollisionItem> Collisions;
            public NativeReference<int> CollisionsCount;
            public NativeParallelHashSet<BodiesPair> Pairs;
            public NativeReference<int> PairsCount;

            public void Execute()
            {
                Collisions.Clear();
                CollisionsCount.Value = 0;
                Pairs.Clear();
                PairsCount.Value = 0;
            }
        }

        [BurstCompile]
        private struct PairsCheckJob : IJobParallelFor
        {
            [ReadOnly] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
            [ReadOnly] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
            [ReadOnly] public NativeParallelHashMap<uint, Entity> StaticMap;

            public NativeParallelHashSet<BodiesPair>.ParallelWriter Pairs;
            [NativeDisableContainerSafetyRestriction] public NativeReference<int> PairsCount;

            public int MaxPairs;

            [ReadOnly] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            public NativeParallelMultiHashMap<Entity, CollisionItem>.ParallelWriter Collisions;
            [NativeDisableContainerSafetyRestriction] public NativeReference<int> CollisionsCount;
            public int MaxCollisions;

            public unsafe void Execute(int cellIndex)
            {
                uint cellKey = (uint)cellIndex;

                CheckDynamicVsDynamic(cellKey);
                CheckDynamicVsStatic(cellKey);
                CheckTriggerVsDynamic(cellKey);
                CheckTriggerVsStatic(cellKey);
            }

            private unsafe void CheckDynamicVsDynamic(uint cellKey)
            {
                if (!DynamicMap.TryGetFirstValue(cellKey, out var entity1, out var outerIt))
                    return;

                do
                {
                    var innerIt = outerIt;
                    while (DynamicMap.TryGetNextValue(out var entity2, ref innerIt))
                    {
                        if (PairsCount.Value >= MaxPairs)
                            return;
                        if (entity1.Equals(entity2)) continue;
                        if (entity1.CompareTo(entity2) > 0) continue;

                        var pair = new BodiesPair { Entity1 = entity1, Entity2 = entity2 };
                        if (Pairs.Add(pair))
                        {
                            Interlocked.Increment(ref UnsafeUtility.AsRef<int>(PairsCount.GetUnsafePtr()));
                            CheckCollision(pair);
                        }
                    }
                }
                while (DynamicMap.TryGetNextValue(out entity1, ref outerIt));
            }

            private unsafe void CheckDynamicVsStatic(uint cellKey)
            {
                if (PairsCount.Value >= MaxPairs) return;

                if (!StaticMap.TryGetValue(cellKey, out var staticEntity))
                    return;

                if (!DynamicMap.TryGetFirstValue(cellKey, out var dynEntity, out var dynIt))
                    return;

                do
                {
                    if (PairsCount.Value >= MaxPairs)
                        return;
                    TryAddOrderedPair(dynEntity, staticEntity);
                }
                while (DynamicMap.TryGetNextValue(out dynEntity, ref dynIt));
            }

            private unsafe void CheckTriggerVsDynamic(uint cellKey)
            {
                if (PairsCount.Value >= MaxPairs) return;

                if (!TriggersMap.TryGetFirstValue(cellKey, out var trigEntity, out var trigIt))
                    return;

                do
                {
                    if (!DynamicMap.TryGetFirstValue(cellKey, out var dynEntity, out var dynIt))
                        continue;

                    do
                    {
                        if (PairsCount.Value >= MaxPairs) return;
                        TryAddOrderedPair(trigEntity, dynEntity);
                    }
                    while (DynamicMap.TryGetNextValue(out dynEntity, ref dynIt));
                }
                while (TriggersMap.TryGetNextValue(out trigEntity, ref trigIt));
            }

            private unsafe void CheckTriggerVsStatic(uint cellKey)
            {
                if (PairsCount.Value >= MaxPairs)
                    return;

                if (!StaticMap.TryGetValue(cellKey, out var staticEntity))
                    return;

                if (!TriggersMap.TryGetFirstValue(cellKey, out var trigEntity, out var trigIt))
                    return;

                do
                {
                    if (PairsCount.Value >= MaxPairs)
                        return;
                    TryAddOrderedPair(trigEntity, staticEntity);
                }
                while (TriggersMap.TryGetNextValue(out trigEntity, ref trigIt));
            }

            private unsafe void TryAddOrderedPair(Entity a, Entity b)
            {
                Entity e1 = a.CompareTo(b) <= 0 ? a : b;
                Entity e2 = a.CompareTo(b) <= 0 ? b : a;

                var pair = new BodiesPair { Entity1 = e1, Entity2 = e2 };
                if (Pairs.Add(pair))
                {
                    Interlocked.Increment(ref UnsafeUtility.AsRef<int>(PairsCount.GetUnsafePtr()));
                    CheckCollision(pair);
                }
            }

            private unsafe void CheckCollision(BodiesPair pair)
            {
                if (!Bodies.TryGetValue(pair.Entity1, out var body1)) return;
                if (!Bodies.TryGetValue(pair.Entity2, out var body2)) return;

                if (!CollisionMethods.AreBodiesColliding(body1, body2, out var contactPoint)) return;

                int prev = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(CollisionsCount.GetUnsafePtr())) - 1;
                if (prev >= MaxCollisions)
                {
                    Interlocked.Decrement(ref UnsafeUtility.AsRef<int>(CollisionsCount.GetUnsafePtr()));
                    return;
                }

                Collisions.Add(pair.Entity1, new CollisionItem { Target = pair.Entity2, ContactPoint = contactPoint });
                Collisions.Add(pair.Entity2, new CollisionItem { Target = pair.Entity1, ContactPoint = contactPoint });
            }
        }
    }
}
