using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class CollisionMapDebugAuthoring : MonoBehaviour
    {
        public float UpdateTimeSec = 1f;
        public GameObject CellPrefab;
        public BodyType BodyToDebug = BodyType.Dynamic;

        private sealed class Baker : Baker<CollisionMapDebugAuthoring>
        {
            public override void Bake(CollisionMapDebugAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CollisionMapDebugComponent
                {
                    UpdateTimeSec = authoring.UpdateTimeSec,
                    CellPrefab = authoring.CellPrefab != null
                        ? GetEntity(authoring.CellPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    BodyToDebug = authoring.BodyToDebug
                });
            }
        }
    }
}
