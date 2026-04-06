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

        [NoAlias] public CollisionPairHashMap Pairs;
        [NoAlias] public CollisionPairHashMap CollisionsNew;

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
            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
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
                BodiesList = physicsSingleton.BodiesList,
                PairMap = Pairs,
                CollisionsMap = CollisionsNew,
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

            Pairs = new CollisionPairHashMap(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxPairPerEntity,
                Allocator.Persistent);

            CollisionsNew = new CollisionPairHashMap(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxCollisionsPerEntity,
                Allocator.Persistent);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public CollisionPairHashMap PairMap;
            public CollisionPairHashMap CollisionsMap;

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
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;

            [NativeDisableContainerSafetyRestriction] public CollisionPairHashMap PairMap;
            [NativeDisableContainerSafetyRestriction] public CollisionPairHashMap CollisionsMap;
            public int MaxCollisions;

            public unsafe void Execute(int cellIndex)
            {
                CheckDynamicVsDynamic((uint)cellIndex);
                //CheckDynamicVsStatic((uint)cellIndex);
                //CheckTriggerVsDynamic((uint)cellIndex);
                //CheckTriggerVsStatic((uint)cellIndex);
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

            private unsafe void CheckCollision(uint bodyIndexA, uint bodyIndexB)
            {
                if (PairMap.TryAdd(bodyIndexA, bodyIndexB) == false)
                { 
                    return; 
                }

                var bodyA = BodiesList[(int)bodyIndexA];
                var bodyB = BodiesList[(int)bodyIndexB];

                if (CollisionsMap.CanAdd((uint)bodyIndexA) == false)
                {
                    return;
                }

                if (CollisionMethods.AreBodiesColliding(bodyA, bodyB, out var contactPoint) == false)
                { 
                    return; 
                }

                if (CollisionsMap.TryAdd((uint)bodyIndexA, (uint)bodyIndexB) == false)
                {
                    return;
                }
            }
        }
    }
}
