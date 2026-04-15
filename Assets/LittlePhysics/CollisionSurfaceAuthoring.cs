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
                        Scale = authoring.transform.localScale.x
                    },
                    Bounciness = authoring.Bounciness,
                    Hardness = authoring.Hardness,
                    Layer = authoring.gameObject.layer,
                });
            }
        }
    }
}
