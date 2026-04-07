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

        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsSingleton>(out var singleton))
                return;
            if (singleton.PhysicsVelocities.IsCreated)
                singleton.PhysicsVelocities.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var systemHandle = state.World.GetExistingSystem<LittlePhysicsUpdateSystem>();
            if (systemHandle == SystemHandle.Null)
                return;

            ref var littleSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<LittlePhysicsUpdateSystem>(systemHandle);

            if (!littleSystem.Bodies.IsCreated || !littleSystem.BodiesEntities.IsCreated || !littleSystem.BodiesList.IsCreated)
                return;

            var collisionsHandle = state.World.GetExistingSystem<CollisionMapUpdateSystem>();
            if (collisionsHandle == SystemHandle.Null)
                return;

            ref var collisionsSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionMapUpdateSystem>(collisionsHandle);

            if (!collisionsSystem.DynamicCollisionMap.IsCreated)
                return;

            var detectionHandle = state.World.GetExistingSystem<CollisionDetectionSystem>();
            if (detectionHandle == SystemHandle.Null)
                return;

            ref var detectionSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(detectionHandle);

            if (!detectionSystem.Collisions.IsCreated)
                return;

            var spacialMap = SystemAPI.GetSingleton<SpacialMapSettingsComponent>().SpacialMap;
            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            int velocityCapacity = physicsSettings.BlobRef.Value.LodData.MaxEntityCount;
            var physicsVelocities = new NativeArray<PhysicsVelocityData>(velocityCapacity, Allocator.Persistent);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var singletonEntity = ecb.CreateEntity();
            ecb.AddComponent(singletonEntity, new PhysicsSingleton
            {
                BodiesEntities = littleSystem.BodiesEntities,
                Bodies = littleSystem.Bodies,
                BodiesList = littleSystem.BodiesList,
                PhysicsVelocities = physicsVelocities,
                CollisionMap = new CollisionMapSingleton
                {
                    DynamicCollisionMap = collisionsSystem.DynamicCollisionMap,
                    TriggersCollisionMap = collisionsSystem.TriggersCollisionMap,
                    StaticCollisionMap = collisionsSystem.StaticCollisionMap
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
