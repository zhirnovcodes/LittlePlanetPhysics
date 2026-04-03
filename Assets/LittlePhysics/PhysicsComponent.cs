using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct PhysicsComponent : IComponentData
    {
        public Entity MainEntity;
        public float3 Position;
        public float3 RotationOffset;
        public float Scale;
    }

    public struct PhysicsCreateTag : IComponentData, IEnableableComponent
    {
    }
}
