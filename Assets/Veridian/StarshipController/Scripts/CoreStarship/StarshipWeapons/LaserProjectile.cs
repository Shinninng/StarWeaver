using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    public class LaserProjectile : ProjectileBase
    {
        [Header("Laser Settings")]
        public float speed = 500f;
        public float damage = 15f;
        public GameObject impactVFXPrefab;

        // --- MODIFIED ---
        // Set to -1 (Everything) to hit all layers.
        public LayerMask hitLayerMask = -1;
        // --- END MODIFIED ---

        private Rigidbody rb;

        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            // Important for high speeds
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        protected override void OnInitialized()
        {
            // Velocity is the sum of inherited velocity and the laser's own forward speed (Local Z axis).
            rb.linearVelocity = InheritedVelocity + transform.forward * speed;
        }

        // Use FixedUpdate Raycasting for reliable hit detection at high speeds (CCD alternative)
        void FixedUpdate()
        {
            // Calculate how far the projectile travels this physics step
            float travelDistance = (rb.linearVelocity * Time.fixedDeltaTime).magnitude;

            // Raycast forward from the current position
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, travelDistance, hitLayerMask))
            {
                // We call the base class HandleImpact, which now has our robust self-check logic.
                HandleImpact(hit.point, hit.normal, hit.collider.gameObject);
            }
        }

        // Fallback collision detection
        void OnCollisionEnter(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            // We call the base class HandleImpact, which now has our robust self-check logic.
            HandleImpact(contact.point, contact.normal, collision.gameObject);
        }


        // We receive the hitHealth component for free from HandleImpact.
        protected override void ApplyDamage(GameObject hitObject, HealthComponent hitHealth)
        {
            // Lasers deal direct damage
            // The base class already confirmed this isn't our owner,
            // so if hitHealth exists, we apply damage.
            if (hitHealth != null)
            {
                hitHealth.ApplyDamage(damage);
            }
        }

        protected override void SpawnImpactFX(Vector3 impactPoint, Vector3 impactNormal)
        {
            if (impactVFXPrefab != null)
            {
                // Align the VFX with the impact surface normal
                Instantiate(impactVFXPrefab, impactPoint, Quaternion.LookRotation(impactNormal));
            }
        }
    }
}