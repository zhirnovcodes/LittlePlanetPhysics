using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PhysicsObjectSpawnSystem : ISystem
    {
        private ComponentLookup<PhysicsBodyComponent> physicsBodyLookup;
        private EntityQuery physicsBodyQuery;

        public void OnCreate(ref SystemState state)
        {
            physicsBodyLookup = state.GetComponentLookup<PhysicsBodyComponent>(true);
            physicsBodyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsBodyComponent>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            physicsBodyLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (SystemAPI.TryGetSingleton<LittlePhysicsTimeComponent>(out var time) == false)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime * time.TimeScale;
            var currentPhysicsBodyCount = physicsBodyQuery.CalculateEntityCount();

            if (SystemAPI.HasSingleton<BodiesCountComponent>())
            {
                SystemAPI.GetSingletonRW<BodiesCountComponent>().ValueRW.Count = currentPhysicsBodyCount;
            }

            foreach (var spawner in SystemAPI.Query<RefRW<SpawnComponent>>())
            {
                ref var spawn = ref spawner.ValueRW;

                if (currentPhysicsBodyCount >= spawn.MaxCount)
                {
                    state.Enabled = false;
                    continue;
                }

                spawn.TimeUntilNextSpawn -= deltaTime;

                if (spawn.TimeUntilNextSpawn > 0f)
                {
                    continue;
                }

                var batchCount = spawn.Rng.NextInt(spawn.SingleSpawnCount.x, spawn.SingleSpawnCount.y + 1);
                batchCount = math.min(batchCount, spawn.MaxCount - currentPhysicsBodyCount);

                var halfScale = spawn.Scale * 0.5f;

                for (int i = 0; i < batchCount; i++)
                {
                    var instance = ecb.Instantiate(spawn.Prefab);

                    var position = spawn.CenterPosition + new float3(
                        spawn.Rng.NextFloat(-halfScale.x, halfScale.x),
                        spawn.Rng.NextFloat(-halfScale.y, halfScale.y),
                        spawn.Rng.NextFloat(-halfScale.z, halfScale.z)
                    );

                    var scale = spawn.Rng.NextFloat(spawn.ScaleRange.x, spawn.ScaleRange.y);
                    ecb.SetComponent(instance, LocalTransform.FromPositionRotationScale(position, quaternion.identity, scale));

                }

                spawn.TimeUntilNextSpawn = spawn.Rng.NextFloat(spawn.SpawnIntervalSec.x, spawn.SpawnIntervalSec.y);
            }
        }
    }
}
