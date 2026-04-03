using Unity.Collections;
using Unity.Entities;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct LittlePhysicsBootstrapSystem : ISystem
    {
        private const string PhysicsWorldName = "LittlePhysicsWorld";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            World physicsWorld = null;
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.Name == PhysicsWorldName)
                {
                    physicsWorld = world;
                    break;
                }
            }

            if (physicsWorld == null)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

                physicsWorld = new World(PhysicsWorldName);
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(physicsWorld, typeof(LittlePhysicsUpdateSystem));
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(physicsWorld);

                var settingsEntity = physicsWorld.EntityManager.CreateEntity();
                physicsWorld.EntityManager.AddComponentData(settingsEntity, settings.Clone());
                return;
            }

            var systemHandle = physicsWorld.GetExistingSystem<LittlePhysicsUpdateSystem>();
            if (systemHandle == SystemHandle.Null)
                return;

            ref var littleSystem = ref physicsWorld.Unmanaged.GetUnsafeSystemRef<LittlePhysicsUpdateSystem>(systemHandle);

            if (!littleSystem.DynamicData.IsCreated)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var singletonEntity = ecb.CreateEntity();
            ecb.AddComponent(singletonEntity, new PhysicsSingleton
            {
                DynamicData = littleSystem.DynamicData,
                StaticData = littleSystem.StaticData,
                TriggerData = littleSystem.TriggerData
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}
