using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public enum GravitySourceType
    {
        Spherical,
        Directional
    }

    public struct SphericalGravitySourceComponent : IComponentData
    {
        public float3 Center;
        public float SurfaceGravity;
        public float Radius;
    }

    public struct DirectionalGravitySourceComponent : IComponentData
    {
        public float3 Direction;
        public float Strength;
    }
}
