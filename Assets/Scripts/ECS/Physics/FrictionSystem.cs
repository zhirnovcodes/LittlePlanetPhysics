using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(LittlePhysicsInternalSystemGroup))]
    [UpdateAfter(typeof(SurfaceCollisionSystem))]
    public partial struct FrictionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<LittlePhysicsTimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            if (!physicsSingleton.BodiesList.IsCreated || !physicsSingleton.PhysicsVelocities.IsCreated)
            {
                return;
            }

            if (!physicsSingleton.CollisionMap.SurfaceCollisionMap.IsCreated)
            {
                return;
            }

            var physicsTime = SystemAPI.GetSingleton<LittlePhysicsTimeComponent>();
            var physicsSettings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            bool hasSurface = SystemAPI.HasSingleton<CollisionSurfaceComponent>();
            PhysicsBodyData surfaceBody = default;
            if (hasSurface)
            {
                surfaceBody = SystemAPI.GetSingleton<CollisionSurfaceComponent>().ToBodyData();
            }

            var combinedDependency = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            state.Dependency = new ApplyFrictionJob
            {
                BodiesList = physicsSingleton.BodiesList,
                PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                SurfaceCollisionMap = physicsSingleton.CollisionMap.SurfaceCollisionMap,
                BodiesCount = physicsSingleton.BodiesCount,
                DeltaTime = physicsTime.DeltaTime,
                AirFriction = physicsSettings.BlobRef.Value.EnvironmentSettings.AirFriction,
                SurfaceBody = surfaceBody,
            }.Schedule(physicsSingleton.BodiesList.Length, 32, combinedDependency);

            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        [BurstCompile]
        private struct ApplyFrictionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [NativeDisableContainerSafetyRestriction] public NativeArray<SurfaceCollisionData> SurfaceCollisionMap;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            public float DeltaTime;
            public float AirFriction;
            public PhysicsBodyData SurfaceBody;

            public void Execute(int index)
            {
                if ((uint)index >= BodiesCount.Value)
                {
                    return;
                }

                var body = BodiesList[index];
                if (body.BodyType != BodyType.Dynamic)
                {
                    return;
                }

                var velocity = PhysicsVelocities[index];
                bool isOnSurface = SurfaceCollisionMap[index].IsColliding;

                float3 frictionLinear;
                float3 frictionAngular;

                if (isOnSurface)
                {
                    float3 contactPoint = SurfaceCollisionMap[index].ContactPoint;
                    ApplyFrictionWithSurface(body, SurfaceBody, velocity, contactPoint, out frictionLinear, out frictionAngular);
                    ApplyAirFriction(velocity, out var frictionAirLinear, out var frictionAirAngular);
                    frictionLinear += frictionAirLinear;
                    frictionAngular += frictionAirAngular;
                }
                else
                {
                    ApplyAirFriction(velocity, out frictionLinear, out frictionAngular);
                }

                velocity.Linear += frictionLinear;
                velocity.Angular += frictionAngular;
                PhysicsVelocities[index] = velocity;
            }

            private void ApplyFrictionWithSurface(
                in PhysicsBodyData body,
                in PhysicsBodyData surfaceBody,
                in PhysicsVelocityData velocity,
                float3 contactPoint,
                out float3 frictionLinear,
                out float3 frictionAngular)
            {
                if (surfaceBody.Friction <= 0)
                {
                    frictionLinear = float3.zero;
                    frictionAngular = float3.zero;
                    return;
                }

                float3 bodyRadiusVec = CollisionForces.GetRadiusVector(body, contactPoint);
                float3 surfaceRadiusVec = CollisionForces.GetRadiusVector(surfaceBody, contactPoint);
                float3 contactDelta = bodyRadiusVec - surfaceRadiusVec;
                float contactDeltaLength = math.length(contactDelta);

                if (contactDeltaLength < 0.0001f)
                {
                    frictionLinear = float3.zero;
                    frictionAngular = float3.zero;
                    return;
                }

                float3 surfaceNormal = contactDelta / contactDeltaLength;
                float normalComponent = math.dot(velocity.Linear, surfaceNormal);
                float3 tangentialVelocity = velocity.Linear - surfaceNormal * normalComponent;
                float frictionFactor = math.clamp(surfaceBody.Friction * DeltaTime, 0f, 1f);

                frictionLinear = -tangentialVelocity * frictionFactor;
                frictionAngular = -velocity.Angular * frictionFactor;
            }

            private void ApplyAirFriction(
                in PhysicsVelocityData velocity,
                out float3 frictionLinear,
                out float3 frictionAngular)
            {
                if (AirFriction <= 0)
                {
                    frictionLinear = float3.zero;
                    frictionAngular = float3.zero;
                    return;
                }

                float frictionFactor = math.clamp(AirFriction * DeltaTime, 0f, 1f);
                frictionLinear = -velocity.Linear * frictionFactor;
                frictionAngular = -velocity.Angular * frictionFactor;
            }
        }
    }
}
