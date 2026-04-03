using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct LittlePhysicsUpdateSystem : ISystem
    {
        [NoAlias] public NativeParallelHashMap<int, DynamicPhysicsData> DynamicData;
        [NoAlias] public NativeParallelHashMap<int, StaticPhysicsData> StaticData;
        [NoAlias] public NativeParallelHashMap<int, TriggerPhysicsData> TriggerData;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (DynamicData.IsCreated) DynamicData.Dispose();
            if (StaticData.IsCreated) StaticData.Dispose();
            if (TriggerData.IsCreated) TriggerData.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DynamicData.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var capacity = settings.BlobRef.Value.MaxEntitiesCount;
                DynamicData = new NativeParallelHashMap<int, DynamicPhysicsData>(capacity, Allocator.Persistent);
                StaticData = new NativeParallelHashMap<int, StaticPhysicsData>(capacity, Allocator.Persistent);
                TriggerData = new NativeParallelHashMap<int, TriggerPhysicsData>(capacity, Allocator.Persistent);
            }

            state.Dependency = new MoveRightJob
            {
                DynamicData = DynamicData,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct MoveRightJob : IJob
        {
            public NativeParallelHashMap<int, DynamicPhysicsData> DynamicData;
            public float DeltaTime;

            public void Execute()
            {
                var keys = DynamicData.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    var data = DynamicData[keys[i]];
                    data.Position.x += 0.5f * DeltaTime;
                    DynamicData[keys[i]] = data;
                }
                keys.Dispose();
            }
        }
    }
}
