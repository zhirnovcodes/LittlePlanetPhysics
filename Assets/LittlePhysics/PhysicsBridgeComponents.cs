using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct BodiesPair : System.IEquatable<BodiesPair>
    {
        public Entity Entity1;
        public Entity Entity2;

        public bool Equals(BodiesPair other)
        {
            return Entity1.Equals(other.Entity1) && Entity2.Equals(other.Entity2);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(Entity1.Index ^ Entity2.Index));
        }
    }
    public struct CollisionMapSingleton
    {
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> DynamicMap;
        [NoAlias] public NativeParallelMultiHashMap<uint, Entity> TriggersMap;
        [NoAlias] public NativeParallelHashMap<uint, Entity> StaticMap;
    }

    public struct CollisionItem
    {
        public Entity Target;
        public float3 ContactPoint;
    }

    public struct CollisionsSingleton
    {
        [NoAlias] public NativeParallelMultiHashMap<Entity, CollisionItem> Collisions;
    }

    public enum ColliderType
    {
        Sphere,
        Capsule
    }

    public struct DynamicPhysicsData
    {
        public float3 Position;
        public float3 RotationOffset;
        public float Scale;

        public Sphere GetSphere() => new Sphere { Position = Position, Scale = Scale };
    }

    public struct StaticPhysicsData
    {
        public ColliderType ColliderType;
        public float3 Position;
        public float3 Up;
        public float Scale;

        public Sphere GetSphere() => new Sphere { Position = Position, Scale = Scale };
        public Capsule GetCapsule() => new Capsule { Position = Position, Up = Up, Scale = Scale };
    }

    public struct TriggerPhysicsData
    {
        public float3 Position;
        public float Scale;

        public Sphere GetSphere() => new Sphere { Position = Position, Scale = Scale };
    }

    public struct PhysicsBodyData
    {
        public Entity Main;
        public ColliderType ColliderType;
        public BodyType BodyType;
        public float3 Position;
        public float3 RotationOffset;
        public float Scale;
        public float3 Up;
        public float Mass;
        public float Bounciness;
        public float Friction;
        public float Hardness;
        public bool ShouldUpdate;

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

    public struct PhysicsVelocityData
    {
        public float3 LinearVelocity;
        public float3 AngularVelocity;
    }

    public struct PhysicsSingleton : IComponentData
    {
        [NoAlias] public NativeList<Entity> BodiesEntities;
        [NoAlias] public NativeParallelHashMap<Entity, PhysicsBodyData> Bodies;
        [NoAlias] public NativeList<PhysicsBodyData> BodiesList;
        public CollisionMapSingleton CollisionMap;
        public CollisionsSingleton Collisions;
        public SpacialMap SpacialMap;
        public JobHandle PhysicsJobHandle;
    }
}
