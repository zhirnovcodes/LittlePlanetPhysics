using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public class BodiesCountAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<BodiesCountAuthoring>
        {
            public override void Bake(BodiesCountAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BodiesCountComponent
                {
                    Count = 0,
                });
            }
        }
    }
}
