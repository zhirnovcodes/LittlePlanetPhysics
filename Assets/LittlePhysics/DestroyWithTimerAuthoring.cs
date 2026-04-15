using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class DestroyWithTimerAuthoring : MonoBehaviour
    {
        public float DestroyTime = 1f;

        private sealed class Baker : Baker<DestroyWithTimerAuthoring>
        {
            public override void Bake(DestroyWithTimerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DestroyWithTimerComponent
                {
                    DestroyTime = authoring.DestroyTime,
                    TimeElapsed = 0f,
                });
            }
        }
    }
}
