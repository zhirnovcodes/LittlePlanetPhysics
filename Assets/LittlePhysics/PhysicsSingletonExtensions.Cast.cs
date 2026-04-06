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

            var shouldFindDynamic = (filter.Types & CastFilter.BodyTypes.Dynamic) != 0;
            var shouldFindStatic = (filter.Types & CastFilter.BodyTypes.Static) != 0;
            var shouldFindTrigger = (filter.Types & CastFilter.BodyTypes.Trigger) != 0;

            var line = new Line { Position = start, Direction = direction };
            var cellIterator = new TraverseLineIterator();

            while (physics.SpacialMap.TraverseLineNext(start, direction, ref cellIterator, out int cellId))
            {
                if (shouldFindDynamic)
                {
                    var dynMap = physics.CollisionMap.DynamicCollisionMap;
                    var dynIt = dynMap.GetCellIterator((uint)cellId);
                    while (dynMap.TraverseCell(ref dynIt, out uint bodyIndex))
                    {
                        var body = physics.BodiesList[(int)bodyIndex];
                        Entity dynEntity = body.Main;
                        if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
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
                }

                if (shouldFindStatic)
                {
                    CheckStatic(ref physics, start, ref result, ref closestDistSq, ref found, line, cellId);
                }

                if (shouldFindTrigger)
                {
                    var trigMap = physics.CollisionMap.TriggersCollisionMap;
                    var trigIt = trigMap.GetCellIterator((uint)cellId);
                    while (trigMap.TraverseCell(ref trigIt, out uint bodyIndex))
                    {
                        var body = physics.BodiesList[(int)bodyIndex];
                        Entity trigEntity = body.Main;
                        if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
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
                }
            }

            return found;
        }

        private static void CheckStatic(ref PhysicsSingleton physics, float3 start, ref LineCastResult result, ref float closestDistSq, ref bool found, Line line, int cellId)
        {
            var staticMap = physics.CollisionMap.StaticCollisionMap;
            var it = staticMap.GetCellIterator((uint)cellId);
            while (staticMap.TraverseCell(ref it, out uint bodyIndex))
            {
                var body = physics.BodiesList[(int)bodyIndex];
                Entity staticEntity = body.Main;

                if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact) == false)
                {
                    continue;
                }

                float distSq = math.distancesq(start, contact);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    result = new LineCastResult { Target = staticEntity, Contact = contact };
                    found = true;
                }
            }
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
                if ((filter.Types & CastFilter.BodyTypes.Dynamic) != 0)
                {
                    var dynMap = physics.CollisionMap.DynamicCollisionMap;
                    var dynIt = dynMap.GetCellIterator((uint)cellId);
                    while (count < results.Length && dynMap.TraverseCell(ref dynIt, out uint bodyIndex))
                    {
                        var body = physics.BodiesList[(int)bodyIndex];
                        Entity dynEntity = body.Main;
                        if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            results[count++] = new LineCastResult { Target = dynEntity, Contact = contact };
                        }
                    }
                }

                if (count < results.Length &&
                    (filter.Types & CastFilter.BodyTypes.Static) != 0)
                {
                    var staticMap = physics.CollisionMap.StaticCollisionMap;
                    var staticIt = staticMap.GetCellIterator((uint)cellId);
                    while (count < results.Length && staticMap.TraverseCell(ref staticIt, out uint bodyIndex))
                    {
                        var body = physics.BodiesList[(int)bodyIndex];
                        Entity staticEntity = body.Main;
                        if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            results[count++] = new LineCastResult { Target = staticEntity, Contact = contact };
                        }
                    }
                }

                if ((filter.Types & CastFilter.BodyTypes.Trigger) != 0)
                {
                    var trigMap = physics.CollisionMap.TriggersCollisionMap;
                    var trigIt = trigMap.GetCellIterator((uint)cellId);
                    while (count < results.Length && trigMap.TraverseCell(ref trigIt, out uint bodyIndex))
                    {
                        var body = physics.BodiesList[(int)bodyIndex];
                        Entity trigEntity = body.Main;
                        if (CollisionMethods.IsLineCollidingBody(line, body, out float3 contact))
                        {
                            results[count++] = new LineCastResult { Target = trigEntity, Contact = contact };
                        }
                    }
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
