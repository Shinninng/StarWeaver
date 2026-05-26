using UnityEngine;

namespace Veridian.Starship.Weapons
{
    // ... (Class definition and attributes)
    public class RocketPropulsion : MonoBehaviour
    {
        [Tooltip("The constant force applied in the forward direction.")]
        public float thrustForce = 5000f;

        [Tooltip("The maximum speed the rocket can reach.")]
        public float maxSpeed = 3000f;

        [Tooltip("Optional delay before the engine ignites.")]
        public float ignitionDelay = 0.1f;

        private Rigidbody rb;
        private float startTime;

        // MODIFIED: Changed from private bool to public property
        public bool IsIgnited { get; private set; } = false;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            // Ensure the rocket is not affected by gravity
            rb.useGravity = false;
        }

        void Start()
        {
            startTime = Time.time;
        }

        void FixedUpdate()
        {
            // --- Ignition Check ---
            if (!IsIgnited)
            {
                if (Time.time - startTime >= ignitionDelay)
                {
                    IsIgnited = true;
                    // Optional: You could add a small impulse force here on ignition if desired
                    // rb.AddForce(transform.forward * initialBoostForce, ForceMode.Impulse);
                }
                else
                {
                    // If not ignited yet, do nothing else this frame.
                    // The Rigidbody continues moving based on the velocity set in OnInitialized.
                    return;
                }
            }

            // --- Apply Thrust ---
            // Only apply thrust if we are below max speed *relative to the direction of travel*.
            // This allows the rocket to potentially exceed maxSpeed briefly due to inherited velocity,
            // but the engine won't push it faster once it reaches that speed limit along its forward axis.
            float currentSpeedAlongForward = Vector3.Dot(rb.linearVelocity, transform.forward);

            if (currentSpeedAlongForward < maxSpeed)
            {
                // Apply force consistently. ForceMode.Force applies force over time, accounting for mass and FixedDeltaTime.
                rb.AddForce(transform.forward * thrustForce, ForceMode.Force);

                // --- Re-check and Clamp Speed (Optional but Recommended) ---
                // After applying force, if we've now exceeded max speed, clamp it.
                // This prevents overshooting the maxSpeed significantly due to large thrust values.
                currentSpeedAlongForward = Vector3.Dot(rb.linearVelocity, transform.forward); // Re-calculate after AddForce
                if (currentSpeedAlongForward > maxSpeed)
                {
                    // Calculate the excess speed along the forward vector
                    Vector3 excessVelocity = transform.forward * (currentSpeedAlongForward - maxSpeed);
                    // Subtract the excess velocity
                    rb.linearVelocity -= excessVelocity;
                }
                // Alternative simpler clamp (might feel slightly different):
                // if (rb.linearVelocity.magnitude > maxSpeed)
                // {
                //     rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
                // }
            }
            // If already at or above maxSpeed along the forward axis, the engine provides no additional thrust.
        }
    }
}