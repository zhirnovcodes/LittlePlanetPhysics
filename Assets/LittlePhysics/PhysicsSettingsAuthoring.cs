using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsSettingsAuthoring : MonoBehaviour
    {
        public int CellsWidth = 16;
        public int MaxEntitiesCount = 1000000;

        private sealed class Baker : Baker<PhysicsSettingsAuthoring>
        {
            public override void Bake(PhysicsSettingsAuthoring authoring)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<PhysicsSettingsBlobAsset>();
                root.CellsWidth = authoring.CellsWidth;
                root.MaxEntitiesCount = authoring.MaxEntitiesCount;

                var blobRef = builder.CreateBlobAssetReference<PhysicsSettingsBlobAsset>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddBlobAsset(ref blobRef, out _);
                AddComponent(entity, new PhysicsSettingsComponent { BlobRef = blobRef });
            }
        }
    }
}
