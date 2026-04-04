using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
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
            if (!singleton.Bodies.IsCreated)
                return;

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            state.Dependency = new ExportPhysicsDataJob
            {
                Bodies = singleton.Bodies,
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private partial struct ExportPhysicsDataJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;

            public void Execute(Entity entity, ref LocalTransform transform, in PhysicsBodyComponent body, in PhysicsBodyUpdateTag tag)
            {
                if (body.BodyType == BodyType.Dynamic == false)
                    return;

                if (!Bodies.TryGetValue(entity, out var bodyData))
                    return;

                transform.Position = bodyData.Position - body.LocalPosition;
                transform.Rotation = math.mul(transform.Rotation, quaternion.EulerXYZ(bodyData.RotationOffset));
            }
        }
    }
}
