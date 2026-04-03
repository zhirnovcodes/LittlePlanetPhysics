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
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            state.Dependency = new ImportPhysicsDataJob
            {
                Bodies = singleton.Bodies
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PhysicsBodyData> Bodies;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body, in PhysicsBodyIndexComponent bodyIndex)
        {
            Bodies[bodyIndex.Value] = body.ToBodyData(entity, transform);
        }
    }
}
