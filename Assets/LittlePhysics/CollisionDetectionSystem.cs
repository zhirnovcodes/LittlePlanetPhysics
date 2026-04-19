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
        public float3 Impulse1;
        public float3 Impulse2;
        public float3 PushOutForce1;
        public float3 PushOutForce2;

        public CollisionData(
            uint body1,
            uint body2,
            float3 contactPoint,
            float3 impulse1,
            float3 impulse2,
            float3 pushOutForce1,
            float3 pushOutForce2)
        {
            Body1 = body1;
            Body2 = body2;
            ContactPoint = contactPoint;
            Impulse1 = impulse1;
            Impulse2 = impulse2;
            PushOutForce1 = pushOutForce1;
            PushOutForce2 = pushOutForce2;
        }

        public bool Equals(CollisionData other)
        {
            return (Body1 == other.Body1 && Body2 == other.Body2)
                || (Body1 == other.Body2 && Body2 == other.Body1);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(LittlePhysicsInternalSystemGroup))]
    [UpdateAfter(typeof(CollisionMapUpdateSystem))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        [NoAlias] public LittleHashMap<uint> Pairs;
        [NoAlias] public LittleHashMap<CollisionData> Collisions;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (Collisions.IsCreated) Collisions.Dispose();
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

            if (!SystemAPI.TryGetSingleton<LittlePhysicsTimeComponent>(out var time))
                return;

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!physicsSingleton.CollisionMap.DynamicCollisionMap.IsCreated)
                return;
            if (!physicsSingleton.PhysicsVelocities.IsCreated)
                return;
            var spacialMapSettings = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                CollisionsMap = Collisions,
                PairMap = Pairs,
            }.Schedule(physicsHandle);

            var afterSurfaceJob = clearJob;
            if (SystemAPI.HasSingleton<CollisionSurfaceComponent>())
            {
                afterSurfaceJob = new CheckDynamicVsSurfaceJob
                {
                    SurfaceBody = SystemAPI.GetSingleton<CollisionSurfaceComponent>().ToBodyData(),
                    BodiesList = physicsSingleton.BodiesList,
                    BodiesCount = physicsSingleton.BodiesCount,
                    PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                    PhysicsSettings = physicsSingleton.Settings,
                }.Schedule(physicsSingleton.BodiesList.Length, 32, clearJob);
            }

            int totalCells = spacialMapSettings.SpacialMap.GetCellsCount();

            var pairsCheckJob = new PairsCheckJob
            {
                DynamicCollisionMap = physicsSingleton.CollisionMap.DynamicCollisionMap,
                StaticCollisionMap = physicsSingleton.CollisionMap.StaticCollisionMap,
                TriggersCollisionMap = physicsSingleton.CollisionMap.TriggersCollisionMap,
                BodiesList = physicsSingleton.BodiesList,
                PairMap = Pairs,
                CollisionsMap = Collisions,
                PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                PhysicsSettings = physicsSingleton.Settings,
            }.Schedule(totalCells, 16, afterSurfaceJob);

            int bodyCount = physicsSingleton.Settings.BlobRef.Value.LodData.MaxEntityCount;
            var deltaTime = time.DeltaTime;

            var collisionDep = new ApplyCollisionVelocitiesJob
            {
                CollisionsMap = Collisions,
                BodiesList = physicsSingleton.BodiesList,
                PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                BodiesCount = physicsSingleton.BodiesCount,
                DeltaTime = deltaTime
            }.Schedule(bodyCount, 32, pairsCheckJob);

            state.Dependency = collisionDep;
            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void InitCollections(ref PhysicsSettingsBlobAsset blob)
        {
            Pairs = new LittleHashMap<uint>(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxPairPerEntity,
                Allocator.Persistent);

            Collisions = new LittleHashMap<CollisionData>(
                blob.LodData.MaxEntityCount,
                blob.LodData.MaxCollisionsPerEntity,
                Allocator.Persistent);
        }

        [BurstCompile]
        private struct CheckDynamicVsSurfaceJob : IJobParallelFor
        {
            public PhysicsBodyData SurfaceBody;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public PhysicsSettingsComponent PhysicsSettings;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                {
                    return;
                }

                var body = BodiesList[index];
                if (body.BodyType != BodyType.Dynamic)
                {
                    return;
                }

                if (!PhysicsSettings.IsColliding(body.Layer, SurfaceBody.Layer))
                {
                    return;
                }

                if (!CollisionMethods.AreBodiesColliding(body, SurfaceBody, out float3 contactPoint))
                {
                    return;
                }

                var vel = PhysicsVelocities[index];

                CollisionForces.GetCollisionImpulses(body, SurfaceBody, vel, default, contactPoint,
                    out float3 impulse, out _);
                CollisionForces.GetPushOutForce(body, SurfaceBody, contactPoint,
                    out float3 pushForce, out _);
                CollisionForces.ImpulseToVelocity(body, impulse, contactPoint,
                    out float3 linearChange, out float3 angularChange);

                vel.Linear += linearChange + pushForce;
                vel.Angular += angularChange;
                PhysicsVelocities[index] = vel;
            }
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
            [ReadOnly] public NativeCollisionMap DynamicCollisionMap;
            [ReadOnly] public NativeCollisionMap StaticCollisionMap;
            [ReadOnly] public NativeCollisionMap TriggersCollisionMap;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;

            [NativeDisableContainerSafetyRestriction] public LittleHashMap<uint> PairMap;
            [NativeDisableContainerSafetyRestriction] public LittleHashMap<CollisionData> CollisionsMap;
            [ReadOnly] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [ReadOnly] public PhysicsSettingsComponent PhysicsSettings;

            public unsafe void Execute(int cellIndex)
            {
                if (PhysicsSettings.CheckSettings.CheckDynamicVsStatic)
                {
                    CheckDynamicVsStatic((uint)cellIndex);
                }

                if (PhysicsSettings.CheckSettings.CheckDynamicVsDynamic)
                {
                    CheckDynamicVsDynamic((uint)cellIndex);
                }

                if (PhysicsSettings.CheckSettings.CheckTriggerVsDynamic)
                {
                    CheckTriggerVsDynamic((uint)cellIndex);
                }

                if (PhysicsSettings.CheckSettings.CheckTriggerVsStatic)
                {
                    CheckTriggerVsStatic((uint)cellIndex);
                }
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
                        CheckTrigger(bodyIndexA, bodyIndexB);
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
                        CheckTrigger(bodyIndexA, bodyIndexB);
                    }
                }
            }

            private unsafe void CheckCollision(uint bodyIndexA, uint bodyIndexB)
            {
                uint lo = math.min(bodyIndexA, bodyIndexB);
                uint hi = math.max(bodyIndexA, bodyIndexB);

                var bodyA = BodiesList[(int)bodyIndexA];
                var bodyB = BodiesList[(int)bodyIndexB];

                if (bodyA.Main == bodyB.Main)
                {
                    return;
                }

                if (!PhysicsSettings.IsColliding(bodyA.Layer, bodyB.Layer))
                {
                    return;
                }

                if (PairMap.TryAdd(lo, hi) == false)
                {
                    return;
                }

                if (CollisionsMap.CanAdd((uint)bodyIndexA) == false)
                {
                    return;
                }

                if (CollisionMethods.AreBodiesColliding(bodyA, bodyB, out var contactPoint) == false)
                {
                    return;
                }

                var vel1 = PhysicsVelocities[(int)bodyIndexA];
                var vel2 = PhysicsVelocities[(int)bodyIndexB];

                CollisionForces.GetCollisionImpulses(
                    bodyA, bodyB, vel1, vel2, contactPoint,
                    out float3 impulse1, out float3 impulse2);

                CollisionForces.GetPushOutForce(
                    bodyA, bodyB, contactPoint,
                    out float3 pushForce1, out float3 pushForce2);

                var collision = new CollisionData(
                    bodyIndexA, bodyIndexB, contactPoint,
                    impulse1, impulse2, pushForce1, pushForce2);

                CollisionsMap.TryAdd((uint)bodyIndexA, collision);

                CollisionsMap.TryAdd((uint)bodyIndexB, collision);
            }

            private unsafe void CheckTrigger(uint triggerIndex, uint nonTriggerIndex)
            {
                var trigger = BodiesList[(int)triggerIndex];
                var nonTrigger = BodiesList[(int)nonTriggerIndex];

                if (trigger.Main == nonTrigger.Main)
                {
                    return;
                }

                if (!PhysicsSettings.IsColliding(trigger.Layer, nonTrigger.Layer))
                {
                    return;
                }

                var lo = math.min(triggerIndex, nonTriggerIndex);
                var hi = math.max(triggerIndex, nonTriggerIndex);

                if (PairMap.TryAdd(lo, hi) == false)
                {
                    return;
                }

                if (CollisionsMap.CanAdd((uint)triggerIndex) == false)
                {
                    return;
                }

                if (CollisionMethods.AreBodiesColliding(trigger, nonTrigger, out var contactPoint) == false)
                {
                    return;
                }

                var collision = new CollisionData(
                    triggerIndex, nonTriggerIndex, contactPoint,
                    float3.zero, float3.zero, float3.zero, float3.zero);

                CollisionsMap.TryAdd((uint)triggerIndex, collision);

                CollisionsMap.TryAdd((uint)nonTriggerIndex, collision);
            }
        }

        [BurstCompile]
        private struct ApplyCollisionVelocitiesJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction] public LittleHashMap<CollisionData> CollisionsMap;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            public float DeltaTime;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                    return;

                uint row = (uint)index;

                var body = BodiesList[index];
                if (body.BodyType != BodyType.Dynamic)
                    return;

                var sumVelocity = PhysicsVelocities[index];

                var iterator = CollisionsMap.GetSingleIterator(index);
                while (CollisionsMap.Traverse(ref iterator, out var pair))
                {
                    var collision = pair.Item2;

                    float3 impulse;
                    float3 pushForce;
                    if (row == collision.Body1)
                    {
                        impulse = collision.Impulse1;
                        pushForce = collision.PushOutForce1;
                    }
                    else
                    {
                        impulse = collision.Impulse2;
                        pushForce = collision.PushOutForce2;
                    }

                    CollisionForces.ImpulseToVelocity(
                        body, impulse, collision.ContactPoint,
                        out float3 linearFromImpulse, out float3 angularFromImpulse);

                    var additionVelocity = new PhysicsVelocityData
                    {
                        Linear = linearFromImpulse,
                        Angular = angularFromImpulse
                    };

                    body.Position += pushForce * DeltaTime * 10f;

                    sumVelocity += additionVelocity;
                }

                PhysicsVelocities[index] = sumVelocity;
                BodiesList[index] = body;
            }
        }
    }
}
