using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CollisionsUpdateSystem : ISystem
    {
        [NoAlias] public NativeParallelHashMap<int, uint> EntitiesInCellsCount;

        [NoAlias] public NativeParallelMultiHashMap<Entity, Entity> Collisions;
        [NoAlias] public NativeParallelHashMap<Entity, uint> CollisionsCount;

        [NoAlias] public NativeParallelHashSet<BodiesPair> Intersections;
        [NoAlias] public NativeParallelHashMap<Entity, uint> IntersectionsCount;

        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
        [NoAlias] public NativeParallelHashMap<int, Entity> StaticMap;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (EntitiesInCellsCount.IsCreated) EntitiesInCellsCount.Dispose();
            if (Collisions.IsCreated) Collisions.Dispose();
            if (CollisionsCount.IsCreated) CollisionsCount.Dispose();
            if (Intersections.IsCreated) Intersections.Dispose();
            if (IntersectionsCount.IsCreated) IntersectionsCount.Dispose();
            if (DynamicMap.IsCreated) DynamicMap.Dispose();
            if (TriggersMap.IsCreated) TriggersMap.Dispose();
            if (StaticMap.IsCreated) StaticMap.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (EntitiesInCellsCount.IsCreated)
                return;

            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            var spacialMapSingleton = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();
            initCollections(ref settings.BlobRef.Value, spacialMapSingleton.SpacialMap.GridSize);
        }

        private void initCollections(ref PhysicsSettingsBlobAsset blob, Unity.Mathematics.int3 gridSize)
        {
            int totalCells = gridSize.x * gridSize.y * gridSize.z;

            EntitiesInCellsCount = new NativeParallelHashMap<int, uint>(
                totalCells, Allocator.Persistent);

            Collisions = new NativeParallelMultiHashMap<Entity, Entity>(
                blob.GetSumEntitiesXCollisions(), Allocator.Persistent);

            CollisionsCount = new NativeParallelHashMap<Entity, uint>(
                blob.MaxEntitiesCount, Allocator.Persistent);

            Intersections = new NativeParallelHashSet<BodiesPair>(
                blob.GetSumEntitiesXIntersections(), Allocator.Persistent);

            IntersectionsCount = new NativeParallelHashMap<Entity, uint>(
                blob.MaxEntitiesCount, Allocator.Persistent);

            DynamicMap = new NativeParallelMultiHashMap<uint, Entity>(
                totalCells * blob.GetMaxEntitiesInCell(), Allocator.Persistent);

            TriggersMap = new NativeParallelMultiHashMap<uint, Entity>(
                totalCells * blob.GetMaxEntitiesInCell(), Allocator.Persistent);

            StaticMap = new NativeParallelHashMap<int, Entity>(
                totalCells, Allocator.Persistent);
        }
    }
}
