using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public struct CameraOffsetTriggerComponent : IComponentData
    {
        public float Offset;
        public Entity TriggerPrefab;
    }

    public sealed class CameraOffsetTriggerAuthoring : MonoBehaviour
    {
        public float Offset = 5f;
        public GameObject TriggerPrefab;

        private sealed class Baker : Baker<CameraOffsetTriggerAuthoring>
        {
            public override void Bake(CameraOffsetTriggerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CameraOffsetTriggerComponent
                {
                    Offset = authoring.Offset,
                    TriggerPrefab = authoring.TriggerPrefab != null
                        ? GetEntity(authoring.TriggerPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                });
            }
        }
    }
}
