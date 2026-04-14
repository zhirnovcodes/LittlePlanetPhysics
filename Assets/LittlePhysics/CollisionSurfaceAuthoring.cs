using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class CollisionSurfaceAuthoring : MonoBehaviour
    {
        public SurfaceType SurfaceType;

        [Header("Simple Plane")]
        public float PlaneY;

        [Header("Sphere")]
        public float SphereRadius = 10f;

        [Header("Material")]
        public float Bounciness = 0.5f;
        public float Hardness = 0.5f;

        private sealed class Baker : Baker<CollisionSurfaceAuthoring>
        {
            public override void Bake(CollisionSurfaceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CollisionSurfaceComponent
                {
                    SurfaceType = authoring.SurfaceType,
                    Plane = new SimplePlane { Y = authoring.PlaneY },
                    Sphere = new Sphere
                    {
                        Position = authoring.transform.position,
                        Scale = authoring.SphereRadius * 2f
                    },
                    Bounciness = authoring.Bounciness,
                    Hardness = authoring.Hardness,
                });
            }
        }
    }
}
