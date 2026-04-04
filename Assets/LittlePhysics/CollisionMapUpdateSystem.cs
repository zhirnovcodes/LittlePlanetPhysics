using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial struct CollisionMapUpdateSystem : ISystem
    {
        [NoAlias] public NativeArray<int> DynamicInCellsCount;
        [NoAlias] public NativeArray<int> TriggersInCellsCount;

        [NoAlias] public NativeParallelMultiHashMap<Entity, Entity> Collisions;
        [NoAlias] public NativeParallelHashMap<Entity, uint> CollisionsCount;

        [NoAlias] public NativeParallelHashSet<BodiesPair> Intersections;
        [NoAlias] public NativeParallelHashMap<Entity, uint> IntersectionsCount;

        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
        [NoAlias] public NativeParallelHashMap<uint, Entity> StaticMap;

        [NoAlias] public NativeArray<Random> Randoms;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
            state.RequireForUpdate<PhysicsMapRandomComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (DynamicInCellsCount.IsCreated) DynamicInCellsCount.Dispose();
            if (TriggersInCellsCount.IsCreated) TriggersInCellsCount.Dispose();
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
            if (!DynamicInCellsCount.IsCreated)
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
            if (!physicsSingleton.BodiesEntities.IsCreated)
                return;

            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                DynamicMap = DynamicMap,
                TriggersMap = TriggersMap,
                DynamicInCellsCount = DynamicInCellsCount,
                TriggersInCellsCount = TriggersInCellsCount
            }.Schedule(physicsHandle);

            var addDynamicJob = new AddBodiesJob
            {
                BodiesEntities = physicsSingleton.BodiesEntities,
                Bodies = physicsSingleton.Bodies,
                SpatialMap = physicsSingleton.SpacialMap,
                DynamicMap = DynamicMap.AsParallelWriter(),
                TriggersMap = TriggersMap.AsParallelWriter(),
                StaticMap = StaticMap.AsParallelWriter(),
                DynamicInCellsCount = DynamicInCellsCount,
                TriggersInCellsCount = TriggersInCellsCount,
                Randoms = Randoms,
                MaxCellsPerEntity = physicsSettings.BlobRef.Value.LodData.MaxCellPerEntity,
                MaxEntitiesInCell = physicsSettings.BlobRef.Value.LodData.MaxEntitiesInCell
            }.Schedule(physicsSettings.BlobRef.Value.MaxEntitiesCount, 16, clearJob);

            state.Dependency = JobHandle.CombineDependencies(clearJob, addDynamicJob);

            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void initCollections(ref PhysicsSettingsBlobAsset blob, int3 gridSize, uint seed)
        {
            int totalCells = gridSize.x * gridSize.y * gridSize.z;

            DynamicInCellsCount = new NativeArray<int>(totalCells, Allocator.Persistent);
            TriggersInCellsCount = new NativeArray<int>(totalCells, Allocator.Persistent);

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

            StaticMap = new NativeParallelHashMap<uint, Entity>(
                totalCells, Allocator.Persistent);

            Randoms = new NativeArray<Random>(blob.MaxEntitiesCount, Allocator.Persistent);
            for (int i = 0; i < blob.MaxEntitiesCount; i++)
                Randoms[i] = new Random(seed + (uint)i + 1u);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            [WriteOnly] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
            [WriteOnly] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
            public NativeArray<int> DynamicInCellsCount;
            public NativeArray<int> TriggersInCellsCount;

            public void Execute()
            {
                DynamicMap.Clear();
                TriggersMap.Clear();
                for (int i = 0; i < DynamicInCellsCount.Length; i++)
                    DynamicInCellsCount[i] = 0;
                for (int i = 0; i < TriggersInCellsCount.Length; i++)
                    TriggersInCellsCount[i] = 0;
            }
        }

        [BurstCompile]
        private struct AddBodiesJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<Entity> BodiesEntities;
            [ReadOnly] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
            [ReadOnly] public SpacialMap SpatialMap;

            [WriteOnly] public NativeParallelMultiHashMap<uint, Entity>.ParallelWriter DynamicMap;
            [WriteOnly] public NativeParallelMultiHashMap<uint, Entity>.ParallelWriter TriggersMap;
            [WriteOnly] public NativeParallelHashMap<uint, Entity>.ParallelWriter StaticMap;

            [NativeDisableParallelForRestriction] public NativeArray<int> DynamicInCellsCount;
            [NativeDisableParallelForRestriction] public NativeArray<int> TriggersInCellsCount;
            [NativeDisableParallelForRestriction] public NativeArray<Random> Randoms;

            public int MaxCellsPerEntity;
            public int MaxEntitiesInCell;

            public void Execute(int index)
            {
                if (index >= BodiesEntities.Length)
                    return;

                var entity = BodiesEntities[index];
                if (!Bodies.TryGetValue(entity, out var body))
                    return;

                switch (body.BodyType)
                {
                    case BodyType.Dynamic:
                        AddBodyToDynamic(index, body);
                        break;
                    case BodyType.Static:
                        AddBodyToStatic(index, body);
                        break;
                    case BodyType.Trigger:
                        AddBodyToTrigger(index, body);
                        break;
                }

            }

            private void AddBodyToDynamic(int index, PhysicsBodyData body)
            {
                var random = Randoms[index];

                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);
                int totalCellsInArea = cellSize.x * cellSize.y * cellSize.z;
                int cubeSize = math.max(math.max(cellSize.x, cellSize.y), cellSize.z);

                if (totalCellsInArea <= MaxCellsPerEntity)
                {
                    AddToDynamicMap(body.Main, body.Position, body.Scale, startCellIndex, cubeSize);
                }
                else
                {
                    AddDynamicToMapOptimized(body.Main, body.Position, body.Scale, startCellIndex, cubeSize, ref random);
                }

                Randoms[index] = random;
            }

            private void AddBodyToStatic(int index, PhysicsBodyData body)
            {
                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);
                int cubeSize = math.max(math.max(cellSize.x, cellSize.y), cellSize.z);

                AddToStaticMap(body.Main, body.Position, body.Scale, startCellIndex, cubeSize);
            }

            private void AddBodyToTrigger(int index, PhysicsBodyData body)
            {
                var random = Randoms[index];

                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);
                int totalCellsInArea = cellSize.x * cellSize.y * cellSize.z;
                int cubeSize = math.max(math.max(cellSize.x, cellSize.y), cellSize.z);

                if (totalCellsInArea <= MaxCellsPerEntity)
                {
                    AddToTriggersMap(body.Main, body.Position, body.Scale, startCellIndex, cubeSize);
                }
                else
                {
                    AddTriggerToMapOptimized(body.Main, body.Position, body.Scale, startCellIndex, cubeSize, ref random);
                }

                Randoms[index] = random;
            }

            private void AddToDynamicMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    if (CanAddDynamic(cellIndex))
                    {
                        DynamicMap.Add((uint)cellIndex, entity);
                    }

                }
            }

            private void AddDynamicToMapOptimized(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, ref Random random)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseSphereOptimizedNext(position, scale, startCellIndex, cubeSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    if (CanAddDynamic(cellIndex))
                    {
                        DynamicMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private void AddToStaticMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    StaticMap.TryAdd((uint)cellIndex, entity);
                }
            }

            private void AddToTriggersMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    if (CanAddTrigger(cellIndex))
                    {
                        TriggersMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private void AddTriggerToMapOptimized(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, ref Random random)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseSphereOptimizedNext(position, scale, startCellIndex, cubeSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    if (CanAddTrigger(cellIndex))
                    {
                        TriggersMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private unsafe bool CanAddDynamic(int cellIndex)
            {
                ref int count = ref UnsafeUtility.ArrayElementAsRef<int>(DynamicInCellsCount.GetUnsafePtr(), cellIndex);
                int prev = Interlocked.Increment(ref count) - 1;
                if (prev >= MaxEntitiesInCell)
                {
                    Interlocked.Decrement(ref count);
                    return false;
                }
                return true;
            }

            private unsafe bool CanAddTrigger(int cellIndex)
            {
                ref int count = ref UnsafeUtility.ArrayElementAsRef<int>(TriggersInCellsCount.GetUnsafePtr(), cellIndex);
                int prev = Interlocked.Increment(ref count) - 1;
                if (prev >= MaxEntitiesInCell)
                {
                    Interlocked.Decrement(ref count);
                    return false;
                }
                return true;
            }
        }
    }
}
