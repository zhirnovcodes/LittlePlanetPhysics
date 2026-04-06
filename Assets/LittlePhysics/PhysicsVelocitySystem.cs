using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CollisionMapUpdateSystem))]
    public partial struct PhysicsVelocitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);
            state.Dependency = new ApplyVelocitiesJob
            {
                BodiesEntities = singleton.BodiesEntities,
                Bodies = singleton.Bodies,
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct ApplyVelocitiesJob : IJob
        {
            [ReadOnly] public NativeList<Entity> BodiesEntities;
            public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            [ReadOnly] public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;
            public float DeltaTime;

            public void Execute()
            {
                for (int i = 0; i < BodiesEntities.Length; i++)
                {
                    var entity = BodiesEntities[i];

                    if (!Bodies.TryGetValue(entity, out var body))
                        continue;

                    if (body.BodyType != BodyType.Dynamic)
                        continue;

                    if (!VelocityLookup.TryGetComponent(entity, out var velocity))
                        continue;

                    body.Position += velocity.Linear * DeltaTime;
                    body.RotationOffset += velocity.Angular * DeltaTime;
                    Bodies[entity] = body;
                }
            }
        }
    }
}
