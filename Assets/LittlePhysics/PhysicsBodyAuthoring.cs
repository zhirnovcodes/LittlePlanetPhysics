using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsBodyAuthoring : MonoBehaviour
    {
        public BodyType BodyType;
        public StaticColliderType ColliderType;
        public Vector3 LocalPosition;
        public float Scale = 1f;

        private sealed class Baker : Baker<PhysicsBodyAuthoring>
        {
            public override void Bake(PhysicsBodyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PhysicsBodyComponent
                {
                    BodyType = authoring.BodyType,
                    ColliderType = authoring.ColliderType,
                    LocalPosition = authoring.LocalPosition,
                    RotationOffset = float3.zero,
                    Scale = authoring.Scale
                });
            }
        }
    }
}
