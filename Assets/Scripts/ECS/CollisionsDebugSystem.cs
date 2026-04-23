using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    public struct CollisionsDebugComponent : IComponentData
    {
        public float UpdateTimeSec;
        public Entity CellPrefab;
        public BodyType BodyToDebug;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsDataSystem))]
    public partial struct CollisionsDebugSystem : ISystem
    {
        private const int MaxEntities = 100;
        private const int MaxDebugObjects = 100;

        private NativeList<Entity> spawnedEntities;
        private float elapsedTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<CollisionsDebugComponent>();
            spawnedEntities = new NativeList<Entity>(MaxDebugObjects, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (spawnedEntities.IsCreated)
                spawnedEntities.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<CollisionsDebugComponent>();
            if (config.CellPrefab == Entity.Null)
                return;

            elapsedTime += SystemAPI.Time.DeltaTime;
            if (elapsedTime < config.UpdateTimeSec)
                return;

            elapsedTime = 0f;

            state.CompleteDependency();

            var physics = SystemAPI.GetSingleton<PhysicsSingleton>();
            physics.PhysicsJobHandle.Complete();

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            for (int i = 0; i < spawnedEntities.Length; i++)
                ecb.DestroyEntity(spawnedEntities[i]);
            spawnedEntities.Clear();

            var collisions = physics.Collisions.Collisions;

            int entityCount = 0;
            foreach (var (body, entity) in SystemAPI.Query<RefRO<PhysicsBodyComponent>>().WithEntityAccess())
            {
                if (entityCount >= MaxEntities)
                    break;

                entityCount++;

                if (body.ValueRO.BodyType != config.BodyToDebug)
                    continue;

                var iterator = collisions.GetIterator();

                while (collisions.Traverse(ref iterator, out var pair))
                {
                    if (spawnedEntities.Length >= MaxDebugObjects)
                        break;

                    var spawned = state.EntityManager.Instantiate(config.CellPrefab);
                    var transform = LocalTransform.FromPosition(pair.Item2.ContactPoint);
                    state.EntityManager.SetComponentData(spawned, transform);
                    spawnedEntities.Add(spawned);
                }
                
            }
        }
    }
}
