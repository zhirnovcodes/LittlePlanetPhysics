using Unity.Entities;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct CollisionVelocityCalculateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
