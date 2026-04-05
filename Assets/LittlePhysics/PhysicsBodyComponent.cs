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

    public enum UpdateType
    {
        EveryFrame,
        Once,
        WithInterval
    }

    public struct PhysicsBodyUpdateComponent : IComponentData, IEnableableComponent
    {
        public UpdateType Type;
        public float Interval;
        public bool WasUpdated;
        public float TimeElapsed;
        public int Index;
    }

    public struct PhysicsBodyComponent : IComponentData
    {
        public BodyType BodyType;
        public ColliderType ColliderType;
        public float3 LocalPosition;
        public float3 RotationOffset;
        public float Scale;
        public float Mass;
        public float Bounciness;
        public float Friction;
        public float Hardness;

        public PhysicsBodyData ToBodyData(Entity entity, LocalTransform transform, bool shouldUpdate = true) => new PhysicsBodyData
        {
            Main = entity,
            BodyType = BodyType,
            ColliderType = ColliderType,
            Position = transform.Position + LocalPosition,
            RotationOffset = RotationOffset,
            Scale = transform.Scale * Scale,
            Up = math.rotate(transform.Rotation, math.up()),
            Mass = Mass,
            Bounciness = Bounciness,
            Friction = Friction,
            Hardness = Hardness,
            ShouldUpdate = shouldUpdate
        };
    }
}
