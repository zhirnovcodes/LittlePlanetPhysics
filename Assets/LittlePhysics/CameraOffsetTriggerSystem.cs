using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsDataSystem))]
    public partial struct CameraOffsetTriggerSystem : ISystem
    {
        private Entity triggerEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraOffsetTriggerComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            spawnTrigger(ref state);

            if (triggerEntity == Entity.Null)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<CameraOffsetTriggerComponent>();
            var cameraTransform = camera.transform;
            var targetPosition = (float3)cameraTransform.position + (float3)cameraTransform.forward * config.Offset;

            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
            if (localTransformLookup.HasComponent(triggerEntity))
            {
                var lt = localTransformLookup[triggerEntity];
                lt.Position = targetPosition;
                localTransformLookup[triggerEntity] = lt;
            }
        }

        private void spawnTrigger(ref SystemState state)
        {
            if (triggerEntity != Entity.Null)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<CameraOffsetTriggerComponent>();
            if (config.TriggerPrefab == Entity.Null)
            {
                return;
            }

            triggerEntity = state.EntityManager.Instantiate(config.TriggerPrefab);
        }
    }
}
