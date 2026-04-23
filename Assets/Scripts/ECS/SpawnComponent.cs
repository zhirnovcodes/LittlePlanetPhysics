using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct SpawnComponent : IComponentData
    {
        public Entity Prefab;
        public int MaxCount;
        public int2 SingleSpawnCount;   // x = min, y = max per batch
        public float2 SpawnIntervalSec; // x = min, y = max seconds between batches
        public float2 MassRange;        // x = min, y = max
        public float3 CenterPosition;
        public float3 Scale;            // full extents of the spawn AABB

        // Runtime state — managed by PhysicsObjectSpawnSystem
        public int CurrentCount;
        public float TimeUntilNextSpawn;
        public Random Rng;
    }
}
