using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
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

            // Complete any previous-frame job that was reading/writing Bodies
            // before touching the list on the main thread.
            //singleton.PhysicsJobHandle.Complete();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            var clearJob = new ClearJob
            {
                Bodies = singleton.Bodies
            }.Schedule(combinedDep);

            var inportJob = new ImportPhysicsDataJob
            {
                Bodies = singleton.Bodies
            }.Schedule(clearJob);

            state.Dependency = JobHandle.CombineDependencies(clearJob, inportJob);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }
    }

    [BurstCompile]
    public partial struct ClearJob : IJob
    {
        public NativeList<PhysicsBodyData> Bodies;
        public void Execute()
        {
            Bodies.Clear();
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PhysicsBodyData> Bodies;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body)
        {
            Bodies.Add(body.ToBodyData(entity, transform));
        }
    }
}
