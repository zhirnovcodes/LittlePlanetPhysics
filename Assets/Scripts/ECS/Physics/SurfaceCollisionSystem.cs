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
        public NativeArray<SurfaceCollisionData> SurfaceCollisionMap;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<CollisionSurfaceComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SurfaceCollisionMap.IsCreated)
            {
                SurfaceCollisionMap.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();

            if (!SurfaceCollisionMap.IsCreated)
            {
                SurfaceCollisionMap = new NativeArray<SurfaceCollisionData>(settings.BlobRef.Value.LodData.MaxEntityCount, Allocator.Persistent);
            }

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

            var clearJob = new ClearSurfaceCollisionMapJob
            {
                SurfaceCollisionMap = SurfaceCollisionMap,
            }.Schedule(SurfaceCollisionMap.Length, 64, physicsHandle);

            var surfaceJob = new CheckDynamicVsSurfaceJob
            {
                SurfaceBody = SystemAPI.GetSingleton<CollisionSurfaceComponent>().ToBodyData(),
                BodiesList = physicsSingleton.BodiesList,
                BodiesCount = physicsSingleton.BodiesCount,
                PhysicsVelocities = physicsSingleton.PhysicsVelocities,
                PhysicsSettings = physicsSingleton.Settings,
                DeltaTime = time.DeltaTime,
                SurfaceCollisionMap = SurfaceCollisionMap,
            }.Schedule(physicsSingleton.BodiesList.Length, 32, clearJob);

            state.Dependency = surfaceJob;
            physicsSingleton.CollisionMap.SurfaceCollisionMap = SurfaceCollisionMap;
            physicsSingleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(physicsSingleton);
        }

        [BurstCompile]
        private struct ClearSurfaceCollisionMapJob : IJobParallelFor
        {
            public NativeArray<SurfaceCollisionData> SurfaceCollisionMap;

            public void Execute(int index)
            {
                SurfaceCollisionMap[index] = default;
            }
        }

        [BurstCompile]
        private struct CheckDynamicVsSurfaceJob : IJobParallelFor
        {
            public PhysicsBodyData SurfaceBody;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            [NativeDisableContainerSafetyRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            [NativeDisableContainerSafetyRestriction] public NativeArray<SurfaceCollisionData> SurfaceCollisionMap;
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

                SurfaceCollisionMap[index] = new SurfaceCollisionData { IsColliding = true, ContactPoint = contactPoint };

                var vel = PhysicsVelocities[index];

                CollisionForces.GetCollisionImpulses(body, SurfaceBody, vel, default, contactPoint,
                    out float3 impulse, out _);
                CollisionForces.GetPushOutForce(body, SurfaceBody, contactPoint,
                    out float3 pushForce, out _);
                CollisionForces.ImpulseToVelocity(body, impulse, contactPoint,
                    out float3 linearChange, out float3 angularChange);

                body.Position += pushForce * DeltaTime * 10f;
                vel.Linear += linearChange;
                vel.Angular += body.ShouldRotateOnCollision ? angularChange : float3.zero;
                BodiesList[index] = body;
                PhysicsVelocities[index] = vel;
            }
        }
    }
}
