using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct PhysicsVelocityComponent : IComponentData
    {
        public float3 LinearVelocity;
        public float3 AngularVelocity;

        public PhysicsVelocityData ToData() => new PhysicsVelocityData
        {
            LinearVelocity = LinearVelocity,
            AngularVelocity = AngularVelocity
        };
    }
}
