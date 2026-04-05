using Unity.Burst;
using Unity.Collections;
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

            return;
            state.Dependency = new CalculateCollisionVelocitiesJob
            {
                BodiesEntities = singleton.BodiesEntities,
                Bodies = singleton.Bodies,
                Collisions = singleton.Collisions.Collisions,
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(false)
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct CalculateCollisionVelocitiesJob : IJob
        {
            [ReadOnly] public NativeList<Entity> BodiesEntities;
            [ReadOnly] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            [ReadOnly] public NativeParallelMultiHashMap<Entity, CollisionItem> Collisions;
            public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;

            public void Execute()
            {
                /*
                for (int i = 0; i < BodiesEntities.Length; i++)
                {
                    var entity = BodiesEntities[i];

                    if (!Bodies.TryGetValue(entity, out var body))
                        continue;

                    if (body.BodyType != BodyType.Dynamic)
                        continue;

                    if (!Collisions.TryGetFirstValue(entity, out var collision, out var iterator))
                        continue;

                    if (!VelocityLookup.TryGetComponent(entity, out var velocity))
                        continue;

                    float3 linearDelta = float3.zero;
                    float3 angularDelta = float3.zero;

                    do
                    {
                        if (!Bodies.TryGetValue(collision.Target, out var targetBody))
                            continue;

                        var vel1 = new PhysicsVelocityData
                        {
                            LinearVelocity = velocity.LinearVelocity,
                            AngularVelocity = velocity.AngularVelocity
                        };

                        var vel2 = new PhysicsVelocityData();
                        if (VelocityLookup.TryGetComponent(collision.Target, out var targetVelocity))
                        {
                            vel2.LinearVelocity = targetVelocity.LinearVelocity;
                            vel2.AngularVelocity = targetVelocity.AngularVelocity;
                        }

                        CollisionForces.GetCollisionImpulses(
                            body, targetBody, vel1, vel2, collision.ContactPoint,
                            out var impulse, out _);

                        CollisionForces.GetPushOutForce(
                            body, targetBody, collision.ContactPoint,
                            out var pushForce, out _);

                        CollisionForces.ImpulseToVelocity(body, impulse, collision.ContactPoint,
                            out var impulseLinear, out var impulseAngular);

                        CollisionForces.ImpulseToVelocity(body, pushForce, collision.ContactPoint,
                            out var pushLinear, out var pushAngular);

                        linearDelta += impulseLinear + pushLinear;
                        angularDelta += impulseAngular + pushAngular;
                    }
                    while (Collisions.TryGetNextValue(out collision, ref iterator));

                    velocity.LinearVelocity += linearDelta;
                    velocity.AngularVelocity += angularDelta;
                    VelocityLookup[entity] = velocity;
                }*/
            }
        }
    }
}
