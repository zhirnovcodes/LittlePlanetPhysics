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
    [UpdateAfter(typeof(PhysicsVelocitySystem))]
    public partial struct TriggerAttractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<TriggerAttractionComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();
            if (!singleton.BodiesList.IsCreated || !singleton.Collisions.Collisions.IsCreated)
            {
                return;
            }

            var attraction = SystemAPI.GetSingleton<TriggerAttractionComponent>();
            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            int bodyCount = settings.BlobRef.Value.LodData.MaxEntityCount;

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);
            
            state.Dependency = new TriggerAttractionJob
            {
                Collisions = singleton.Collisions.Collisions,
                BodiesList = singleton.BodiesList,
                PhysicsVelocities = singleton.PhysicsVelocities,
                BodiesCount = singleton.BodiesCount,
                Power = attraction.Power,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule(bodyCount, 32, combinedDep);

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }

        [BurstCompile]
        private struct TriggerAttractionJob : IJobParallelFor
        {
            [ReadOnly] public LittleHashMap<CollisionData> Collisions;
            [ReadOnly] public NativeArray<PhysicsBodyData> BodiesList;
            [ReadOnly] public NativeReference<uint> BodiesCount;
            [NativeDisableParallelForRestriction] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
            public float Power;
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

                var iterator = Collisions.GetSingleIterator(index);
                while (Collisions.Traverse(ref iterator, out var pair))
                {
                    var collision = pair.Item2;

                    uint otherIndex = (uint)index == collision.Body1 ? collision.Body2 : collision.Body1;
                    var otherBody = BodiesList[(int)otherIndex];

                    if (otherBody.BodyType != BodyType.Trigger)
                    {
                        continue;
                    }

                    float3 toTrigger = otherBody.Position - body.Position;
                    float distance = math.length(toTrigger);

                    if (distance < 0.001f)
                    {
                        continue;
                    }

                    float3 direction = toTrigger / distance;

                    var velocity = PhysicsVelocities[index];
                    velocity.Linear += direction * Power;
                    PhysicsVelocities[index] = velocity;
                }
            }
        }
    }
}
