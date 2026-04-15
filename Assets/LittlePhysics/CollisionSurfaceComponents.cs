using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public enum SurfaceType
    {
        SimplePlane,
        Sphere
    }

    public struct CollisionSurfaceComponent : IComponentData
    {
        public SurfaceType SurfaceType;
        public SimplePlane Plane;
        public Sphere Sphere;
        public float Bounciness;
        public float Hardness;
        public int Layer;

        public PhysicsBodyData ToBodyData()
        {
            return new PhysicsBodyData
            {
                BodyType = BodyType.Static,
                ColliderType = SurfaceType == SurfaceType.SimplePlane
                    ? ColliderType.SimplePlane
                    : ColliderType.Sphere,
                Position = SurfaceType == SurfaceType.SimplePlane
                    ? new float3(0f, Plane.Y, 0f)
                    : Sphere.Position,
                Scale = SurfaceType == SurfaceType.SimplePlane ? 0f : Sphere.Scale,
                Bounciness = Bounciness,
                Hardness = Hardness,
                Layer = Layer,
            };
        }
    }
}
