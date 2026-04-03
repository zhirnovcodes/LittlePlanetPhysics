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

        public int MaxIntersectionsPerEntity;
        public int MaxIntersectionsPerEntityX2;
        public int MaxIntersectionsPerEntityX4;

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
