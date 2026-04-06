using Unity.Burst;
using Unity.Mathematics;

namespace LittlePhysics
{
    [BurstCompile]
    public static class CollisionForces
    {
        /// <summary>
        /// Returns the radius vector from the body's effective rotation center to the contact point.
        /// Sphere: from sphere center to contact point.
        /// Capsule: from the nearest point on the capsule's core axis segment to the contact point
        ///          (perpendicular from axis to surface).
        /// </summary>
        private static float3 GetRadiusVector(in PhysicsBodyData body, float3 contactPoint)
        {
            if (body.ColliderType == ColliderType.Capsule)
            {
                float3 capA = body.Position - body.Up * 0.5f;
                float3 capB = body.Position + body.Up * 0.5f;
                float3 axisPoint = closestPointOnSegment(capA, capB, contactPoint);
                return contactPoint - axisPoint;
            }

            return contactPoint - body.Position;
        }

        /// <summary>
        /// Returns the delta vector from body1's effective center to body2's effective center,
        /// resolved via radius vectors so capsule axis points are used instead of geometric centers.
        /// Mathematically: GetRadiusVector(body1, cp) - GetRadiusVector(body2, cp) = ref2 - ref1.
        /// </summary>
        private static float3 GetDelta(in PhysicsBodyData body1, in PhysicsBodyData body2, float3 contactPoint)
        {
            return GetRadiusVector(body1, contactPoint) - GetRadiusVector(body2, contactPoint);
        }

        private static float3 closestPointOnSegment(float3 a, float3 b, float3 point)
        {
            float3 ab = b - a;
            float abLenSq = math.lengthsq(ab);
            if (abLenSq < 0.0001f)
                return a;
            float t = math.clamp(math.dot(point - a, ab) / abLenSq, 0f, 1f);
            return a + ab * t;
        }

        /// <summary>
        /// Calculates collision impulses for a pair of bodies at the given contact point.
        /// Static bodies and triggers receive zero impulse.
        /// Dynamic-vs-dynamic accounts for angular velocity and moment of inertia.
        /// Dynamic-vs-static treats the static body as immovable.
        /// </summary>
        public static void GetCollisionImpulses(
            in PhysicsBodyData body1,
            in PhysicsBodyData body2,
            in PhysicsVelocityData vel1,
            in PhysicsVelocityData vel2,
            float3 contactPoint,
            out float3 impulse1,
            out float3 impulse2)
        {
            impulse1 = float3.zero;
            impulse2 = float3.zero;

            bool body1Dynamic = body1.BodyType == BodyType.Dynamic;
            bool body2Dynamic = body2.BodyType == BodyType.Dynamic;

            if (!body1Dynamic && !body2Dynamic)
                return;

            float3 delta = GetDelta(body1, body2, contactPoint);
            float distSq = math.lengthsq(delta);
            if (distSq < 0.0001f)
                return;

            float distance = math.sqrt(distSq);
            float3 normal = delta / distance;

            if (body1Dynamic && body2Dynamic)
            {
                calculateDynamicVsDynamic(body1, body2, vel1, vel2, normal, contactPoint,
                    out impulse1, out impulse2);
            }
            else if (body1Dynamic)
            {
                calculateDynamicVsStatic(body1, body2, vel1, normal, out impulse1, out impulse2);
            }
            else
            {
                // body2 is dynamic, body1 is static — flip normal and swap outputs
                calculateDynamicVsStatic(body2, body1, vel2, -normal, out impulse2, out impulse1);
            }
        }

        /// <summary>
        /// Calculates push-out separation forces for a pair of penetrating bodies.
        /// Static and trigger bodies are not pushed. At least one body must be dynamic.
        /// Force magnitude is proportional to penetration depth and Hardness.
        /// </summary>
        public static void GetPushOutForce(
            in PhysicsBodyData body1,
            in PhysicsBodyData body2,
            float3 contactPoint,
            out float3 force1,
            out float3 force2)
        {
            force1 = float3.zero;
            force2 = float3.zero;

            if (body1.BodyType == BodyType.Trigger || body2.BodyType == BodyType.Trigger)
                return;

            bool body1Dynamic = body1.BodyType == BodyType.Dynamic;
            bool body2Dynamic = body2.BodyType == BodyType.Dynamic;

            if (!body1Dynamic && !body2Dynamic)
                return;

            if (body1.Hardness == 0f && body2.Hardness == 0f)
                return;

            float radius1 = body1.Scale * 0.5f;
            float radius2 = body2.Scale * 0.5f;

            float3 delta = GetDelta(body1, body2, contactPoint);
            float distance = math.length(delta);
            float penetration = (radius1 + radius2) - distance;

            if (penetration <= 0f)
                return;

            float3 normal = delta / math.max(distance, 0.0001f);

            const float MinPower = 0.02f;
            const float MaxPower = 0.5f;

            if (body1Dynamic)
                force1 = -normal * penetration * math.lerp(MinPower, MaxPower, body2.Hardness);

            if (body2Dynamic)
                force2 = normal * penetration * math.lerp(MinPower, MaxPower, body1.Hardness);
        }

        /// <summary>
        /// Converts an impulse applied at a contact point into linear and angular velocity changes.
        /// Uses sphere moment of inertia: I = (2/5) * m * r^2.
        /// </summary>
        public static void ImpulseToVelocity(
            in PhysicsBodyData body,
            float3 impulse,
            float3 contactPoint,
            out float3 linearVelocityChange,
            out float3 angularVelocityChange)
        {
            float mass = body.Mass;
            float radius = body.Scale * 0.5f;
            float inertia = 0.4f * mass * radius * radius;
            float3 radiusVector = GetRadiusVector(body, contactPoint);
            linearVelocityChange = impulse / mass;
            angularVelocityChange = math.cross(radiusVector, impulse) / inertia;
        }

        private static void calculateDynamicVsDynamic(
            in PhysicsBodyData body1,
            in PhysicsBodyData body2,
            in PhysicsVelocityData vel1,
            in PhysicsVelocityData vel2,
            float3 normal,
            float3 contactPoint,
            out float3 impulse1,
            out float3 impulse2)
        {
            impulse1 = float3.zero;
            impulse2 = float3.zero;

            float3 radiusI = GetRadiusVector(body1, contactPoint);
            float3 radiusJ = GetRadiusVector(body2, contactPoint);

            float3 velAtContactI = vel1.Linear + math.cross(vel1.Angular, radiusI);
            float3 velAtContactJ = vel2.Linear + math.cross(vel2.Angular, radiusJ);
            float relVelAlongNormal = math.dot(velAtContactJ - velAtContactI, normal);

            if (relVelAlongNormal >= 0f)
                return;

            float avgBounciness = (body1.Bounciness + body2.Bounciness) * 0.5f;

            float radius1 = body1.Scale * 0.5f;
            float radius2 = body2.Scale * 0.5f;

            // Moment of inertia for a solid sphere: I = (2/5) * m * r^2
            float inertiaI = 0.4f * body1.Mass * radius1 * radius1;
            float inertiaJ = 0.4f * body2.Mass * radius2 * radius2;

            float3 crossI = math.cross(radiusI, normal);
            float3 crossJ = math.cross(radiusJ, normal);

            float angularEffect = math.dot(crossI, crossI) / inertiaI
                                + math.dot(crossJ, crossJ) / inertiaJ;

            float impulseMag = -(1.0f + avgBounciness) * relVelAlongNormal
                               / (1.0f / body1.Mass + 1.0f / body2.Mass + angularEffect);

            float3 impulse = normal * impulseMag;
            impulse1 = -impulse;
            impulse2 = impulse;
        }

        private static void calculateDynamicVsStatic(
            in PhysicsBodyData dynBody,
            in PhysicsBodyData staticBody,
            in PhysicsVelocityData dynVel,
            float3 normal,
            out float3 dynImpulse,
            out float3 staticImpulse)
        {
            dynImpulse = float3.zero;
            staticImpulse = float3.zero;

            float relVelAlongNormal = math.dot(-dynVel.Linear, normal);

            if (relVelAlongNormal >= 0f)
                return;

            float avgBounciness = (dynBody.Bounciness + staticBody.Bounciness) * 0.5f;
            float impulseMag = -(1.0f + avgBounciness) * relVelAlongNormal;
            float3 impulse = normal * impulseMag * dynBody.Mass;

            dynImpulse = -impulse;
            staticImpulse = impulse;
        }
    }
}
