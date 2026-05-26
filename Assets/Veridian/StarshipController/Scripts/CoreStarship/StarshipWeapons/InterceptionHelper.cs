using UnityEngine;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// Helper class for calculating interception points.
    /// </summary>
    public static class InterceptionHelper
    {
        /// <summary>
        /// Calculates the interception point for a projectile to hit a moving target using the Quadratic Intercept method.
        /// Solves for the future time 't' when the projectile will intercept the target.
        /// </summary>
        /// <param name="shooterPos">Position of the shooter.</param>
        /// <param name="shooterVel">Velocity of the shooter (e.g., for moving AI ships, zero for stationary turrets).</param>
        /// <param name="projectileSpeed">The constant speed of the projectile.</param>
        /// <param name="targetPos">Current position of the target.</param>
        /// <param name="targetVel">Current velocity of the target.</param>
        /// <param name="interceptPoint">The calculated point of interception.</param>
        /// <returns>True if interception is possible, false otherwise.</returns>
        public static bool CalculateQuadraticIntercept(
            Vector3 shooterPos,
            Vector3 shooterVel,
            float projectileSpeed,
            Vector3 targetPos,
            Vector3 targetVel,
            out Vector3 interceptPoint)
        {
            // Relative velocity and position
            Vector3 V_r = targetVel - shooterVel;
            Vector3 P_r = targetPos - shooterPos;

            // Solve the quadratic equation: a*t^2 + b*t + c = 0

            // a = V_r^2 - S_p^2
            float a = V_r.sqrMagnitude - (projectileSpeed * projectileSpeed);

            // b = 2 * P_r . V_r
            float b = 2f * Vector3.Dot(P_r, V_r);

            // c = P_r^2
            float c = P_r.sqrMagnitude;

            // Handle the case where a is near zero (speeds are similar) - solve linear equation
            if (Mathf.Abs(a) < 0.001f)
            {
                if (Mathf.Abs(b) < 0.001f)
                {
                    interceptPoint = Vector3.zero;
                    return false;
                }

                float t = -c / b;
                if (t < 0)
                {
                    interceptPoint = Vector3.zero;
                    return false;
                }

                interceptPoint = targetPos + targetVel * t;
                return true;
            }

            // Calculate the discriminant (b^2 - 4ac)
            float discriminant = (b * b) - (4f * a * c);

            if (discriminant < 0)
            {
                // No real solutions, interception is impossible
                interceptPoint = Vector3.zero;
                return false;
            }

            // Calculate the two possible times
            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b + sqrtDiscriminant) / (2f * a);
            float t2 = (-b - sqrtDiscriminant) / (2f * a);

            // Find the smallest positive time
            float timeToIntercept = -1f;

            if (t1 > 0 && (t2 < 0 || t1 < t2)) timeToIntercept = t1;
            else if (t2 > 0) timeToIntercept = t2;

            if (timeToIntercept < 0)
            {
                // Interception happened in the past
                interceptPoint = Vector3.zero;
                return false;
            }

            // Calculate the interception point: P_i = P_t + V_t * t
            interceptPoint = targetPos + targetVel * timeToIntercept;
            return true;
        }
    }
}