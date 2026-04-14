using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public struct PhysicsCollisionEditorDebugComponent : IComponentData
    {
        public BodyType Body1Filter;
        public BodyType Body2Filter;
        public int CollisionCount;
    }

    public sealed class PhysicsCollisionEditorDebugAuthoring : MonoBehaviour
    {
        public BodyType Body1Filter = BodyType.Dynamic;
        public BodyType Body2Filter = BodyType.Dynamic;

        private sealed class Baker : Baker<PhysicsCollisionEditorDebugAuthoring>
        {
            public override void Bake(PhysicsCollisionEditorDebugAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PhysicsCollisionEditorDebugComponent
                {
                    Body1Filter = authoring.Body1Filter,
                    Body2Filter = authoring.Body2Filter,
                    CollisionCount = 0
                });
            }
        }
    }
}
