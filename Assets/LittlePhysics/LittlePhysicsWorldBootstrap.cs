using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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

        public void OnUpdate(ref SystemState state)
        {
            if (!WaitForImportSystem(ref state))
                return;

            if (!WaitForCollisionMapSystem(ref state))
                return;

            if (!WaitForCollisionSystem(ref state))
                return;

            CreateSingleton(ref state);

            state.Enabled = false;
        }

        private static bool WaitForImportSystem(ref SystemState state)
        {
            var handle = state.World.GetExistingSystem<ImportPhysicsDataSystem>();
            if (handle == SystemHandle.Null)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForImportSystem: ImportPhysicsDataSystem not registered yet; deferring.");
                return false;
            }

            ref var importSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<ImportPhysicsDataSystem>(handle);

            if (!importSystem.BodiesList.IsCreated || !importSystem.PhysicsVelocities.IsCreated)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForImportSystem: BodiesList / PhysicsVelocities not ready; deferring.");
                return false;
            }

            Debug.Log("[LittlePhysicsBootstrap] WaitForImportSystem: ImportPhysicsDataSystem buffers ready.");
            return true;
        }

        private static bool WaitForCollisionMapSystem(ref SystemState state)
        {
            var handle = state.World.GetExistingSystem<CollisionMapUpdateSystem>();
            if (handle == SystemHandle.Null)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionMapSystem: CollisionMapUpdateSystem not registered yet; deferring.");
                return false;
            }

            ref var mapSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionMapUpdateSystem>(handle);

            if (!mapSystem.DynamicCollisionMap.IsCreated)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionMapSystem: DynamicCollisionMap not created yet; deferring.");
                return false;
            }

            Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionMapSystem: Collision map buffers ready.");
            return true;
        }

        private static bool WaitForCollisionSystem(ref SystemState state)
        {
            var handle = state.World.GetExistingSystem<CollisionDetectionSystem>();
            if (handle == SystemHandle.Null)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionSystem: CollisionDetectionSystem not registered yet; deferring.");
                return false;
            }

            ref var detectionSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(handle);
            if (!detectionSystem.Collisions.IsCreated)
            {
                Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionSystem: Collisions buffer not created yet; deferring.");
                return false;
            }

            Debug.Log("[LittlePhysicsBootstrap] WaitForCollisionSystem: Collision detection buffers ready.");
            return true;
        }

        private void CreateSingleton(ref SystemState state)
        {
            Debug.Log("[LittlePhysicsBootstrap] CreateSingleton: Building PhysicsSingleton and playing back ECB.");

            var importHandle = state.World.GetExistingSystem<ImportPhysicsDataSystem>();
            ref var importSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<ImportPhysicsDataSystem>(importHandle);

            var collisionMapHandle = state.World.GetExistingSystem<CollisionMapUpdateSystem>();
            ref var collisionMapSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionMapUpdateSystem>(collisionMapHandle);

            var detectionHandle = state.World.GetExistingSystem<CollisionDetectionSystem>();
            ref var detectionSystem = ref state.World.Unmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(detectionHandle);

            var spacialMap = SystemAPI.GetSingleton<SpacialMapSettingsComponent>().SpacialMap;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var singletonEntity = ecb.CreateEntity();
            ecb.AddComponent(singletonEntity, new PhysicsSingleton
            {
                BodiesList = importSystem.BodiesList,
                PhysicsVelocities = importSystem.PhysicsVelocities,
                BodiesCount = importSystem.BodiesCount,
                CollisionMap = new CollisionMapSingleton
                {
                    DynamicCollisionMap = collisionMapSystem.DynamicCollisionMap,
                    TriggersCollisionMap = collisionMapSystem.TriggersCollisionMap,
                    StaticCollisionMap = collisionMapSystem.StaticCollisionMap
                },
                Collisions = new CollisionsSingleton
                {
                    Collisions = detectionSystem.Collisions
                },
                SpacialMap = spacialMap
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            Debug.Log("[LittlePhysicsBootstrap] CreateSingleton: PhysicsSingleton created; bootstrap system will disable.");
        }
    }
}
