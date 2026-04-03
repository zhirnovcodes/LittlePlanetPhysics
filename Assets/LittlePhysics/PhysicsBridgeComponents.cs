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
        public float Height;
    }
    public struct TriggerPhysicsData
    {
        public float3 Position;
        public float Scale;
    }

    public struct PhysicsSingleton : IComponentData
    {
        [NoAlias] public NativeParallelHashMap<int, DynamicPhysicsData> DynamicData;
        [NoAlias] public NativeParallelHashMap<int, StaticPhysicsData> StaticData;
        [NoAlias] public NativeParallelHashMap<int, TriggerPhysicsData> TriggerData;
    }
}
