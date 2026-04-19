using Unity.Burst;
using Unity.Entities;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BodiesCountSystem : ISystem
    {
        private EntityQuery BodiesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            BodiesQuery = SystemAPI.QueryBuilder().WithAll<PhysicsBodyComponent>().Build();
            state.RequireForUpdate<BodiesCountComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<BodiesCountComponent>();
            singleton.ValueRW.Count = BodiesQuery.CalculateEntityCount();
        }
    }
}
