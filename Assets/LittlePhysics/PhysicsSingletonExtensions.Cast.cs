using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct LineCastResult
    {
        public Entity Target;
        public float3 Contact;
    }

    public struct CastFilter
    {
        [System.Flags]
        public enum BodyTypes
        {
            None = 0,
            Dynamic = 1 << 0,
            Static = 1 << 1,
            Trigger = 1 << 2
        }

        public BodyTypes Types;

        public static CastFilter Default => new CastFilter { Types = BodyTypes.Dynamic };
    }

    public static partial class PhysicsSingletonExtensions
    {
        /// <summary>
        /// Performs a line cast and returns the first (closest) hit against bodies matching the filter.
        /// Each cell crossed by the line is queried for dynamic, then static, then trigger bodies.
        /// </summary>
        public static bool LineCastFirst(
            this PhysicsSingleton physics,
            float3 start,
            float3 direction,
            CastFilter filter,
            out LineCastResult result)
        {
            result = default;
            float closestDistSq = float.MaxValue;
            bool found = false;

            var line = new Line { Position = start, Direction = direction };
            var cellIterator = new TraverseLineIterator();

            while (physics.SpacialMap.TraverseLineNext(start, direction, ref cellIterator, out int cellId))
            {
                if ((filter.Types & CastFilter.BodyTypes.Dynamic) != 0 &&
                    physics.CollisionMap.DynamicMap.TryGetFirstValue((uint)cellId, out Entity dynEntity, out var dynIt))
                {
                    do
                    {
                        if (physics.Bodies.TryGetValue(dynEntity, out PhysicsBodyData body) &&
                            CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            float distSq = math.distancesq(start, contact);
                            if (distSq < closestDistSq)
                            {
                                closestDistSq = distSq;
                                result = new LineCastResult { Target = dynEntity, Contact = contact };
                                found = true;
                            }
                        }
                    }
                    while (physics.CollisionMap.DynamicMap.TryGetNextValue(out dynEntity, ref dynIt));
                }

                if ((filter.Types & CastFilter.BodyTypes.Static) != 0 &&
                    physics.CollisionMap.StaticMap.TryGetValue(cellId, out Entity staticEntity))
                {
                    if (physics.Bodies.TryGetValue(staticEntity, out PhysicsBodyData body) &&
                        CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                    {
                        float distSq = math.distancesq(start, contact);
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            result = new LineCastResult { Target = staticEntity, Contact = contact };
                            found = true;
                        }
                    }
                }

                if ((filter.Types & CastFilter.BodyTypes.Trigger) != 0 &&
                    physics.CollisionMap.TriggersMap.TryGetFirstValue((uint)cellId, out Entity trigEntity, out var trigIt))
                {
                    do
                    {
                        if (physics.Bodies.TryGetValue(trigEntity, out PhysicsBodyData body) &&
                            CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            float distSq = math.distancesq(start, contact);
                            if (distSq < closestDistSq)
                            {
                                closestDistSq = distSq;
                                result = new LineCastResult { Target = trigEntity, Contact = contact };
                                found = true;
                            }
                        }
                    }
                    while (physics.CollisionMap.TriggersMap.TryGetNextValue(out trigEntity, ref trigIt));
                }
            }

            return found;
        }

        /// <summary>
        /// Performs a line cast and fills <paramref name="results"/> with all hits against bodies
        /// matching the filter. Returns the number of hits written.
        /// Each cell crossed by the line is queried for dynamic, then static, then trigger bodies.
        /// </summary>
        public static int LineCast(
            this PhysicsSingleton physics,
            float3 start,
            float3 direction,
            CastFilter filter,
            ref NativeArray<LineCastResult> results)
        {
            int count = 0;
            var line = new Line { Position = start, Direction = direction };
            var cellIterator = new TraverseLineIterator();

            while (count < results.Length &&
                   physics.SpacialMap.TraverseLineNext(start, direction, ref cellIterator, out int cellId))
            {
                if ((filter.Types & CastFilter.BodyTypes.Dynamic) != 0 &&
                    physics.CollisionMap.DynamicMap.TryGetFirstValue((uint)cellId, out Entity dynEntity, out var dynIt))
                {
                    do
                    {
                        if (count < results.Length &&
                            physics.Bodies.TryGetValue(dynEntity, out PhysicsBodyData body) &&
                            CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            results[count++] = new LineCastResult { Target = dynEntity, Contact = contact };
                        }
                    }
                    while (physics.CollisionMap.DynamicMap.TryGetNextValue(out dynEntity, ref dynIt));
                }

                if (count < results.Length &&
                    (filter.Types & CastFilter.BodyTypes.Static) != 0 &&
                    physics.CollisionMap.StaticMap.TryGetValue(cellId, out Entity staticEntity))
                {
                    if (physics.Bodies.TryGetValue(staticEntity, out PhysicsBodyData body) &&
                        CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                    {
                        results[count++] = new LineCastResult { Target = staticEntity, Contact = contact };
                    }
                }

                if ((filter.Types & CastFilter.BodyTypes.Trigger) != 0 &&
                    physics.CollisionMap.TriggersMap.TryGetFirstValue((uint)cellId, out Entity trigEntity, out var trigIt))
                {
                    do
                    {
                        if (count < results.Length &&
                            physics.Bodies.TryGetValue(trigEntity, out PhysicsBodyData body) &&
                            CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            results[count++] = new LineCastResult { Target = trigEntity, Contact = contact };
                        }
                    }
                    while (physics.CollisionMap.TriggersMap.TryGetNextValue(out trigEntity, ref trigIt));
                }
            }

            SortLineCastResults(start, ref results, count); 

            return count;
        }

        /// <summary>
        /// Sorts the filled portion of <paramref name="results"/> (indices 0..<paramref name="count"/>)
        /// by ascending distance of the contact point from <paramref name="origin"/>.
        /// Uses an insertion sort — suitable for the typically small result sets produced by a line cast.
        /// </summary>
        private static void SortLineCastResults(float3 origin, ref NativeArray<LineCastResult> results, int count)
        {
            for (int i = 1; i < count; i++)
            {
                LineCastResult key = results[i];
                float keyDistSq = math.distancesq(origin, key.Contact);
                int j = i - 1;

                while (j >= 0 && math.distancesq(origin, results[j].Contact) > keyDistSq)
                {
                    results[j + 1] = results[j];
                    j--;
                }

                results[j + 1] = key;
            }
        }
    }
}
