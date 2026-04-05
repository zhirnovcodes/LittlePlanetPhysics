using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            var maxEntitiesCount = SystemAPI.GetSingleton<PhysicsSettingsComponent>().BlobRef.Value.MaxEntitiesCount;

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var clearJob = new ClearJob
            {
                BodiesList = singleton.BodiesList
            }.Schedule(combinedDep);

            var importJob = new ImportPhysicsDataJob
            {
                BodiesList = singleton.BodiesList,
                MaxEntitiesCount = maxEntitiesCount,
                ECB = ecb,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(clearJob);

            state.Dependency = JobHandle.CombineDependencies(clearJob, importJob);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }
    }

    [BurstCompile]
    public partial struct ClearJob : IJob
    {
        public NativeList<PhysicsBodyData> BodiesList;

        public void Execute()
        {
            BodiesList.Clear();
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PhysicsBodyData> BodiesList;
        public int MaxEntitiesCount;
        public EntityCommandBuffer ECB;
        public float DeltaTime;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body, ref PhysicsBodyUpdateComponent tag)
        {
            if (BodiesList.Length >= MaxEntitiesCount)
                return;

            bool shouldUpdate = false;

            switch (tag.Type)
            {
                case UpdateType.EveryFrame:
                    shouldUpdate = true;
                    break;
                case UpdateType.Once:
                    if (tag.WasUpdated == false)
                    {
                        tag.WasUpdated = true;
                        shouldUpdate = true;
                    }
                    break;
                case UpdateType.WithInterval:
                    if (tag.WasUpdated)
                    {
                        shouldUpdate = (int)math.floor(tag.TimeElapsed / tag.Interval) != (int)math.floor((tag.TimeElapsed - DeltaTime) / tag.Interval);
                    }
                    else
                    {
                        tag.WasUpdated = true;
                        shouldUpdate = true;
                    }

                    tag.TimeElapsed += DeltaTime;
                    break;
            }

            var bodyData = body.ToBodyData(entity, transform, shouldUpdate);

            tag.Index = BodiesList.Length;
            BodiesList.Add(bodyData);
        }
    }
}
