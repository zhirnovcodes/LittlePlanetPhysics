using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class SpawnAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public int MaxCount = 100;
        public Vector2Int SingleSpawnCount = new Vector2Int(1, 3);
        public Vector2 SpawnIntervalSec = new Vector2(1f, 2f);
        public Vector2 MassRange = new Vector2(0.5f, 2f);
        //public Vector3 CenterPosition = Vector3.zero;
        //public Vector3 Scale = Vector3.one * 10f;
        public uint RandomSeed = 100;

        private sealed class Baker : Baker<SpawnAuthoring>
        {
            public override void Bake(SpawnAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpawnComponent
                {
                    Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                    MaxCount = authoring.MaxCount,
                    SingleSpawnCount = new int2(authoring.SingleSpawnCount.x, authoring.SingleSpawnCount.y),
                    SpawnIntervalSec = new float2(authoring.SpawnIntervalSec.x, authoring.SpawnIntervalSec.y),
                    MassRange = new float2(authoring.MassRange.x, authoring.MassRange.y),
                    CenterPosition = authoring.transform.position,
                    Scale = authoring.transform.localScale,
                    CurrentCount = 0,
                    TimeUntilNextSpawn = 0f,
                    Rng = Unity.Mathematics.Random.CreateFromIndex(authoring.RandomSeed)
                });
            }
        }
    }
}
