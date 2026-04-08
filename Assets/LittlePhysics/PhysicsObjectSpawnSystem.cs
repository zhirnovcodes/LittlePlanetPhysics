using Unity.Burst;
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

        public void OnCreate(ref SystemState state)
        {
            physicsBodyLookup = state.GetComponentLookup<PhysicsBodyComponent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            physicsBodyLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var spawner in SystemAPI.Query<RefRW<SpawnComponent>>())
            {
                ref var spawn = ref spawner.ValueRW;

                if (spawn.CurrentCount >= spawn.MaxCount)
                {
                    state.Enabled = false;
                    continue; 
                }

                spawn.TimeUntilNextSpawn -= deltaTime;

                if (spawn.TimeUntilNextSpawn > 0f)
                    continue;

                var batchCount = spawn.Rng.NextInt(spawn.SingleSpawnCount.x, spawn.SingleSpawnCount.y + 1);
                batchCount = math.min(batchCount, spawn.MaxCount - spawn.CurrentCount);

                var halfScale = spawn.Scale * 0.5f;

                for (int i = 0; i < batchCount; i++)
                {
                    var instance = ecb.Instantiate(spawn.Prefab);

                    var position = spawn.CenterPosition + new float3(
                        spawn.Rng.NextFloat(-halfScale.x, halfScale.x),
                        spawn.Rng.NextFloat(-halfScale.y, halfScale.y),
                        spawn.Rng.NextFloat(-halfScale.z, halfScale.z)
                    );

                    ecb.SetComponent(instance, LocalTransform.FromPosition(position));

                }

                spawn.CurrentCount += batchCount;
                spawn.TimeUntilNextSpawn = spawn.Rng.NextFloat(spawn.SpawnIntervalSec.x, spawn.SpawnIntervalSec.y);
            }
        }
    }
}
