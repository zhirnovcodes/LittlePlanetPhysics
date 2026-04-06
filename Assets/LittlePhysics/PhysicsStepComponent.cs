using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct CameraData
    {
        public float3 CameraPosition;
        public float4x4 WorldToClipMatrix;
        public float2 ViewportSizeInPixels;
    }

    public struct PhysicsStepComponent : IComponentData
    {
        public CameraData Camera;
    }
}
