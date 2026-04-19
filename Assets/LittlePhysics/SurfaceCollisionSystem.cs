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
    [UpdateAfter(typeof(CollisionMapUpdateSystem))]
    public partial struct SurfaceCollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<CollisionSurfaceComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
            {
                return;
            }

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            if (!physicsSingleton.PhysicsVelocities.IsCreated)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<LittlePhysicsTimeComponent>(out var time))
            {
                return;
            }

            var physicsHandle = JobHandle.CombineDependencies(state.Dependency, physicsSingleton.PhysicsJobHandle);

            var surfaceJob = new CheckDynamicVsSurfaceJob
            {
                SurfaceBody = SystemAPI.GetSingleton<CollisionSurfaceComponent>().ToBodyData(),
                BodiesList = physicsSingleton.BodiesList,
                BodiesCount = physicsSingleton.BodiesCount,
                PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                PhysicsSettings = physicsSingleton.Settings,
                DeltaTime = time.DeltaTime,
            }.Schedule(physicsSingleton.BodiesList.Length, 32, physicsHandle);

            state.Dependency = surfaceJob;
            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        [BurstCompile]
        private struct CheckDynamicVsSurfaceJob : IJobParallelFor
        {
            public PhysicsBodyData SurfaceBody;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public PhysicsSettingsComponent PhysicsSettings;
            public float DeltaTime;

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

                if (!PhysicsSettings.IsColliding(body.Layer, SurfaceBody.Layer))
                {
                    return;
                }

                if (!CollisionMethods.AreBodiesColliding(body, SurfaceBody, out float3 contactPoint))
                {
                    return;
                }

                var vel = PhysicsVelocities[index];

                CollisionForces.GetCollisionImpulses(body, SurfaceBody, vel, default, contactPoint,
                    out float3 impulse, out _);
                CollisionForces.GetPushOutForce(body, SurfaceBody, contactPoint,
                    out float3 pushForce, out _);
                CollisionForces.ImpulseToVelocity(body, impulse, contactPoint,
                    out float3 linearChange, out float3 angularChange);

                ApplyFriction(body, SurfaceBody, vel, contactPoint,
                    out float3 frictionLinear, out float3 frictionAngular);

                body.Position += pushForce * DeltaTime * 10f;
                vel.Linear += linearChange + frictionLinear;
                vel.Angular += angularChange + frictionAngular;
                BodiesList[index] = body;
                PhysicsVelocities[index] = vel;
            }

            private static void ApplyFriction(
                in PhysicsBodyData body,
                in PhysicsBodyData surfaceBody,
                in PhysicsVelocityData vel,
                float3 contactPoint,
                out float3 frictionLinear,
                out float3 frictionAngular)
            {
                float3 bodyRv = CollisionForces.GetRadiusVector(body, contactPoint);
                float3 surfaceRv = CollisionForces.GetRadiusVector(surfaceBody, contactPoint);
                float3 delta = bodyRv - surfaceRv;
                float deltaLen = math.length(delta);

                if (deltaLen < 0.0001f)
                {
                    frictionLinear = float3.zero;
                    frictionAngular = float3.zero;
                    return;
                }

                float3 normal = delta / deltaLen;
                float normalComponent = math.dot(vel.Linear, normal);
                float3 tangentialVelocity = vel.Linear - normal * normalComponent;

                frictionLinear = -tangentialVelocity * body.Friction;
                frictionAngular = -vel.Angular * body.Friction;
            }
        }
    }
}
