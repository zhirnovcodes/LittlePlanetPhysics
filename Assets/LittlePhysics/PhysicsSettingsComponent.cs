using Unity.Entities;

namespace LittlePhysics
{
    [System.Serializable]
    public struct LodPhysicsData
    {
        public int MaxEntityCount = 100000;
        public int MaxEntitiesInCell = 16;

        public int MaxCollisionsPerEntity = 16;
        public int MaxCollisionsPerEntityX2 = 8;
        public int MaxCollisionsPerEntityX4 = 4;

        public int MaxCellPerEntity = 32;
        public int MaxCellPerEntityX2 = 16;
        public int MaxCellPerEntityX4 = 8;

        public int MaxIntersectionsPerEntity = 32;
        public int MaxIntersectionsPerEntityX2 = 16;
        public int MaxIntersectionsPerEntityX4 = 8;

    }

    public struct PhysicsSettingsBlobAsset
    {
        public int MaxEntitiesCount;
        public LodPhysicsData LodData;

        public int GetMaxEntitiesInCell() => LodData.MaxEntitiesInCell;
        public int GetSumEntitiesXCells() => LodData.MaxEntitiesInCell * LodData.MaxCellPerEntity;
        public int GetSumEntitiesXCollisions() => LodData.MaxEntityCount * LodData.MaxCollisionsPerEntity;
        public int GetSumEntitiesXIntersections() => LodData.MaxEntityCount * LodData.MaxIntersectionsPerEntity;
    }

    public struct PhysicsSettingsComponent : IComponentData
    {
        public BlobAssetReference<PhysicsSettingsBlobAsset> BlobRef;
    }
}
