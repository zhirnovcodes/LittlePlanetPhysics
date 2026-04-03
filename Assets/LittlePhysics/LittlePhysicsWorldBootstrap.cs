using Unity.Collections;
using Unity.Entities;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct LittlePhysicsBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var systemHandle = state.World.GetExistingSystem<LittlePhysicsUpdateSystem>();
            if (systemHandle == SystemHandle.Null)
                return;

            ref var littleSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<LittlePhysicsUpdateSystem>(systemHandle);

            if (!littleSystem.Bodies.IsCreated)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var singletonEntity = ecb.CreateEntity();
            ecb.AddComponent(singletonEntity, new PhysicsSingleton
            {
                Bodies = littleSystem.Bodies
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}
