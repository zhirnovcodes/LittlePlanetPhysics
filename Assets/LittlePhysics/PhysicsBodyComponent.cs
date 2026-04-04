using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    public enum BodyType
    {
        Dynamic,
        Static,
        Trigger
    }

    public struct PhysicsBodyUpdateTag : IComponentData, IEnableableComponent
    {
    }

    public struct PhysicsBodyComponent : IComponentData
    {
        public BodyType BodyType;
        public ColliderType ColliderType;
        public float3 LocalPosition;
        public float3 RotationOffset;
        public float Scale;

        public PhysicsBodyData ToBodyData(Entity entity, LocalTransform transform) => new PhysicsBodyData
        {
            Main = entity,
            BodyType = BodyType,
            ColliderType = ColliderType,
            Position = transform.Position + LocalPosition,
            RotationOffset = RotationOffset,
            Scale = transform.Scale * Scale,
            Up = math.rotate(transform.Rotation, math.up())
        };
    }
}
