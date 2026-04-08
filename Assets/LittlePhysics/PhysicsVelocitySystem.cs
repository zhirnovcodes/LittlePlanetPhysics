using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct PhysicsVelocitySystem : ISystem
    {
        private NativeReference<uint> BodiesCount;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            BodiesCount = new NativeReference<uint>(Allocator.Persistent);
        }

        public void OnDisable(ref SystemState state)
        {
            if (BodiesCount.IsCreated)
            { 
                BodiesCount.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!singleton.BodiesList.IsCreated || !singleton.PhysicsVelocities.IsCreated)
                return;
            if (!singleton.Collisions.Collisions.IsCreated)
                return;

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);
            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            int bodyCount = settings.BlobRef.Value.LodData.MaxEntityCount;

            var collisionDep = new ApplyCollisionVelocitiesJob
            {
                CollisionsMap = singleton.Collisions.Collisions,
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
                BodiesCount = BodiesCount
            }.Schedule(bodyCount, 32, combinedDep);

            state.Dependency = new ApplyVelocitiesJob
            {
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
                DeltaTime = SystemAPI.Time.DeltaTime,
                BodiesCount = BodiesCount
            }.Schedule(bodyCount, 32, collisionDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct ApplyCollisionVelocitiesJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction] public LittleHashMap<CollisionData> CollisionsMap;
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [NativeDisableContainerSafetyRestriction] public NativeReference<uint> BodiesCount;

            public void Execute(int index)
            {
                if (index == 0)
                {
                    BodiesCount.Value = (uint)BodiesList.Length;
                }

                uint row = (uint)index;

                if (index >= BodiesList.Length)
                    return;

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
                        Linear = linearFromImpulse + pushForce,
                        Angular = angularFromImpulse
                    };

                    sumVelocity += additionVelocity;
                }

                PhysicsVelocities[index] = sumVelocity;
            }
        }

        [BurstCompile]
        private struct ApplyVelocitiesJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction] public NativeList<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public float DeltaTime;
            [ReadOnly] public NativeReference<uint> BodiesCount;

            public void Execute(int index)
            {
                if (index >= BodiesCount.Value)
                {
                    return;
                }

                var body = BodiesList[index];

                if (body.BodyType != BodyType.Dynamic)
                    return;

                var velocity = PhysicsVelocities[index];
                body.Position += velocity.Linear * DeltaTime;
                body.RotationOffset += velocity.Angular * DeltaTime;

                BodiesList[index] = body;
            }
        }
    }
}
