using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class CollisionsDebugAuthoring : MonoBehaviour
    {
        public float UpdateTimeSec = 1f;
        public GameObject CellPrefab;
        public BodyType BodyToDebug = BodyType.Dynamic;

        private sealed class Baker : Baker<CollisionsDebugAuthoring>
        {
            public override void Bake(CollisionsDebugAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CollisionsDebugComponent
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
