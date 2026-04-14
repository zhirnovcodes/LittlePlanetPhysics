using Unity.Burst;
using Unity.Mathematics;

namespace LittlePhysics
{
    public struct Sphere
    {
        public float3 Position;
        public float Scale; // diameter
    }

    public struct Line
    {
        public float3 Position;
        public float3 Direction;
    }

    public struct Capsule
    {
        public float3 Position;
        public float3 Up;   // direction of capsule axis * height
        public float Scale; // diameter (radius * 2)
    }

    public struct SimplePlane
    {
        public float Y;
    }

    [BurstCompile]
    public static class CollisionMethods
    {
        private const float CollisionEpsilon = 0.001f;
        private const float CollisionEpsilonSq = CollisionEpsilon * CollisionEpsilon;

        /// <summary>
        /// Returns true when two spheres overlap, with the contact point on the surface of sphere a.
        /// </summary>
        public static bool AreSpheresColliding(Sphere a, Sphere b, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float3 delta = b.Position - a.Position;
            float distSq = math.lengthsq(delta);
            float minDist = (a.Scale + b.Scale) * 0.5f;
            float minDistSq = minDist * minDist;

            bool colliding = distSq <= minDistSq + CollisionEpsilonSq && distSq > CollisionEpsilonSq;

            if (colliding)
            {
                float dist = math.length(delta);
                if (dist < 0.0001f)
                {
                    contactPoint = a.Position;
                }
                else
                {
                    float3 normal = delta / dist;
                    contactPoint = a.Position + normal * (a.Scale * 0.5f);
                }
            }

            return colliding;
        }

        /// <summary>
        /// Returns true when a sphere overlaps a capsule, with the contact point on the capsule surface.
        /// </summary>
        public static bool IsSphereCollidingCapsule(Sphere sphere, Capsule capsule, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float capsuleRadius = capsule.Scale * 0.5f;
            float sphereRadius = sphere.Scale * 0.5f;
            float combinedRadius = sphereRadius + capsuleRadius;

            float3 capA = capsule.Position - capsule.Up * 0.5f;
            float3 capB = capsule.Position + capsule.Up * 0.5f;

            float3 axisClosest = ClosestPointOnSegment(capA, capB, sphere.Position);
            float3 delta = sphere.Position - axisClosest;
            float distSq = math.lengthsq(delta);

            if (distSq > combinedRadius * combinedRadius + CollisionEpsilonSq)
                return false;

            float dist = math.sqrt(distSq);
            if (dist < 0.0001f)
            {
                contactPoint = axisClosest;
            }
            else
            {
                float3 normal = delta / dist;
                contactPoint = axisClosest + normal * capsuleRadius;
            }

            return true;
        }

        /// <summary>
        /// Returns true when a sphere overlaps a horizontal infinite plane at plane.Y, tested from above only.
        /// Contact point is on the plane surface directly below the sphere center.
        /// </summary>
        public static bool IsSphereCollidingSimplePlane(Sphere sphere, SimplePlane plane, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float radius = sphere.Scale * 0.5f;

            if (sphere.Position.y - radius > plane.Y + CollisionEpsilon)
                return false;

            contactPoint = new float3(sphere.Position.x, plane.Y, sphere.Position.z);
            return true;
        }

        /// <summary>
        /// Returns true when a ray intersects a sphere, with the contact point at the entry point on the sphere surface.
        /// The ray only tests in the forward direction of Line.Direction.
        /// </summary>
        public static bool IsLineCollidingSphere(Line line, Sphere sphere, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float radius = sphere.Scale * 0.5f;

            float dirLenSq = math.lengthsq(line.Direction);
            if (dirLenSq < 0.0001f)
                return false;

            float3 normalDir = line.Direction * math.rsqrt(dirLenSq);
            float3 toSphere = sphere.Position - line.Position;
            float proj = math.dot(toSphere, normalDir);

            if (proj < 0f)
                return false;

            float distSq = math.lengthsq(toSphere) - proj * proj;
            float radiusSq = radius * radius;

            if (distSq > radiusSq)
                return false;

            float halfChord = math.sqrt(radiusSq - distSq);
            float t = proj - halfChord;
            if (t < 0f) t = proj + halfChord;
            if (t < 0f) return false;

            contactPoint = line.Position + normalDir * t;
            return true;
        }

        /// <summary>
        /// Returns true when a ray intersects a capsule, with the contact point at the entry point on the capsule surface.
        /// The ray only tests in the forward direction of Line.Direction.
        /// </summary>
        public static bool IsLineCollidingCapsule(Line line, Capsule capsule, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float radius = capsule.Scale * 0.5f;
            float3 capA = capsule.Position - capsule.Up * 0.5f;
            float3 capB = capsule.Position + capsule.Up * 0.5f;
            float3 ab = capB - capA;
            float abLen = math.length(ab);

            if (abLen < 0.0001f)
                return IsLineCollidingSphere(line, new Sphere { Position = capsule.Position, Scale = capsule.Scale }, out contactPoint);

            float3 abDir = ab / abLen;

            float dirLenSq = math.lengthsq(line.Direction);
            if (dirLenSq < 0.0001f)
                return false;

            float3 normalDir = line.Direction * math.rsqrt(dirLenSq);

            float tMin = float.MaxValue;

            // Intersect with the infinite cylinder
            float3 dPerp = normalDir - math.dot(normalDir, abDir) * abDir;
            float3 ao = line.Position - capA;
            float3 aoPerp = ao - math.dot(ao, abDir) * abDir;

            float cylA = math.lengthsq(dPerp);
            if (cylA > 0.0001f)
            {
                float cylB = 2f * math.dot(dPerp, aoPerp);
                float cylC = math.lengthsq(aoPerp) - radius * radius;
                float disc = cylB * cylB - 4f * cylA * cylC;

                if (disc >= 0f)
                {
                    float sqrtDisc = math.sqrt(disc);
                    float t1 = (-cylB - sqrtDisc) / (2f * cylA);
                    float t2 = (-cylB + sqrtDisc) / (2f * cylA);

                    for (int i = 0; i < 2; i++)
                    {
                        float t = i == 0 ? t1 : t2;
                        if (t < 0f) continue;

                        float3 hitPoint = line.Position + normalDir * t;
                        float proj = math.dot(hitPoint - capA, abDir);
                        if (proj >= 0f && proj <= abLen)
                            tMin = math.min(tMin, t);
                    }
                }
            }

            // Intersect with the hemispherical end caps
            CheckCapSphere(line.Position, normalDir, capA, radius, ref tMin);
            CheckCapSphere(line.Position, normalDir, capB, radius, ref tMin);

            if (tMin <= float.MaxValue)
                return false;

            contactPoint = line.Position + normalDir * tMin;
            return true;
        }

        /// <summary>
        /// Returns true when a ray intersects a physics body (sphere or capsule), with the contact point at
        /// the entry point on the body surface. The ray only tests in the forward direction of Line.Direction.
        /// </summary>
        public static bool IsLineCollidingBody(Line line, PhysicsBodyData body, out float3 contactPoint)
        {
            if (body.ColliderType == ColliderType.Capsule)
                return IsLineCollidingCapsule(line, new Capsule { Position = body.Position, Up = body.Up, Scale = body.Scale }, out contactPoint);

            return IsLineCollidingSphere(line, new Sphere { Position = body.Position, Scale = body.Scale }, out contactPoint);
        }

        /// <summary>
        /// Returns true when two physics bodies overlap, with the contact point on the surface of body1 facing body2.
        /// Supports Sphere-Sphere, Sphere-Capsule, Capsule-Sphere, Capsule-Capsule, and Sphere-SimplePlane pairs.
        /// </summary>
        public static bool AreBodiesColliding(PhysicsBodyData body1, PhysicsBodyData body2, out float3 contactPoint)
        {
            if (body1.ColliderType == ColliderType.SimplePlane)
            {
                return IsSphereCollidingSimplePlane(
                    new Sphere { Position = body2.Position, Scale = body2.Scale },
                    new SimplePlane { Y = body1.Position.y },
                    out contactPoint);
            }

            if (body2.ColliderType == ColliderType.SimplePlane)
            {
                return IsSphereCollidingSimplePlane(
                    new Sphere { Position = body1.Position, Scale = body1.Scale },
                    new SimplePlane { Y = body2.Position.y },
                    out contactPoint);
            }

            bool b1Sphere = body1.ColliderType == ColliderType.Sphere;
            bool b2Sphere = body2.ColliderType == ColliderType.Sphere;

            if (b1Sphere && b2Sphere)
                return AreSpheresColliding(
                    new Sphere { Position = body1.Position, Scale = body1.Scale },
                    new Sphere { Position = body2.Position, Scale = body2.Scale },
                    out contactPoint);

            if (b1Sphere)
                return IsSphereCollidingCapsule(
                    new Sphere { Position = body1.Position, Scale = body1.Scale },
                    new Capsule { Position = body2.Position, Up = body2.Up, Scale = body2.Scale },
                    out contactPoint);

            if (b2Sphere)
                return IsSphereCollidingCapsule(
                    new Sphere { Position = body2.Position, Scale = body2.Scale },
                    new Capsule { Position = body1.Position, Up = body1.Up, Scale = body1.Scale },
                    out contactPoint);

            return AreCapsulesColliding(
                new Capsule { Position = body1.Position, Up = body1.Up, Scale = body1.Scale },
                new Capsule { Position = body2.Position, Up = body2.Up, Scale = body2.Scale },
                out contactPoint);
        }

        private static bool AreCapsulesColliding(Capsule a, Capsule b, out float3 contactPoint)
        {
            contactPoint = float3.zero;

            float radiusA = a.Scale * 0.5f;
            float radiusB = b.Scale * 0.5f;
            float combinedRadius = radiusA + radiusB;

            float3 a0 = a.Position - a.Up * 0.5f;
            float3 a1 = a.Position + a.Up * 0.5f;
            float3 b0 = b.Position - b.Up * 0.5f;
            float3 b1 = b.Position + b.Up * 0.5f;

            ClosestPointsBetweenSegments(a0, a1, b0, b1, out float3 closestA, out float3 closestB);

            float3 delta = closestB - closestA;
            float distSq = math.lengthsq(delta);

            if (distSq > combinedRadius * combinedRadius + CollisionEpsilonSq)
                return false;

            float dist = math.sqrt(distSq);
            float3 normal = dist < 0.0001f ? new float3(1f, 0f, 0f) : delta / dist;
            contactPoint = closestA + normal * radiusA;
            return true;
        }

        private static float3 ClosestPointOnSegment(float3 a, float3 b, float3 point)
        {
            float3 ab = b - a;
            float abLenSq = math.lengthsq(ab);
            if (abLenSq < 0.0001f)
                return a;

            float t = math.clamp(math.dot(point - a, ab) / abLenSq, 0f, 1f);
            return a + ab * t;
        }

        private static void ClosestPointsBetweenSegments(float3 p1, float3 p2, float3 p3, float3 p4, out float3 closest1, out float3 closest2)
        {
            float3 d1 = p2 - p1;
            float3 d2 = p4 - p3;
            float3 r = p1 - p3;

            float a = math.lengthsq(d1);
            float e = math.lengthsq(d2);
            float f = math.dot(d2, r);

            float s, t;

            if (a < 0.0001f && e < 0.0001f)
            {
                s = t = 0f;
            }
            else if (a < 0.0001f)
            {
                s = 0f;
                t = math.clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e < 0.0001f)
                {
                    t = 0f;
                    s = math.clamp(-c / a, 0f, 1f);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 0.0001f ? math.clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.clamp(-c / a, 0f, 1f);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.clamp((b - c) / a, 0f, 1f);
                    }
                }
            }

            closest1 = p1 + d1 * s;
            closest2 = p3 + d2 * t;
        }

        private static void CheckCapSphere(float3 rayOrigin, float3 rayDir, float3 center, float radius, ref float tMin)
        {
            float3 toCenter = center - rayOrigin;
            float proj = math.dot(toCenter, rayDir);
            if (proj < 0f) return;

            float distSq = math.lengthsq(toCenter) - proj * proj;
            float radiusSq = radius * radius;
            if (distSq > radiusSq) return;

            float halfChord = math.sqrt(radiusSq - distSq);
            float t = proj - halfChord;
            if (t < 0f) t = proj + halfChord;
            if (t >= 0f)
                tMin = math.min(tMin, t);
        }
    }
}
