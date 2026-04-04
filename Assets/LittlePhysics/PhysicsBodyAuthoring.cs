using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsBodyAuthoring : MonoBehaviour
    {
        public BodyType BodyType;
        public ColliderType ColliderType;
        public Vector3 LocalPosition;
        public float Scale = 1f;
        public float Mass = 1f;
        public float Bounciness = 0.5f;
        public float Friction = 0.5f;
        public float Hardness = 0.5f;

        private sealed class Baker : Baker<PhysicsBodyAuthoring>
        {
            public override void Bake(PhysicsBodyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PhysicsBodyComponent
                {
                    BodyType = authoring.BodyType,
                    ColliderType = authoring.BodyType == BodyType.Static ? authoring.ColliderType : ColliderType.Sphere,
                    LocalPosition = authoring.LocalPosition,
                    RotationOffset = float3.zero,
                    Scale = authoring.Scale,
                    Mass = authoring.Mass,
                    Bounciness = authoring.Bounciness,
                    Friction = authoring.Friction,
                    Hardness = authoring.Hardness
                });

                AddComponent(entity, new PhysicsBodyUpdateTag());

                if (authoring.BodyType == BodyType.Dynamic)
                {
                    AddComponent(entity, new PhysicsVelocityComponent());
                }
            }
        }
    }
}
