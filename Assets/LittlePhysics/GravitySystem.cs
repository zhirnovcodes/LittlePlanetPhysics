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
    [UpdateBefore(typeof(PhysicsVelocitySystem))]
    public partial struct GravitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<PhysicsSettingsComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!singleton.BodiesList.IsCreated || !singleton.PhysicsVelocities.IsCreated)
                return;

            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            int bodyCount = settings.BlobRef.Value.LodData.MaxEntityCount;
            float deltaTime = SystemAPI.Time.DeltaTime;

            var dep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            if (SystemAPI.HasSingleton<SphericalGravitySourceComponent>())
            {
                dep = new SphericalGravityJob
                {
                    Source = SystemAPI.GetSingleton<SphericalGravitySourceComponent>(),
                    BodiesList = singleton.BodiesList,
                    PhysicsVelocities = singleton.PhysicsVelocities,
                    BodiesCount = singleton.BodiesCount,
                    DeltaTime = deltaTime,
                }.Schedule(bodyCount, 32, dep);
            }

            if (SystemAPI.HasSingleton<DirectionalGravitySourceComponent>())
            {
                dep = new DirectionalGravityJob
                {
                    Source = SystemAPI.GetSingleton<DirectionalGravitySourceComponent>(),
                    BodiesList = singleton.BodiesList,
                    PhysicsVelocities = singleton.PhysicsVelocities,
                    BodiesCount = singleton.BodiesCount,
                    DeltaTime = deltaTime,
                }.Schedule(bodyCount, 32, dep);
            }

            state.Dependency = dep;
            singleton.PhysicsJobHandle = dep;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct SphericalGravityJob : IJobParallelFor
        {
            [ReadOnly] public SphericalGravitySourceComponent Source;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            public float DeltaTime;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                    return;

                var body = BodiesList[index];
                if (body.BodyType != BodyType.Dynamic)
                    return;

                float3 toSource = Source.Center - body.Position;
                float distance = math.length(toSource);

                if (distance < 0.001f)
                    return;

                float3 direction = toSource / distance;
                float gravityMagnitude = Source.SurfaceGravity * (Source.Radius * Source.Radius) / (distance * distance);

                var velocity = PhysicsVelocities[index];
                velocity.Linear += direction * gravityMagnitude * DeltaTime;
                PhysicsVelocities[index] = velocity;
            }
        }

        [BurstCompile]
        private struct DirectionalGravityJob : IJobParallelFor
        {
            [ReadOnly] public DirectionalGravitySourceComponent Source;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            public float DeltaTime;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                    return;

                var body = BodiesList[index];
                if (body.BodyType != BodyType.Dynamic)
                    return;

                var velocity = PhysicsVelocities[index];
                velocity.Linear += Source.Direction * Source.Strength * DeltaTime;
                PhysicsVelocities[index] = velocity;
            }
        }
    }
}
