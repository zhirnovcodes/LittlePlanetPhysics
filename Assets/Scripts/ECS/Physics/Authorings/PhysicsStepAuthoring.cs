using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public sealed class PhysicsStepAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<PhysicsStepAuthoring>
        {
            public override void Bake(PhysicsStepAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PhysicsStepComponent());
            }
        }
    }
}
