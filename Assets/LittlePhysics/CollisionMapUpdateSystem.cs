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
        [NoAlias] public NativeCollisionMap DynamicCollisionMap;

        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
        [NoAlias] public NativeParallelHashMap<uint, Entity> StaticMap;

        [NoAlias] public NativeArray<Random> Randoms;
        [NoAlias] public NativeList<PhysicsBodyData> SelectedBodies;

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
            if (DynamicCollisionMap.IsCreated) DynamicCollisionMap.Dispose();
            if (DynamicMap.IsCreated) DynamicMap.Dispose();
            if (TriggersMap.IsCreated) TriggersMap.Dispose();
            if (StaticMap.IsCreated) StaticMap.Dispose();
            if (Randoms.IsCreated) Randoms.Dispose();
            if (SelectedBodies.IsCreated) SelectedBodies.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DynamicInCellsCount.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var spacialMapSingleton = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();
                var randomComponent = SystemAPI.GetSingleton<PhysicsMapRandomComponent>();
                InitCollections(ref settings.BlobRef.Value, spacialMapSingleton.SpacialMap.GridSize, randomComponent.Seed);
                return;
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!physicsSingleton.BodiesList.IsCreated)
                return;

            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                DynamicCollisionMap = DynamicCollisionMap
            }.Schedule(physicsHandle);

            var selectJob = new SelectBodiesJob
            {
                BodiesList = physicsSingleton.BodiesList,
                SelectedBodies = SelectedBodies,
                MaxEntitiesCount = physicsSettings.BlobRef.Value.LodData.MaxEntityCount
            }.Schedule(clearJob);

            var addDynamicJob = new AddBodiesJob
            {
                //SelectedIndices = SelectedIndices,
                SelectedBodies = SelectedBodies,
                SpatialMap = physicsSingleton.SpacialMap,
                //DynamicMap = DynamicMap.AsParallelWriter(),
                //TriggersMap = TriggersMap.AsParallelWriter(),
                //StaticMap = StaticMap.AsParallelWriter(),
               // DynamicInCellsCount = DynamicInCellsCount,
                //TriggersInCellsCount = TriggersInCellsCount,
                DynamicCollisionMap = DynamicCollisionMap,
                Randoms = Randoms,
                MaxCellsPerEntity = physicsSettings.BlobRef.Value.LodData.MaxCellPerEntity,
                MaxEntitiesInCell = physicsSettings.BlobRef.Value.LodData.MaxEntitiesInCell
            }.Schedule(SelectedBodies, 32, selectJob);

            state.Dependency = addDynamicJob;

            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void InitCollections(ref PhysicsSettingsBlobAsset blob, int3 gridSize, uint seed)
        {
            int totalCells = gridSize.x * gridSize.y * gridSize.z;

            DynamicInCellsCount = new NativeArray<int>(totalCells, Allocator.Persistent);
            TriggersInCellsCount = new NativeArray<int>(totalCells, Allocator.Persistent);

            DynamicCollisionMap = new NativeCollisionMap(
                (uint)gridSize.x, (uint)blob.GetMaxEntitiesInCell(), Allocator.Persistent);

            DynamicMap = new NativeParallelMultiHashMap<uint, Entity>(
                totalCells * blob.GetMaxEntitiesInCell(), Allocator.Persistent);

            TriggersMap = new NativeParallelMultiHashMap<uint, Entity>(
                totalCells * blob.GetMaxEntitiesInCell(), Allocator.Persistent);

            StaticMap = new NativeParallelHashMap<uint, Entity>(
                totalCells, Allocator.Persistent);

            Randoms = new NativeArray<Random>(blob.LodData.MaxEntityCount, Allocator.Persistent);
            for (int i = 0; i < blob.LodData.MaxEntityCount; i++)
                Randoms[i] = new Random(seed + (uint)i + 1u);

            SelectedBodies = new NativeList<PhysicsBodyData>(blob.LodData.MaxEntityCount, Allocator.Persistent);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeCollisionMap DynamicCollisionMap;

            public void Execute()
            {
                DynamicCollisionMap.Clear();
            }
        }

        [BurstCompile]
        private struct SelectBodiesJob : IJob
        {
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;
            public NativeList<PhysicsBodyData> SelectedBodies;
            public int MaxEntitiesCount;

            public void Execute()
            {
                SelectedBodies.Clear();
                int total = BodiesList.Length;
                if (total == 0)
                    return;

                int interval = math.max(1, (total + MaxEntitiesCount - 1) / MaxEntitiesCount);
                
                for (int i = 0; i < total; i += interval)
                {
                    SelectedBodies.Add(BodiesList[i]);
                }
            }
        }

        [BurstCompile]
        private struct AddBodiesJob : IJobParallelForDefer
        {
            [ReadOnly] public SpacialMap SpatialMap;
            [ReadOnly] public NativeList<PhysicsBodyData> SelectedBodies;

            //[WriteOnly] public NativeParallelMultiHashMap<uint, Entity>.ParallelWriter DynamicMap;
            //[WriteOnly] public NativeParallelMultiHashMap<uint, Entity>.ParallelWriter TriggersMap;
            //[WriteOnly] public NativeParallelHashMap<uint, Entity>.ParallelWriter StaticMap;

            //[NativeDisableParallelForRestriction] public NativeArray<int> DynamicInCellsCount;
            //[NativeDisableParallelForRestriction] public NativeArray<int> TriggersInCellsCount;
            [NativeDisableParallelForRestriction] public NativeCollisionMap DynamicCollisionMap;
            [NativeDisableParallelForRestriction] public NativeArray<Random> Randoms;

            public int MaxCellsPerEntity;
            public int MaxEntitiesInCell;

            public void Execute(int index)
            {
                var body = SelectedBodies[index];

                if (!body.ShouldUpdate)
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
                    AddToDynamicMap(body.Main, body.Position, body.Scale, startCellIndex, cubeSize, index);
                }
                else
                {
                    AddDynamicToMapOptimized(body.Main, body.Position, body.Scale, startCellIndex, cubeSize, ref random, index);
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

            private void AddToDynamicMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, int bodyIndex)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseCubeNext(startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    if (DynamicCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex))
                    {
                        //DynamicMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private void AddDynamicToMapOptimized(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, ref Random random, int bodyIndex)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseCubeOptimizedNext(startCellIndex, cubeSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    if (DynamicCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex))
                    {
                        //DynamicMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private void AddToStaticMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    //StaticMap.TryAdd((uint)cellIndex, entity);
                }
            }

            private void AddToTriggersMap(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseSphereNext(position, scale, startCellIndex, cubeSize, ref iterator, out int cellIndex))
                {
                    //if (CanAddTrigger(cellIndex))
                    {
                        //TriggersMap.Add((uint)cellIndex, entity);
                    }
                }
            }

            private void AddTriggerToMapOptimized(Entity entity, float3 position, float scale, int startCellIndex, int cubeSize, ref Random random)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseSphereOptimizedNext(position, scale, startCellIndex, cubeSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    //if (CanAddTrigger(cellIndex))
                    {
                        //TriggersMap.Add((uint)cellIndex, entity);
                    }
                }
            }
            /*
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
            }*/
        }
    }
}
