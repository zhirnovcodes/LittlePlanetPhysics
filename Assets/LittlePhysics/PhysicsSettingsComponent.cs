using Unity.Entities;

namespace LittlePhysics
{
    [System.Serializable]
    public struct LodPhysicsData
    {
        public int MaxEntityCount;
        public int MaxEntitiesInCell;

        public int MaxCollisionsPerEntity;
        public int MaxCollisionsPerEntityX2;
        public int MaxCollisionsPerEntityX4;

        public int MaxCellPerEntity;
        public int MaxCellPerEntityX2;
        public int MaxCellPerEntityX4;

        public int MaxPairPerEntity;
        public int MaxPairPerEntityX2;
        public int MaxPairPerEntityX4;

    }

    public struct PhysicsSettingsBlobAsset
    {
        public int MaxEntitiesCount;
        public LodPhysicsData LodData;

        public int GetMaxEntitiesInCell() => LodData.MaxEntitiesInCell;
        public int GetSumEntitiesXCells() => LodData.MaxEntitiesInCell * LodData.MaxCellPerEntity;
        public int GetSumEntitiesXCollisions() => LodData.MaxEntityCount * LodData.MaxCollisionsPerEntity;
        public int GetSumEntitiesXPairs() => LodData.MaxEntityCount * LodData.MaxPairPerEntity;
    }

    public struct PhysicsSettingsComponent : IComponentData
    {
        public BlobAssetReference<PhysicsSettingsBlobAsset> BlobRef;
    }
}
