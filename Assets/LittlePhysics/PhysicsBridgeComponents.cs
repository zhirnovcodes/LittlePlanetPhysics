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
        [NoAlias] public NativeCollisionMap DynamicCollisionMap;
        [NoAlias] public NativeCollisionMap TriggersCollisionMap;
        [NoAlias] public NativeCollisionMap StaticCollisionMap;
    }

    public struct CollisionItem
    {
        public Entity Target;
        public float3 ContactPoint;
    }

    public struct CollisionsSingleton
    {
        [NoAlias] public LittleHashMap<CollisionData> Collisions;
    }

    public enum ColliderType
    {
        Sphere,
        Capsule,
        SimplePlane
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
        public bool ShouldUpdateMap;
        public int LodIndex;

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
        public float3 Linear;
        public float3 Angular;

        public static PhysicsVelocityData operator +(PhysicsVelocityData a, PhysicsVelocityData b)
        {
            return new PhysicsVelocityData
            {
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular
            };
        }
    }

    public struct PhysicsSingleton : IComponentData
    {
        [NoAlias] public NativeArray<PhysicsBodyData> BodiesList;
        [NoAlias] public NativeArray<PhysicsVelocityData> PhysicsVelocities;
        [NoAlias] public NativeReference<uint> BodiesCount;
        public CollisionMapSingleton CollisionMap;
        public CollisionsSingleton Collisions;
        public SpacialMap SpacialMap;
        public JobHandle PhysicsJobHandle;
    }
}
