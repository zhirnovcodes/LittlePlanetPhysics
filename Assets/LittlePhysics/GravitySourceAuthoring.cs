using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class GravitySourceAuthoring : MonoBehaviour
    {
        public GravitySourceType SourceType;

        [Header("Spherical")]
        public float SurfaceGravity = 9.81f;
        public float Radius = 10f;

        [Header("Directional")]
        public Vector3 Direction = Vector3.down;
        public float Strength = 9.81f;

        private sealed class Baker : Baker<GravitySourceAuthoring>
        {
            public override void Bake(GravitySourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                switch (authoring.SourceType)
                {
                    case GravitySourceType.Spherical:
                        AddComponent(entity, new SphericalGravitySourceComponent
                        {
                            Center = authoring.transform.position,
                            SurfaceGravity = authoring.SurfaceGravity,
                            Radius = authoring.Radius
                        });
                        break;

                    case GravitySourceType.Directional:
                        AddComponent(entity, new DirectionalGravitySourceComponent
                        {
                            Direction = math.normalizesafe(authoring.Direction),
                            Strength = authoring.Strength
                        });
                        break;
                }
            }
        }
    }
}
