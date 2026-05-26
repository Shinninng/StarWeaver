using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// Abstract base class for all projectiles (Lasers, Rockets, etc.).
    /// Handles initialization, lifespan, and standardized impact logic.
    /// </summary>
    public abstract class ProjectileBase : MonoBehaviour
    {
        [Header("Base Projectile Settings")]
        [Tooltip("The maximum time (in seconds) the projectile can exist before being destroyed.")]
        public float lifespan = 5f;

        // We now store the owner's GameObject AND its HealthComponent
        // for a 100% robust self-collision check.
        public GameObject Owner { get; private set; }
        public HealthComponent OwnerHealth { get; private set; }

        public Vector3 InheritedVelocity { get; private set; }

        protected float spawnTime;

        protected virtual void Awake() { }

        protected virtual void Start()
        {
            spawnTime = Time.time;
            Destroy(gameObject, lifespan);
        }

        /// <summary>
        /// Initializes the projectile with its owner and the owner's velocity.
        /// </summary>
        public void Initialize(GameObject owner, Vector3 inheritedVelocity)
        {
            Owner = owner;
            InheritedVelocity = inheritedVelocity;

            // Find and cache the owner's HealthComponent.
            if (Owner != null)
            {
                // This is the key to robust self-checking.
                OwnerHealth = Owner.GetComponent<HealthComponent>();
            }

            OnInitialized();
        }

        /// <summary>
        /// Called after Initialize(). Use this for specific projectile setup (e.g., applying initial velocity).
        /// </summary>
        protected abstract void OnInitialized();

        /// <summary>
        /// Standardized method to handle impact logic.
        /// </summary>
        protected virtual void HandleImpact(Vector3 impactPoint, Vector3 impactNormal, GameObject hitObject)
        {
            if (hitObject == null) return;

            // ROBUST SELF-HIT CHECK ---

            // 1. Get the HealthComponent of the object we hit (if it has one).
            HealthComponent hitHealth = hitObject.GetComponentInParent<HealthComponent>();

            // 2. Check if we hit ourselves.
            // This works if we hit our own shield, wing, or body, as they all
            // point to the same HealthComponent.
            if (OwnerHealth != null && hitHealth != null && hitHealth == OwnerHealth)
            {
                return; // Ignore self-collision
            }


            // This is a valid hit.
            ApplyDamage(hitObject, hitHealth); // Pass the HealthComponent we already found
            SpawnImpactFX(impactPoint, impactNormal);
            Destroy(gameObject);
        }
        protected abstract void ApplyDamage(GameObject hitObject, HealthComponent hitHealth);

        protected abstract void SpawnImpactFX(Vector3 impactPoint, Vector3 impactNormal);
    }
}