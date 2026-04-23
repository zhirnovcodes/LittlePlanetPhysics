using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct PhysicsVelocityComponent : IComponentData
    {
        public float3 Linear;
        public float3 Angular;

        public PhysicsVelocityData ToVelocityData()
        {
            return new PhysicsVelocityData
            {
                Linear = Linear,
                Angular = Angular
            };
        }
    }
}
