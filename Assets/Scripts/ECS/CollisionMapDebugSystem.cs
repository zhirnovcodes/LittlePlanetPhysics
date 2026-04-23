using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    public struct CollisionMapDebugComponent : IComponentData
    {
        public float UpdateTimeSec;
        public Entity CellPrefab;
        public BodyType BodyToDebug;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsDataSystem))]
    public partial struct CollisionMapDebugSystem : ISystem
    {
        private const int MaxEntitiesPerCell = 3;
        private const int MaxDebugObjects = 1000;

        private NativeList<Entity> spawnedEntities;
        private float elapsedTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<CollisionMapDebugComponent>();
            spawnedEntities = new NativeList<Entity>(MaxDebugObjects, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (spawnedEntities.IsCreated)
                spawnedEntities.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<CollisionMapDebugComponent>();
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

            var spacialMap = physics.SpacialMap;
            int totalCells = spacialMap.GetCellsCount();
            var collisionMap = physics.CollisionMap;

            var cellScale = spacialMap.Grid.CellSize;

            for (int cellIndex = 0; cellIndex < totalCells && spawnedEntities.Length < MaxDebugObjects; cellIndex++)
            {
                int entityCount = getEntityCountInCell(config.BodyToDebug, (uint)cellIndex, ref collisionMap);
                if (entityCount <= 0)
                    continue;

                var cellPos = spacialMap.GetCellPosition(cellIndex);
                int toSpawn = math.min(math.min(entityCount, MaxEntitiesPerCell), MaxDebugObjects - spawnedEntities.Length);

                for (int j = 0; j < toSpawn; j++)
                {
                    var spawned = state.EntityManager.Instantiate(config.CellPrefab);
                    var transform = LocalTransform.FromPositionRotationScale(cellPos, quaternion.identity, cellScale);
                    state.EntityManager.SetComponentData(spawned, transform);
                    spawnedEntities.Add(spawned);
                }
            }
        }

        private static int getEntityCountInCell(BodyType bodyType, uint key, ref CollisionMapSingleton map)
        {
            return bodyType switch
            {
                BodyType.Dynamic => map.DynamicCollisionMap.GetCellCount(key),
                BodyType.Trigger => map.TriggersCollisionMap.GetCellCount(key),
                BodyType.Static => map.StaticCollisionMap.GetCellCount(key),
                _ => 0
            };
        }
    }
}
