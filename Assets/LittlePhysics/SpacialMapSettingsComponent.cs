using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct SpacialMapSettingsComponent : IComponentData
    {
        public float3 Position;
        public float CellWidth;
        public int CellsWidth;
    }
}
