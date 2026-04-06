using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(ImportPhysicsDataSystem))]
    public partial struct PhysicsStepSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsStepComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var cam = Camera.main;
            var worldToClip = math.mul((float4x4)cam.projectionMatrix, (float4x4)cam.worldToCameraMatrix);
            var pos = (float3)cam.transform.position;

            SystemAPI.SetSingleton(new PhysicsStepComponent
            {
                Camera = new CameraData
                {
                    CameraPosition = pos,
                    WorldToClipMatrix = worldToClip,
                    ViewportSizeInPixels = new float2(cam.pixelWidth, cam.pixelHeight)
                }
            });
        }
    }
}
