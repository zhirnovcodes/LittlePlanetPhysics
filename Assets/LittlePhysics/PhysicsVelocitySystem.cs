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
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
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

            int bodyCount = singleton.PhysicsVelocities.Length;

            var collisionDep = new ApplyCollisionVelocitiesJob
            {
                CollisionsMap = singleton.Collisions.Collisions,
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
            }.Schedule(bodyCount, 32, combinedDep);

            state.Dependency = new ApplyVelocitiesJob
            {
                Bodies = singleton.Bodies,
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(collisionDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct ApplyCollisionVelocitiesJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction] public LittleHashMap<CollisionData> CollisionsMap;
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;

            public void Execute(int index)
            {
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
        private struct ApplyVelocitiesJob : IJob
        {
            public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            public NativeList<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public float DeltaTime;

            public void Execute()
            {
                for (int i = 0; i < BodiesList.Length; i++)
                {
                    var body = BodiesList[i];
                    if (body.BodyType != BodyType.Dynamic)
                        continue;

                    var velocity = PhysicsVelocities[i];
                    body.Position += velocity.Linear * DeltaTime;
                    body.RotationOffset += velocity.Angular * DeltaTime;

                    BodiesList[i] = body;
                    if (Bodies.ContainsKey(body.Main))
                        Bodies[body.Main] = body;
                }
            }
        }
    }
}
