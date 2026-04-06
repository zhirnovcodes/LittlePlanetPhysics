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
    public partial struct CollisionVelocityCalculateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            var detectionHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<CollisionDetectionSystem>();
            ref var detectionSystem = ref state.WorldUnmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(detectionHandle);

            if (!singleton.BodiesList.IsCreated || !detectionSystem.CollisionsNew.IsCreated)
                return;

            if (singleton.BodiesList.Length == 0)
                return;

            state.Dependency = new CalculateCollisionVelocitiesJob
            {
                BodiesList = singleton.BodiesList,
                CollisionsNew = detectionSystem.CollisionsNew,
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(false)
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct CalculateCollisionVelocitiesJob : IJob
        {
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;

            [NativeDisableContainerSafetyRestriction]
            public LittleHashMap<CollisionData> CollisionsNew;

            public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;

            public void Execute()
            {
                var iterator = CollisionsNew.GetIterator();
                while (CollisionsNew.Traverse(ref iterator, out var entry))
                {
                    var row = entry.Item1;
                    var collision = entry.Item2;
                    uint minBody = collision.Body1 < collision.Body2 ? collision.Body1 : collision.Body2;
                    if (row != minBody)
                        continue;

                    int ia = (int)collision.Body1;
                    int ib = (int)collision.Body2;

                    var bodyA = BodiesList[ia];
                    var bodyB = BodiesList[ib];
                    float3 contactPoint = collision.ContactPoint;

                    VelocityLookup.TryGetComponent(bodyA.Main, out var velocityA);
                    VelocityLookup.TryGetComponent(bodyB.Main, out var velocityB);

                    var vel1 = velocityA.ToVelocityData();
                    var vel2 = velocityB.ToVelocityData();

                    CollisionForces.GetCollisionImpulses(
                        bodyA, bodyB, vel1, vel2, contactPoint,
                        out float3 impulse1, out float3 impulse2);

                    CollisionForces.GetPushOutForce(
                        bodyA, bodyB, contactPoint,
                        out float3 pushForce1, out float3 pushForce2);

                    float3 total1 = impulse1 + pushForce1;
                    float3 total2 = impulse2 + pushForce2;

                    if (bodyA.BodyType == BodyType.Dynamic)
                    {
                        CollisionForces.ImpulseToVelocity(
                            bodyA, total1, contactPoint,
                            out float3 dLinearA, out float3 dAngularA);
                        velocityA.Linear += dLinearA;
                        velocityA.Angular += dAngularA;
                        VelocityLookup[bodyA.Main] = velocityA;
                    }

                    if (bodyB.BodyType == BodyType.Dynamic)
                    {
                        CollisionForces.ImpulseToVelocity(
                            bodyB, total2, contactPoint,
                            out float3 dLinearB, out float3 dAngularB);
                        velocityB.Linear += dLinearB;
                        velocityB.Angular += dAngularB;
                        VelocityLookup[bodyB.Main] = velocityB;
                    }
                }
            }
        }
    }
}
