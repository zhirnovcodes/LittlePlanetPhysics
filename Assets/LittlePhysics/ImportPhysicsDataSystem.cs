using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ImportPhysicsDataSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            UnityEngine.Debug.Log(1);
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            state.Dependency = new ImportPhysicsDataJob
            {
                DynamicData = singleton.DynamicData,
                StaticData = singleton.StaticData,
                TriggerData = singleton.TriggerData
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        // Each entity writes to its own unique key - no actual races occur
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<int, DynamicPhysicsData> DynamicData;
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<int, StaticPhysicsData> StaticData;
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<int, TriggerPhysicsData> TriggerData;

        public void Execute(in LocalTransform transform, in PhysicsBodyComponent body, in PhysicsBodyIndexComponent bodyIndex)
        {
            switch (body.BodyType)
            {
                case BodyType.Dynamic:
                    DynamicData[bodyIndex.Value] = body.ToDynamicData(transform);
                    break;
                case BodyType.Static:
                    StaticData[bodyIndex.Value] = body.ToStaticData(transform);
                    break;
                case BodyType.Trigger:
                    TriggerData[bodyIndex.Value] = body.ToTriggerData(transform);
                    break;
            }
        }
    }
}
