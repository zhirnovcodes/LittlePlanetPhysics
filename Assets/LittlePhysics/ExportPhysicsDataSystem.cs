using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(ImportPhysicsDataSystem))]
    public partial struct ExportPhysicsDataSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            state.Dependency = new ExportPhysicsDataJob
            {
                Bodies = singleton.Bodies
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ExportPhysicsDataJob : IJobEntity
    {
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PhysicsBodyData> Bodies;

        public void Execute(ref LocalTransform transform, in PhysicsBodyComponent body, in PhysicsBodyIndexComponent bodyIndex)
        {
            if (body.BodyType != BodyType.Dynamic)
                return;

            var data = Bodies[bodyIndex.Value];
            transform.Position = data.Position;
            transform.Rotation = quaternion.EulerXYZ(data.RotationOffset);
        }
    }
}
