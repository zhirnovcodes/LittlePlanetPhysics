using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct CollisionVelocityCalculateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            var detectionHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<CollisionDetectionSystem>();
            ref var detectionSystem = ref state.WorldUnmanaged.GetUnsafeSystemRef<CollisionDetectionSystem>(detectionHandle);

            if (!singleton.BodiesList.IsCreated || !detectionSystem.Collisions.IsCreated)
                return;
            if (!singleton.PhysicsVelocities.IsCreated)
                return;

            state.Dependency = new CalculateCollisionVelocitiesJob
            {
                BodiesList = singleton.BodiesList,
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct CalculateCollisionVelocitiesJob : IJob
        {
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;


            public void Execute()
            {
            }
        }
    }
}
