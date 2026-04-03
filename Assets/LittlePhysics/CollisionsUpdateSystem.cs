using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CollisionsUpdateSystem : ISystem
    {
        public JobHandle Handle;

        [NoAlias] public NativeParallelHashMap<int, uint> EntitiesInCellsCount;

        [NoAlias] public NativeParallelMultiHashMap<Entity, Entity> Collisions;
        [NoAlias] public NativeParallelHashMap<Entity, uint> CollisionsCount;

        [NoAlias] public NativeParallelHashSet<BodiesPair> Intersections;
        [NoAlias] public NativeParallelHashMap<Entity, uint> IntersectionsCount;

        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
        [NoAlias] public NativeParallelHashMap<int, Entity> StaticMap;

        [NoAlias] public NativeArray<Random> Randoms;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
            state.RequireForUpdate<PhysicsMapRandomComponent>();
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
            if (Randoms.IsCreated) Randoms.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!EntitiesInCellsCount.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var spacialMapSingleton = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();
                var randomComponent = SystemAPI.GetSingleton<PhysicsMapRandomComponent>();
                initCollections(ref settings.BlobRef.Value, spacialMapSingleton.SpacialMap.GridSize, randomComponent.Seed);
                return;
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!physicsSingleton.Bodies.IsCreated)
                return;

            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            DynamicMap.Clear();
            EntitiesInCellsCount.Clear();
            return;
            state.Dependency = new AddDynamicBodiesJob
            {
                Bodies = physicsSingleton.Bodies,
                SpatialMap = physicsSingleton.SpacialMap,
                DynamicMap = DynamicMap.AsParallelWriter(),
                EntitiesInCellsCount = EntitiesInCellsCount,
                EntitiesInCellsCountWriter = EntitiesInCellsCount.AsParallelWriter(),
                Randoms = Randoms,
                MaxCellsPerEntity = physicsSettings.BlobRef.Value.LodData.MaxCellPerEntity,
                MaxEntitiesInCell = physicsSettings.BlobRef.Value.LodData.MaxEntitiesInCell
            }.Schedule(physicsSingleton.Bodies.Length, 16, state.Dependency);

            Handle = state.Dependency;
        }

        private void initCollections(ref PhysicsSettingsBlobAsset blob, int3 gridSize, uint seed)
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

            Randoms = new NativeArray<Random>(blob.MaxEntitiesCount, Allocator.Persistent);
            for (int i = 0; i < blob.MaxEntitiesCount; i++)
                Randoms[i] = new Random(seed + (uint)i + 1u);
        }

        [BurstCompile]
        private struct AddDynamicBodiesJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<PhysicsBodyData> Bodies;
            [ReadOnly] public SpacialMap SpatialMap;

            [WriteOnly] public NativeParallelMultiHashMap<uint, Entity>.ParallelWriter DynamicMap;
            [ReadOnly] public NativeParallelHashMap<int, uint> EntitiesInCellsCount;
            [WriteOnly] public NativeParallelHashMap<int, uint>.ParallelWriter EntitiesInCellsCountWriter;
            [NativeDisableParallelForRestriction] public NativeArray<Random> Randoms;

            public int MaxCellsPerEntity;
            public int MaxEntitiesInCell;

            public void Execute(int index)
            {
                var body = Bodies[index];
                if (body.BodyType != BodyType.Dynamic)
                    return;

                var random = Randoms[index];

                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);
                int totalCellsInArea = cellSize.x * cellSize.y * cellSize.z;
                int cubeSize = math.max(math.max(cellSize.x, cellSize.y), cellSize.z);

                if (totalCellsInArea <= MaxCellsPerEntity)
                {
                    AddToMap(body.Main, body.Position, body.Scale, startCellIndex, cubeSize);
                }
                else
                {
                    AddToMapOptimized(body.Main, body.Position, body.Scale, startCellIndex, cubeSize, ref random);
                }

                Randoms[index] = random;
            }

            private void AddToMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    if (EntitiesInCellsCount.TryGetValue(cellIndex, out uint cellCount) && cellCount >= (uint)MaxEntitiesInCell)
                        continue;

                    DynamicMap.Add((uint)cellIndex, entity);
                    EntitiesInCellsCountWriter.TryAdd(cellIndex, cellCount + 1u);
                }
            }

            private void AddToMapOptimized(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, ref Random random)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseSphereOptimizedNext(position, scale, startCellIndex, cubeSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    if (EntitiesInCellsCount.TryGetValue(cellIndex, out uint cellCount) && cellCount >= (uint)MaxEntitiesInCell)
                        continue;

                    DynamicMap.Add((uint)cellIndex, entity);
                    EntitiesInCellsCountWriter.TryAdd(cellIndex, cellCount + 1u);
                }
            }
        }
    }
}
