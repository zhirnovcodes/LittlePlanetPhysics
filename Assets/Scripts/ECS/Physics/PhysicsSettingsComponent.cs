using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    [System.Serializable]
    public struct LodPhysicsData
    {
        public float2 DistanceRange;
        public int MaxEntityCount;

        public int MaxDynamicsInCells;
        public int MaxTriggersInCells;
        public int MaxStaticInCells;

        public int MaxCellPerEntity;
        public int MaxCellPerEntityX2;
        public int MaxCellPerEntityX4;

        public int MaxPairPerEntity;
        public int MaxPairPerEntityX2;
        public int MaxPairPerEntityX4;

        public int MaxCollisionsPerEntity;
        public int MaxCollisionsPerEntityX2;
        public int MaxCollisionsPerEntityX4;
    }

    public struct PhysicsSettingsBlobAsset
    {
        public int MaxEntitiesCount;
        public LodPhysicsData LodData;
        public BlobArray<int> LayersMaps;

        public int GetSumEntitiesXCells() => LodData.MaxDynamicsInCells * LodData.MaxCellPerEntity;
        public int GetSumEntitiesXCollisions() => LodData.MaxEntityCount * LodData.MaxCollisionsPerEntity;
        public int GetSumEntitiesXPairs() => LodData.MaxEntityCount * LodData.MaxPairPerEntity;
    }

    [System.Serializable]
    public struct CollisionCheckSettings
    {
        public bool CheckDynamicVsStatic;
        public bool CheckDynamicVsDynamic;
        public bool CheckTriggerVsDynamic;
        public bool CheckTriggerVsStatic;
    }

    public struct PhysicsSettingsComponent : IComponentData
    {
        public BlobAssetReference<PhysicsSettingsBlobAsset> BlobRef;
        public CollisionCheckSettings CheckSettings;
    }

    public struct PhysicsSettingsInitComponent : IComponentData
    {
        public int MaxEntitiesCount;
        public LodPhysicsData LodData;
        public CollisionCheckSettings CheckSettings;
    }
}
