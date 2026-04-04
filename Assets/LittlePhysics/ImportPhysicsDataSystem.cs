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
                Bodies = singleton.Bodies,
                BodiesEntities = singleton.BodiesEntities
            }.Schedule(combinedDep);

            var importJob = new ImportPhysicsDataJob
            {
                Bodies = singleton.Bodies,
                BodiesEntities = singleton.BodiesEntities,
                MaxEntitiesCount = maxEntitiesCount,
                ECB = ecb
            }.Schedule(clearJob);

            state.Dependency = JobHandle.CombineDependencies(clearJob, importJob);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }
    }

    [BurstCompile]
    public partial struct ClearJob : IJob
    {
        public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
        public NativeList<Entity> BodiesEntities;

        public void Execute()
        {
            Bodies.Clear();
            BodiesEntities.Clear();
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<Entity> BodiesEntities;
        public int MaxEntitiesCount;
        public EntityCommandBuffer ECB;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body, in PhysicsBodyUpdateTag tag)
        {
            if (BodiesEntities.Length >= MaxEntitiesCount)
                return;

            BodiesEntities.Add(entity);
            Bodies.TryAdd(entity, body.ToBodyData(entity, transform));

            if (body.BodyType == BodyType.Static)
            {
                ECB.SetComponentEnabled<PhysicsBodyUpdateTag>(entity, false);
            }
        }
    }
}
