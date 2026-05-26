using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RocketPropulsion))]
    public class GuidedRocketProjectile : RocketProjectile
    {
        [Header("Guidance System")]
        [Tooltip("The maximum rate at which the rocket can turn (degrees per second).")]
        public float maxTurnRate = 90f;
        [Tooltip("If the target is destroyed or lost, should the rocket continue straight or explode?")]
        public bool detonateOnTargetLoss = false;

        public GameObject Target { get; private set; }
        private Rigidbody rb;
        private RocketPropulsion propulsion;

        protected override void Awake()
        {
            base.Awake();
            // We need local references as the base class fields might be private.
            rb = GetComponent<Rigidbody>();
            propulsion = GetComponent<RocketPropulsion>();
        }

        /// <summary>
        /// Specific initialization for guided rockets, setting the target.
        /// </summary>
        public void InitializeGuided(GameObject owner, Vector3 inheritedVelocity, GameObject target)
        {
            // Call the base initialization first
            Initialize(owner, inheritedVelocity);
            Target = target;
        }

        void FixedUpdate()
        {
            // Check if the target is still valid
            if (Target == null || !Target.activeInHierarchy)
            {
                HandleTargetLoss();
                return;
            }

            // Optional: Check if the target is dead (if it has a HealthComponent)
            if (Target.TryGetComponent(out HealthComponent health) && !health.IsAlive)
            {
                HandleTargetLoss();
                return;
            }


            // Only apply guidance if the propulsion system is active (ignited).
            // This relies on the modification to RocketPropulsion to expose its ignition status.
            if (propulsion != null && propulsion.IsIgnited)
            {
                ApplyGuidance();
            }
        }

        private void ApplyGuidance()
        {
            Vector3 directionToTarget = (Target.transform.position - transform.position).normalized;

            // Calculate the desired rotation
            Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget);

            // Calculate the rotation step based on maxTurnRate
            float step = maxTurnRate * Time.fixedDeltaTime;

            // Smoothly rotate towards the target (Pursuit behavior)
            Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, step);

            // Apply the rotation to the Rigidbody
            rb.MoveRotation(newRotation);

            // Ensure the velocity vector aligns with the new forward direction after turning,
            // maintaining the current speed. This prevents the rocket from skidding sideways.
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = transform.forward * rb.linearVelocity.magnitude;
            }
        }

        private void HandleTargetLoss()
        {
            if (Target == null && !detonateOnTargetLoss) return; // Already handled or detonation not required

            if (detonateOnTargetLoss)
            {
                // Explode at the current position
                // Passing transform.forward as a plausible impact normal, and null for the hit object.
                HandleImpact(transform.position, transform.forward, null);
            }

            Target = null; // Clear the target reference
            // If not detonating, the rocket continues flying straight (RocketPropulsion handles forward movement)
        }
    }
}