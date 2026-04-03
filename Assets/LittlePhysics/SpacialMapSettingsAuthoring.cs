using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class SpacialMapSettingsAuthoring : MonoBehaviour
    {
        public Vector3 Position = Vector3.zero;
        public float CellWidth = 1f;
        public int CellsWidth = 16;

        private sealed class Baker : Baker<SpacialMapSettingsAuthoring>
        {
            public override void Bake(SpacialMapSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpacialMapSettingsComponent
                {
                    Position = authoring.Position,
                    CellWidth = authoring.CellWidth,
                    CellsWidth = authoring.CellsWidth,
                });
            }
        }
    }
}
