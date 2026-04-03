using Unity.Entities;

namespace LittlePhysics
{
    public struct PhysicsSettingsBlobAsset
    {
        public int CellsWidth;
        public int MaxEntitiesCount;

        public int GetCellsCount() => CellsWidth * CellsWidth * CellsWidth;
    }

    public struct PhysicsSettingsComponent : IComponentData
    {
        public BlobAssetReference<PhysicsSettingsBlobAsset> BlobRef;

        public PhysicsSettingsComponent Clone() => new PhysicsSettingsComponent { BlobRef = BlobRef };
    }
}
