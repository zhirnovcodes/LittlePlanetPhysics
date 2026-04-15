using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsSettingsAuthoring : MonoBehaviour
    {
        public int MaxEntitiesCount = 1000000;
        public LodPhysicsData LodData;

        private sealed class Baker : Baker<PhysicsSettingsAuthoring>
        {
            public override void Bake(PhysicsSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PhysicsSettingsInitComponent
                {
                    MaxEntitiesCount = authoring.MaxEntitiesCount,
                    LodData = authoring.LodData,
                });
            }
        }
    }
}
