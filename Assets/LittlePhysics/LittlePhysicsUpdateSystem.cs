using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct LittlePhysicsUpdateSystem : ISystem
    {
        [NoAlias] public NativeList<PhysicsBodyData> Bodies;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (Bodies.IsCreated) Bodies.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Bodies.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var capacity = settings.BlobRef.Value.MaxEntitiesCount;
                Bodies = new NativeList<PhysicsBodyData>(capacity, Allocator.Persistent);
                Bodies.Resize(capacity, NativeArrayOptions.ClearMemory);
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            state.Dependency = new MoveRightJob
            {
                Bodies = Bodies,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct MoveRightJob : IJob
        {
            public NativeList<PhysicsBodyData> Bodies;
            public float DeltaTime;

            public void Execute()
            {
                for (int i = 0; i < Bodies.Length; i++)
                {
                    var body = Bodies[i];
                    if (body.BodyType != BodyType.Dynamic)
                        continue;
                    body.Position.x += 0.5f * DeltaTime;
                    Bodies[i] = body;
                }
            }
        }
    }
}
