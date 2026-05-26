using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Provides static utility methods for calculating projectile interception points.
    /// </summary>
    public static class InterceptionCalculator
    {
        /// <summary>
        /// Calculates the aim position required to intercept a moving target (First-order interception).
        /// Solves the quadratic equation for the time (t) to interception based on relative positions and velocities.
        /// This accounts for the movement of both the shooter and the target.
        /// </summary>
        /// <param name="shooterPosition">The current position of the shooter.</param>
        /// <param name="shooterVelocity">The current velocity vector of the shooter.</param>
        /// <param name="targetPosition">The current position of the target.</param>
        /// <param name="targetVelocity">The current velocity vector of the target.</param>
        /// <param name="projectileSpeed">The speed of the projectile being fired.</param>
        /// <returns>The world-space position where the interception will occur, or the target's current position if interception is impossible.</returns>
        public static Vector3 CalculateInterceptionPoint(
            Vector3 shooterPosition,
            Vector3 shooterVelocity,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float projectileSpeed)
        {
            // If projectile speed is negligible, aim directly at the target.
            if (projectileSpeed <= Mathf.Epsilon)
            {
                return targetPosition;
            }

            // Calculate relative velocity and position.
            Vector3 relativeVelocity = targetVelocity - shooterVelocity;
            Vector3 relativePosition = targetPosition - shooterPosition;

            // Set up the quadratic equation coefficients (a*t^2 + b*t + c = 0)
            // Derived from the principle that the distance traveled by the projectile must equal the distance traveled by the target to the interception point.
            // |P_rel + V_rel * t|^2 = (S_proj * t)^2
            float a = relativeVelocity.sqrMagnitude - (projectileSpeed * projectileSpeed);
            float b = 2f * Vector3.Dot(relativePosition, relativeVelocity);
            float c = relativePosition.sqrMagnitude;

            // Handle the case where 'a' is near zero (projectile speed is close to relative speed magnitude).
            // This avoids division by zero and requires solving a linear equation instead.
            if (Mathf.Abs(a) < 0.001f)
            {
                if (Mathf.Abs(b) > 0.001f)
                {
                    // Linear solution: t = -c / b
                    float t = -c / b;
                    if (t > 0)
                    {
                        // Calculate the interception point using the absolute target velocity.
                        // P_intercept = P_target_initial + V_target * t
                        return targetPosition + targetVelocity * t;
                    }
                }
                // If speeds are nearly identical and they are not converging (b near 0), or time is negative, fallback to direct aim.
                return targetPosition;
            }

            // Solve the quadratic equation using the discriminant (D = b^2 - 4ac)
            float discriminant = (b * b) - (4f * a * c);

            if (discriminant < 0f)
            {
                // No real solution exists (Discriminant is negative). Interception is impossible. Fallback to direct aim.
                return targetPosition;
            }

            // Calculate the two possible times (t1, t2)
            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b + sqrtDiscriminant) / (2f * a);
            float t2 = (-b - sqrtDiscriminant) / (2f * a);

            // Determine the correct time to use (the smallest positive time)
            float timeToIntercept;

            if (t1 > 0f && (t2 <= 0f || t1 < t2))
            {
                timeToIntercept = t1;
            }
            else if (t2 > 0f)
            {
                timeToIntercept = t2;
            }
            else
            {
                // Both times are non-positive (e.g., target is moving away faster than the projectile).
                return targetPosition;
            }

            // Calculate the interception point: P_intercept = P_target_initial + V_target * t
            Vector3 interceptionPoint = targetPosition + targetVelocity * timeToIntercept;

            return interceptionPoint;
        }
    }
}