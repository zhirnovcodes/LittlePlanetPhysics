using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DestroyWithTimerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (timer, entity) in SystemAPI.Query<RefRW<DestroyWithTimerComponent>>().WithEntityAccess())
            {
                timer.ValueRW.TimeElapsed += deltaTime;

                if (timer.ValueRO.TimeElapsed >= timer.ValueRO.DestroyTime)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
