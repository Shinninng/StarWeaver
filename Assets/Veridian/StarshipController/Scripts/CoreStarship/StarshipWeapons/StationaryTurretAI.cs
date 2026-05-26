using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// A simple AI controller for a stationary turret.
    /// Handles target detection, rotation, line-of-sight checks, and predictive firing.
    /// </summary>
    [RequireComponent(typeof(ShipWeaponController))]
    public class StationaryTurretAI : MonoBehaviour
    {
        [Header("Targeting Configuration")]
        public LayerMask targetLayerMask;
        public float detectionRange = 500f;
        [Tooltip("Layers that block the turret's line of sight.")]
        public LayerMask obstructionLayerMask;

        [Header("Rotation (Optional)")]
        [Tooltip("The part of the turret that rotates (e.g., the cannon). If null, the entire object rotates.")]
        public Transform rotationPivot;
        public float rotationSpeed = 45f;
        [Tooltip("The angle threshold (in degrees) within which the turret considers itself aimed and can fire.")]
        public float firingAngleThreshold = 5f;

        private ShipWeaponController weaponController;
        private Transform currentTarget;
        private Rigidbody currentTargetRigidbody;
        private float nextCheckTime;
        private const float TARGET_CHECK_INTERVAL = 0.5f; // Optimize by checking targets every half second
        private Collider[] _overlapResults = new Collider[32];
        void Awake()
        {
            weaponController = GetComponent<ShipWeaponController>();
            if (rotationPivot == null)
            {
                rotationPivot = this.transform;
            }
        }

        void Update()
        {
            HandleTargetAcquisition();

            if (currentTarget != null)
            {
                Vector3 aimPosition = CalculateAimPosition();
                HandleRotation(aimPosition);
                HandleFiring(aimPosition);
            }
        }

        private void HandleTargetAcquisition()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + TARGET_CHECK_INTERVAL;

            // 1. Check if current target is still valid
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.position);
                // Check range, activity, and basic LOS
                if (distance > detectionRange || !currentTarget.gameObject.activeInHierarchy || !HasLineOfSight(currentTarget.position))
                {
                    ClearTarget();
                }
                // Check if target is destroyed
                else if (currentTarget.TryGetComponent(out HealthComponent health) && !health.IsAlive)
                {
                    ClearTarget();
                }
            }

            // 2. If no target, search for a new one
            if (currentTarget == null)
            {
                // Optimization: Use NonAlloc to find potential targets
                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, _overlapResults, targetLayerMask);
                float closestDistance = float.MaxValue;
                Transform closestTarget = null;

                // Loop only over the colliders that were actually found
                for (int i = 0; i < hitCount; i++)
                {
                    Collider hit = _overlapResults[i];

                    // Ensure the target is alive (look for HealthComponent in parent)
                    HealthComponent health = hit.GetComponentInParent<HealthComponent>();
                    if (health != null && health.IsAlive)
                    {
                        float distance = Vector3.Distance(transform.position, health.transform.position);
                        if (distance < closestDistance)
                        {
                            // Perform Line of Sight check
                            if (HasLineOfSight(health.transform.position))
                            {
                                closestDistance = distance;
                                closestTarget = health.transform; // Target the root object with the HealthComponent
                            }
                        }
                    }
                }

                if (closestTarget != null)
                {
                    SetTarget(closestTarget);
                }

                // Optional: Clear the part of the array we used
                System.Array.Clear(_overlapResults, 0, hitCount);
            }
        }

        private void SetTarget(Transform target)
        {
            currentTarget = target;
            currentTargetRigidbody = target.GetComponent<Rigidbody>();
        }

        private void ClearTarget()
        {
            currentTarget = null;
            currentTargetRigidbody = null;
        }

        private bool HasLineOfSight(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            float distance = direction.magnitude;

            // Raycast against obstructions
            if (Physics.Raycast(transform.position, direction.normalized, distance, obstructionLayerMask))
            {
                return false;
            }
            return true;
        }

        private Vector3 CalculateAimPosition()
        {
            // Turrets primarily use their primary weapon configuration for AI logic.
            if (weaponController.primaryWeaponStats == null) return currentTarget.position;

            WeaponStats stats = weaponController.primaryWeaponStats;

            // If the target has no rigidbody or the weapon is effectively instantaneous (very high speed lasers), aim directly.
            if (currentTargetRigidbody == null || stats.projectileSpeed > 5000f || stats.projectileSpeed <= 0)
            {
                return currentTarget.position;
            }

            // Advanced Interception (Quadratic Intercept)
            bool canIntercept = InterceptionHelper.CalculateQuadraticIntercept(
                transform.position,
                Vector3.zero, // Turret is stationary
                stats.projectileSpeed,
                currentTarget.position,
                currentTargetRigidbody.linearVelocity,
                out Vector3 interceptPoint);

            // If interception fails (e.g., target is moving too fast), aim directly at the current position as a fallback.
            return canIntercept ? interceptPoint : currentTarget.position;
        }

        private void HandleRotation(Vector3 aimPosition)
        {
            Vector3 desiredDirection = (aimPosition - rotationPivot.position).normalized;

            // Optional: Constrain rotation if the turret shouldn't tilt vertically
            // desiredDirection.y = 0; 

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection);
                rotationPivot.rotation = Quaternion.RotateTowards(rotationPivot.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void HandleFiring(Vector3 aimPosition)
        {
            // Check if the turret is aimed closely enough
            Vector3 currentDirection = rotationPivot.forward;
            Vector3 desiredDirection = (aimPosition - rotationPivot.position).normalized;

            float angle = Vector3.Angle(currentDirection, desiredDirection);

            if (angle <= firingAngleThreshold)
            {
                // Final LOS check before firing (in case the target moved behind cover since the last check)
                if (HasLineOfSight(currentTarget.position))
                {
                    // Fire the primary weapon at the calculated aim position
                    weaponController.FirePrimary(aimPosition);
                }
                else
                {
                    ClearTarget(); // Lost LOS, force acquisition of a new target immediately
                    nextCheckTime = Time.time;
                }
            }
        }
    }
}