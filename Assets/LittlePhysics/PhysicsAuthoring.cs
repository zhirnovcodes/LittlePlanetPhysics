using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<PhysicsAuthoring>
        {
            public override void Bake(PhysicsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PhysicsCreateTag>(entity);
            }
        }
    }
}
