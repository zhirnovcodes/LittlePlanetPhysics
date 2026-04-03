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
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>()
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct ExportPhysicsDataJob : IJob
        {
            [ReadOnly] public NativeList<PhysicsBodyData> Bodies;
            public ComponentLookup<LocalTransform> LocalTransformLookup;

            public void Execute()
            {
                for (int i = 0; i < Bodies.Length; i++)
                {
                    var body = Bodies[i];
                    if (!LocalTransformLookup.TryGetComponent(body.Main, out var localTransform))
                        continue;

                    LocalTransformLookup[body.Main] =
                        new LocalTransform
                        {
                            Position = body.Position,
                            Rotation = math.mul( localTransform.Rotation, quaternion.EulerXYZ(body.RotationOffset)),
                            Scale = localTransform.Scale
                        };
                }
            }
        }
    }
}
