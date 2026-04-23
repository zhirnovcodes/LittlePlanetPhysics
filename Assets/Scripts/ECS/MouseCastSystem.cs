using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsDataSystem))]
    public partial struct MouseCastSystem : ISystem
    {
        private Entity cursorEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<MouseCastComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var camera = Camera.main;
            if (camera == null)
                return;

            var physics = SystemAPI.GetSingleton<PhysicsSingleton>();
            physics.PhysicsJobHandle.Complete();

            SpawnCursor(ref state);

            if (cursorEntity == Entity.Null)
                return;

            var cast = SystemAPI.GetSingleton<MouseCastComponent>();
            var filter = new CastFilter { Types = cast.ColliderTypes };

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var ray = camera.ScreenPointToRay((Vector2)mouse.position.ReadValue());
            var hit = physics.LineCastFirst((float3)ray.origin, (float3)ray.direction * cast.Length, filter, out var result);

            var meshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            if (meshInfoLookup.HasComponent(cursorEntity))
                meshInfoLookup.SetComponentEnabled(cursorEntity, hit);

            if (hit)
            {
                var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
                if (localTransformLookup.HasComponent(cursorEntity))
                {
                    var lt = localTransformLookup[cursorEntity];
                    lt.Position = result.Contact;
                    localTransformLookup[cursorEntity] = lt;
                }
            }
        }

        private void SpawnCursor(ref SystemState state)
        {
            if (cursorEntity != Entity.Null)
                return;

            var cast = SystemAPI.GetSingleton<MouseCastComponent>();
            if (cast.CursorPrefab == Entity.Null)
                return;

            cursorEntity = state.EntityManager.Instantiate(cast.CursorPrefab);
            state.EntityManager.SetComponentEnabled<MaterialMeshInfo>(cursorEntity, false);
        }
    }
}
