using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(LittlePhysicsBootstrapSystem))]
    public partial struct RegisterPhysicsBodySystem : ISystem
    {
        private NativeReference<int> slotCounter;
        private EntityQuery pendingQuery;

        public void OnCreate(ref SystemState state)
        {
            slotCounter = new NativeReference<int>(0, Allocator.Persistent);

            pendingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsBodyComponent, LocalTransform>()
                .WithNone<PhysicsBodyIndexComponent>()
                .Build(ref state);

            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate(pendingQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (slotCounter.IsCreated)
                slotCounter.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            var entities = pendingQuery.ToEntityArray(Allocator.Temp);
            var transforms = pendingQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var bodies = pendingQuery.ToComponentDataArray<PhysicsBodyComponent>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int slot = slotCounter.Value;
                if (slot >= singleton.Bodies.Length)
                    break;

                slotCounter.Value++;

                singleton.Bodies[slot] = bodies[i].ToBodyData(entities[i], transforms[i]);
                ecb.AddComponent(entities[i], new PhysicsBodyIndexComponent { Value = slot });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            entities.Dispose();
            transforms.Dispose();
            bodies.Dispose();
        }
    }
}
