using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct CollisionData : IEquatable<CollisionData>
    {
        public uint Body1;
        public uint Body2;
        public float3 ContactPoint;

        public CollisionData(uint body1, uint body2, float3 contactPoint)
        {
            Body1 = body1;
            Body2 = body2;
            ContactPoint = contactPoint;
        }

        public bool Equals(CollisionData other)
        {
            return (Body1 == other.Body1 && Body2 == other.Body2)
                || (Body1 == other.Body2 && Body2 == other.Body1);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CollisionMapUpdateSystem))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        [NoAlias] public NativeParallelMultiHashMap<Entity, CollisionItem> Collisions;

        [NoAlias] public LittleHashMap<uint> Pairs;
        [NoAlias] public LittleHashMap<CollisionData> CollisionsNew;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (Collisions.IsCreated) Collisions.Dispose();
            if (CollisionsNew.IsCreated) CollisionsNew.Dispose();
            if (Pairs.IsCreated) Pairs.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Pairs.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                InitCollections(ref settings.BlobRef.Value);
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!physicsSingleton.CollisionMap.DynamicCollisionMap.IsCreated)
                return;
            var spacialMapSettings = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                CollisionsMap = CollisionsNew,
                PairMap = Pairs,
            }.Schedule(physicsHandle);

            int totalCells = spacialMapSettings.SpacialMap.GetCellsCount();

            var pairsCheckJob = new PairsCheckJob
            {
                DynamicCollisionMap = physicsSingleton.CollisionMap.DynamicCollisionMap,
                StaticCollisionMap = physicsSingleton.CollisionMap.StaticCollisionMap,
                TriggersCollisionMap = physicsSingleton.CollisionMap.TriggersCollisionMap,
                BodiesList = physicsSingleton.BodiesList,
                PairMap = Pairs,
                CollisionsMap = CollisionsNew,
            }.Schedule(totalCells, 16, clearJob);

            state.Dependency = pairsCheckJob;
            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void InitCollections(ref PhysicsSettingsBlobAsset blob)
        {
            Collisions = new NativeParallelMultiHashMap<Entity, CollisionItem>(
                blob.GetSumEntitiesXCollisions(), Allocator.Persistent);

            Pairs = new LittleHashMap<uint>(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxPairPerEntity,
                Allocator.Persistent);

            CollisionsNew = new LittleHashMap<CollisionData>(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxCollisionsPerEntity,
                Allocator.Persistent);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public LittleHashMap<uint> PairMap;
            public LittleHashMap<CollisionData> CollisionsMap;

            public void Execute()
            {
                CollisionsMap.Clear();
                PairMap.Clear();
            }
        }

        [BurstCompile]
        private struct PairsCheckJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeCollisionMap DynamicCollisionMap;
            [NativeDisableParallelForRestriction] public NativeCollisionMap StaticCollisionMap;
            [NativeDisableParallelForRestriction] public NativeCollisionMap TriggersCollisionMap;
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;

            [NativeDisableContainerSafetyRestriction] public LittleHashMap<uint> PairMap;
            [NativeDisableContainerSafetyRestriction] public LittleHashMap<CollisionData> CollisionsMap;

            public unsafe void Execute(int cellIndex)
            {
                CheckDynamicVsDynamic((uint)cellIndex);
                CheckDynamicVsStatic((uint)cellIndex);
                CheckTriggerVsDynamic((uint)cellIndex);
                CheckTriggerVsStatic((uint)cellIndex);
            }

            private unsafe void CheckDynamicVsDynamic(uint cellKey)
            {
                var outerIt = DynamicCollisionMap.GetCellIterator(cellKey);
                while (DynamicCollisionMap.TraverseCell(ref outerIt, out uint bodyIndexA))
                {
                    var innerIt = outerIt;
                    while (DynamicCollisionMap.TraverseCell(ref innerIt, out uint bodyIndexB))
                    {
                        CheckCollision(bodyIndexA, bodyIndexB);
                    }
                }
            }

            private unsafe void CheckDynamicVsStatic(uint cellKey)
            {
                var outerIt = DynamicCollisionMap.GetCellIterator(cellKey);
                while (DynamicCollisionMap.TraverseCell(ref outerIt, out uint bodyIndexA))
                {
                    var innerIt = StaticCollisionMap.GetCellIterator(cellKey);
                    while (StaticCollisionMap.TraverseCell(ref innerIt, out uint bodyIndexB))
                    {
                        CheckCollision(bodyIndexA, bodyIndexB);
                    }
                }
            }

            private unsafe void CheckTriggerVsDynamic(uint cellKey)
            {
                var outerIt = TriggersCollisionMap.GetCellIterator(cellKey);
                while (TriggersCollisionMap.TraverseCell(ref outerIt, out uint bodyIndexA))
                {
                    var innerIt = DynamicCollisionMap.GetCellIterator(cellKey);
                    while (DynamicCollisionMap.TraverseCell(ref innerIt, out uint bodyIndexB))
                    {
                        CheckCollision(bodyIndexA, bodyIndexB);
                    }
                }
            }

            private unsafe void CheckTriggerVsStatic(uint cellKey)
            {
                var outerIt = TriggersCollisionMap.GetCellIterator(cellKey);
                while (TriggersCollisionMap.TraverseCell(ref outerIt, out uint bodyIndexA))
                {
                    var innerIt = StaticCollisionMap.GetCellIterator(cellKey);
                    while (StaticCollisionMap.TraverseCell(ref innerIt, out uint bodyIndexB))
                    {
                        CheckCollision(bodyIndexA, bodyIndexB);
                    }
                }
            }

            private unsafe void CheckCollision(uint bodyIndexA, uint bodyIndexB)
            {
                uint lo = math.min(bodyIndexA, bodyIndexB);
                uint hi = math.max(bodyIndexA, bodyIndexB);

                if (PairMap.TryAdd(lo, hi) == false)
                {
                    return;
                }

                var bodyA = BodiesList[(int)bodyIndexA];
                var bodyB = BodiesList[(int)bodyIndexB];

                if (CollisionsMap.CanAdd((uint)bodyIndexA) == false
                    && CollisionsMap.CanAdd((uint)bodyIndexB) == false)
                {
                    return;
                }

                if (CollisionMethods.AreBodiesColliding(bodyA, bodyB, out var contactPoint) == false)
                {
                    return;
                }

                var collision = new CollisionData(bodyIndexA, bodyIndexB, contactPoint);

                CollisionsMap.TryAdd((uint)bodyIndexA, collision);

                CollisionsMap.TryAdd((uint)bodyIndexB, collision);
            }
        }
    }
}
