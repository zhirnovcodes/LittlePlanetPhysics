using Unity.Entities;
using UnityEngine;

namespace LittlePhysics
{
    public struct MouseCastComponent : IComponentData
    {
        public CastFilter.BodyTypes ColliderTypes;
        public Entity CursorPrefab;
        public float Length;
    }

    public sealed class MouseCastAuthoring : MonoBehaviour
    {
        public CastFilter.BodyTypes ColliderTypes = CastFilter.BodyTypes.Dynamic | CastFilter.BodyTypes.Static;
        public GameObject CursorPrefab;
        public float Length = 100f;

        private sealed class Baker : Baker<MouseCastAuthoring>
        {
            public override void Bake(MouseCastAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MouseCastComponent
                {
                    ColliderTypes = authoring.ColliderTypes,
                    CursorPrefab = authoring.CursorPrefab != null
                        ? GetEntity(authoring.CursorPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    Length = authoring.Length
                });
            }
        }
    }
}
