using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public enum StaticColliderType
    {
        Sphere,
        Capsule
    }

    public struct DynamicPhysicsData
    {
        public float3 Position;
        public float3 RotationOffset;
        public float Scale;
    }

    public struct StaticPhysicsData
    {
        public StaticColliderType ColliderType;
        public float3 Position;
        public float3 Up;
        public float Scale;
    }

    public struct TriggerPhysicsData
    {
        public float3 Position;
        public float Scale;
    }

    public struct PhysicsBodyData
    {
        public Entity Main;
        public StaticColliderType ColliderType;
        public BodyType BodyType;
        public float3 Position;
        public float3 RotationOffset;
        public float Scale;
        public float3 Up;

        public DynamicPhysicsData ToDynamicData() => new DynamicPhysicsData
        {
            Position = Position,
            RotationOffset = RotationOffset,
            Scale = Scale
        };

        public StaticPhysicsData ToStaticData() => new StaticPhysicsData
        {
            ColliderType = ColliderType,
            Position = Position,
            Up = Up,
            Scale = Scale
        };

        public TriggerPhysicsData ToTriggerData() => new TriggerPhysicsData
        {
            Position = Position,
            Scale = Scale
        };
    }

    public struct PhysicsSingleton : IComponentData
    {
        [NoAlias] public NativeList<PhysicsBodyData> Bodies;
    }
}
