using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsSettingsAuthoring : MonoBehaviour
    {
        public int MaxEntitiesCount = 1000000;
        public float AirFriction = 0.5f;
        public float PushOutPower = 10f;
        public LodPhysicsData LodData;
        public CollisionCheckSettings CollisionCheckSettings = new CollisionCheckSettings
        {
            CheckDynamicVsStatic = true,
            CheckDynamicVsDynamic = true,
            CheckTriggerVsDynamic = true,
            CheckTriggerVsStatic = true,
        };

        private sealed class Baker : Baker<PhysicsSettingsAuthoring>
        {
            public override void Bake(PhysicsSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PhysicsSettingsInitComponent
                {
                    MaxEntitiesCount = authoring.MaxEntitiesCount,
                    LodData = authoring.LodData,
                    CheckSettings = authoring.CollisionCheckSettings,
                    EnvironmentSettings = new EnvironmentSettings
                    {
                        AirFriction = authoring.AirFriction,
                        PushOutPower = authoring.PushOutPower
                    },
                });
            }
        }
    }
}
