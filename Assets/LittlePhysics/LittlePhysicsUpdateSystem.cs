using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionMapUpdateSystem))]
    public partial struct LittlePhysicsUpdateSystem : ISystem
    {
        [NoAlias] public NativeList<Entity> BodiesEntities;
        [NoAlias] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
        [NoAlias] public NativeList<PhysicsBodyData> BodiesList;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (BodiesEntities.IsCreated) BodiesEntities.Dispose();
            if (Bodies.IsCreated) Bodies.Dispose();
            if (BodiesList.IsCreated) BodiesList.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Bodies.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var capacity = settings.BlobRef.Value.MaxEntitiesCount;
                BodiesEntities = new NativeList<Entity>(capacity, Allocator.Persistent);
                Bodies = new NativeParallelHashMap<Entity, PhysicsBodyData>(capacity, Allocator.Persistent);
                BodiesList = new NativeList<PhysicsBodyData>(capacity, Allocator.Persistent);
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            state.Dependency = new MoveRightJob
            {
                BodiesEntities = BodiesEntities,
                Bodies = Bodies,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct MoveRightJob : IJob
        {
            [ReadOnly] public NativeList<Entity> BodiesEntities;
            public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            public float DeltaTime;

            public void Execute()
            {
                return;
                for (int i = 0; i < BodiesEntities.Length; i++)
                {
                    var entity = BodiesEntities[i];
                    if (!Bodies.TryGetValue(entity, out var body))
                        continue;

                    if (body.BodyType != BodyType.Dynamic)
                        continue;

                    body.Position.x += 0.5f * DeltaTime;
                    Bodies[entity] = body;
                }
            }
        }
    }
}
