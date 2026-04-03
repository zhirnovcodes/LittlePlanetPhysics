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

    public struct PhysicsBodyComponent : IComponentData
    {
        public BodyType BodyType;
        public StaticColliderType ColliderType;
        public float3 LocalPosition;
        public float Scale;
        public float Height;

        public DynamicPhysicsData ToDynamicData(LocalTransform transform) => new DynamicPhysicsData
        {
            Position = transform.Position + LocalPosition,
            RotationOffset = float3.zero,
            Scale = transform.Scale * Scale
        };

        public StaticPhysicsData ToStaticData(LocalTransform transform) => new StaticPhysicsData
        {
            ColliderType = ColliderType,
            Position = transform.Position + LocalPosition,
            Up = math.rotate(transform.Rotation, math.up()),
            Scale = transform.Scale * Scale,
            Height = Height
        };

        public TriggerPhysicsData ToTriggerData(LocalTransform transform) => new TriggerPhysicsData
        {
            Position = transform.Position + LocalPosition,
            Scale = transform.Scale * Scale
        };
    }
}
