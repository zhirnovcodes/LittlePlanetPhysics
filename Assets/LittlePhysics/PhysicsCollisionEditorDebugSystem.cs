using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsVelocitySystem))]
    [BurstCompile]
    public partial struct PhysicsCollisionEditorDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<PhysicsCollisionEditorDebugComponent>();
        }

    [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physics = SystemAPI.GetSingleton<PhysicsSingleton>();

            if (!physics.BodiesList.IsCreated)
            {
                return;
            }

            if (!physics.Collisions.Collisions.IsCreated)
            {
                return;
            }

            state.CompleteDependency();
            physics.PhysicsJobHandle.Complete();

            ref var debug = ref SystemAPI.GetSingletonRW<PhysicsCollisionEditorDebugComponent>().ValueRW;

            var collisions = physics.Collisions.Collisions;
            var bodiesList = physics.BodiesList;
            var body1Filter = debug.Body1Filter;
            var body2Filter = debug.Body2Filter;

            int count = 0;
            var iterator = collisions.GetIterator();

            while (collisions.Traverse(ref iterator, out var pair))
            {
                uint row = pair.Item1;
                CollisionData collision = pair.Item2;

                // Each collision is stored under both body indices; only count when
                // row matches the smaller index to avoid double-counting.
                if (row != math.min(collision.Body1, collision.Body2))
                {
                    continue;
                }

                if ((int)collision.Body1 >= bodiesList.Length || (int)collision.Body2 >= bodiesList.Length)
                {
                    continue;
                }

                var type1 = bodiesList[(int)collision.Body1].BodyType;
                var type2 = bodiesList[(int)collision.Body2].BodyType;

                bool matches = (type1 == body1Filter && type2 == body2Filter)
                            || (type1 == body2Filter && type2 == body1Filter);

                if (matches)
                {
                    count++;
                }
            }

            debug.CollisionCount += count;
        }
    }
}
