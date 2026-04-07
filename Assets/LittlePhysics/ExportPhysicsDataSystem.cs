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
            if (!singleton.BodiesList.IsCreated)
                return;

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);
            var velocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(false);
            velocityLookup.Update(ref state);

            state.Dependency = new ExportPhysicsDataJob
            {
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
                VelocityLookup = velocityLookup,
            }.Schedule(combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private partial struct ExportPhysicsDataJob : IJobEntity
        {
            [ReadOnly] public NativeList<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;

            public void Execute(Entity entity, ref LocalTransform transform, in PhysicsBodyComponent body, ref PhysicsBodyUpdateComponent tag)
            {
                if (body.BodyType == BodyType.Dynamic == false)
                    return;

                if (!tag.IsEnabled)
                    return;

                if (tag.Index < 0 || tag.Index >= BodiesList.Length)
                    return;

                var bodyData = BodiesList[tag.Index];
                transform.Position = bodyData.Position - body.LocalPosition;
                transform.Rotation = math.mul(transform.Rotation, quaternion.EulerXYZ(bodyData.RotationOffset));

                if (!VelocityLookup.TryGetComponent(entity, out var velComp))
                    return;

                var pv = PhysicsVelocities[tag.Index];
                velComp.Linear = pv.Linear;
                velComp.Angular = pv.Angular;
                VelocityLookup[entity] = velComp;
            }
        }
    }
}
