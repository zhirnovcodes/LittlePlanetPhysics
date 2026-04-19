using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(LittlePhysicsSystemGroup), OrderFirst = true)]
    public partial struct CollisionMapUpdateSystem : ISystem
    {
        [NoAlias] public NativeCollisionMap DynamicCollisionMap;
        [NoAlias] public NativeCollisionMap TriggersCollisionMap;
        [NoAlias] public NativeCollisionMap StaticCollisionMap;

        [NoAlias] public NativeArray<Random> Randoms;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<SpacialMapSettingsComponent>();
            state.RequireForUpdate<PhysicsMapRandomComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (DynamicCollisionMap.IsCreated) DynamicCollisionMap.Dispose();
            if (TriggersCollisionMap.IsCreated) TriggersCollisionMap.Dispose();
            if (StaticCollisionMap.IsCreated) StaticCollisionMap.Dispose();
            if (Randoms.IsCreated) Randoms.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DynamicCollisionMap.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var spacialMapSingleton = SystemAPI.GetSingleton<SpacialMapSettingsComponent>();
                var randomComponent = SystemAPI.GetSingleton<PhysicsMapRandomComponent>();
                InitCollections(ref settings.BlobRef.Value, settings.BlobRef.Value.LodData.MaxEntityCount, spacialMapSingleton.SpacialMap.GridSize, randomComponent.Seed);
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
                DynamicCollisionMap = DynamicCollisionMap,
                TriggersCollisionMap = TriggersCollisionMap,
                StaticCollisionMap = StaticCollisionMap
            }.Schedule(physicsHandle);

            var addDynamicJob = new AddBodiesJob
            {
                BodiesList = physicsSingleton.BodiesList,
                BodiesCount = physicsSingleton.BodiesCount,
                SpatialMap = physicsSingleton.SpacialMap,
                DynamicCollisionMap = DynamicCollisionMap,
                TriggersCollisionMap = TriggersCollisionMap,
                StaticCollisionMap = StaticCollisionMap,
                Randoms = Randoms,
                MaxCellsPerEntity = physicsSettings.BlobRef.Value.LodData.MaxCellPerEntity,
            }.Schedule(physicsSingleton.BodiesList.Length, 16, clearJob);

            state.Dependency = addDynamicJob;

            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        private void InitCollections(ref PhysicsSettingsBlobAsset blob, int maxBodiesForRandoms, int3 gridSize, uint seed)
        {
            ref LodPhysicsData lod = ref blob.LodData;

            DynamicCollisionMap = new NativeCollisionMap(gridSize, (uint)lod.MaxDynamicsInCells, Allocator.Persistent);
            TriggersCollisionMap = new NativeCollisionMap(gridSize, (uint)lod.MaxTriggersInCells, Allocator.Persistent);
            StaticCollisionMap = new NativeCollisionMap(gridSize, (uint)lod.MaxStaticInCells, Allocator.Persistent);

            Randoms = new NativeArray<Random>(maxBodiesForRandoms, Allocator.Persistent);
            for (int i = 0; i < maxBodiesForRandoms; i++)
                Randoms[i] = new Random(seed + (uint)i + 1u);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeCollisionMap DynamicCollisionMap;
            public NativeCollisionMap TriggersCollisionMap;
            public NativeCollisionMap StaticCollisionMap;

            public void Execute()
            {
                DynamicCollisionMap.Clear();
                TriggersCollisionMap.Clear();
            }
        }

        [BurstCompile]
        private struct AddBodiesJob : IJobParallelFor
        {
            [ReadOnly] public SpacialMap SpatialMap;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeReference<uint> BodiesCount;

            [NativeDisableParallelForRestriction] public NativeCollisionMap DynamicCollisionMap;
            [NativeDisableParallelForRestriction] public NativeCollisionMap TriggersCollisionMap;
            [NativeDisableParallelForRestriction] public NativeCollisionMap StaticCollisionMap;
            [NativeDisableParallelForRestriction] public NativeArray<Random> Randoms;

            public int MaxCellsPerEntity;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                    return;

                var body = BodiesList[index];

                if (!body.ShouldUpdateMap)
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

                if (totalCellsInArea <= MaxCellsPerEntity)
                {
                    AddToDynamicMap(body.Position, body.Scale, startCellIndex, cellSize, index);
                }
                else
                {
                    AddDynamicToMapOptimized(body.Position, body.Scale, startCellIndex, cellSize, ref random, index);
                }

                Randoms[index] = random;
            }

            private void AddBodyToStatic(int index, PhysicsBodyData body)
            {
                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);

                AddToStaticMap(body.Position, body.Scale, startCellIndex, cellSize, index);
            }

            private void AddBodyToTrigger(int index, PhysicsBodyData body)
            {
                var random = Randoms[index];

                SpatialMap.GetCellIndices(body.Position, body.Scale, out int startCellIndex, out int3 cellSize);
                int totalCellsInArea = cellSize.x * cellSize.y * cellSize.z;

                if (totalCellsInArea <= MaxCellsPerEntity)
                {
                    AddToTriggersMap(body.Position, body.Scale, startCellIndex, cellSize, index);
                }
                else
                {
                    AddTriggerToMapOptimized(body.Position, body.Scale, startCellIndex, cellSize, ref random, index);
                }

                Randoms[index] = random;
            }

            private void AddToDynamicMap(float3 position, float scale, int startCellIndex, int3 cellSize, int bodyIndex)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseBoxNext(startCellIndex, cellSize, ref iterator, out int cellIndex))
                {
                    DynamicCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex);
                }
            }

            private void AddDynamicToMapOptimized(float3 position, float scale, int startCellIndex, int3 cellSize, ref Random random, int bodyIndex)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseBoxOptimizedNext(startCellIndex, cellSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    DynamicCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex);
                }
            }

            private void AddToStaticMap(float3 position, float scale, int startCellIndex, int3 cellSize, int bodyIndex)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseBoxNext(startCellIndex, cellSize, ref iterator, out int cellIndex))
                {
                    StaticCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex);
                }
            }

            private void AddToTriggersMap(float3 position, float scale, int startCellIndex, int3 cellSize, int bodyIndex)
            {
                var iterator = new TraverseCubeIterator();
                while (SpatialMap.TraverseBoxNext(startCellIndex, cellSize, ref iterator, out int cellIndex))
                {
                    TriggersCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex);
                }
            }

            private void AddTriggerToMapOptimized(float3 position, float scale, int startCellIndex, int3 cellSize, ref Random random, int bodyIndex)
            {
                var iterator = new TraverseCubeOptimizedIterator();
                while (SpatialMap.TraverseBoxOptimizedNext(startCellIndex, cellSize, MaxCellsPerEntity, ref random, ref iterator, out int cellIndex))
                {
                    TriggersCollisionMap.TryAdd((uint)cellIndex, (uint)bodyIndex);
                }
            }
        }
    }
}
