using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class TriggerAttractionAuthoring : MonoBehaviour
    {
        public float Power = 1f;

        private sealed class Baker : Baker<TriggerAttractionAuthoring>
        {
            public override void Bake(TriggerAttractionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TriggerAttractionComponent
                {
                    Power = authoring.Power
                });
            }
        }
    }
}
