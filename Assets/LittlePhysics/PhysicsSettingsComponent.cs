using Unity.Entities;

namespace LittlePhysics
{
    public struct PhysicsSettingsBlobAsset
    {
        public int MaxEntitiesCount;
    }

    public struct PhysicsSettingsComponent : IComponentData
    {
        public BlobAssetReference<PhysicsSettingsBlobAsset> BlobRef;

        public PhysicsSettingsComponent Clone() => new PhysicsSettingsComponent { BlobRef = BlobRef };
    }
}
