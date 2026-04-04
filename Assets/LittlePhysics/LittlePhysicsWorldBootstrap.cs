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
            state.RequireForUpdate<SpacialMapSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var systemHandle = state.World.GetExistingSystem<LittlePhysicsUpdateSystem>();
            if (systemHandle == SystemHandle.Null)
                return;

            ref var littleSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<LittlePhysicsUpdateSystem>(systemHandle);

            if (!littleSystem.Bodies.IsCreated || !littleSystem.BodiesEntities.IsCreated)
                return;

            var collisionsHandle = state.World.GetExistingSystem<CollisionMapUpdateSystem>();
            if (collisionsHandle == SystemHandle.Null)
                return;

            ref var collisionsSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionMapUpdateSystem>(collisionsHandle);

            if (!collisionsSystem.DynamicMap.IsCreated)
                return;

            var detectionHandle = state.World.GetExistingSystem<CollisionDetectionSystem>();
            if (detectionHandle == SystemHandle.Null)
                return;

            ref var detectionSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(detectionHandle);

            if (!detectionSystem.Collisions.IsCreated)
                return;

            var spacialMap = SystemAPI.GetSingleton<SpacialMapSettingsComponent>().SpacialMap;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var singletonEntity = ecb.CreateEntity();
            ecb.AddComponent(singletonEntity, new PhysicsSingleton
            {
                BodiesEntities = littleSystem.BodiesEntities,
                Bodies = littleSystem.Bodies,
                CollisionMap = new CollisionMapSingleton
                {
                    DynamicMap = collisionsSystem.DynamicMap,
                    TriggersMap = collisionsSystem.TriggersMap,
                    StaticMap = collisionsSystem.StaticMap
                },
                Collisions = new CollisionsSingleton
                {
                    Collisions = detectionSystem.Collisions
                },
                SpacialMap = spacialMap
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}
