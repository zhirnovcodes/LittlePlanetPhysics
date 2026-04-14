using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class SpacialMapSettingsAuthoring : MonoBehaviour
    {
        public Vector3 Position = Vector3.zero;
        public float CellWidth = 1f;
        public int3 GridSize = new int3(16, 16, 16);
        public uint RandomSeed = 12345;
        public bool ShouldDrawCells = false;

        private sealed class Baker : Baker<SpacialMapSettingsAuthoring>
        {
            public override void Bake(SpacialMapSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpacialMapSettingsComponent
                {
                    SpacialMap = new SpacialMap
                    {
                        Grid = new Grid3D
                        {
                            Position = authoring.Position,
                            CellSize = authoring.CellWidth
                        },
                        GridSize = authoring.GridSize
                    }
                });
                AddComponent(entity, new PhysicsMapRandomComponent
                {
                    Seed = authoring.RandomSeed
                });
            }
        }
    }
}
