using System;
using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// Manages the weapon systems (Primary, Secondary, Bombardment) on a starship or turret.
    /// Handles firing logic, ammunition tracking, projectile spawning, gimbaling, and targeting coordination with the driver.
    /// </summary>
    public class ShipWeaponController : MonoBehaviour
    {
        // Events for FX Controller (Decoupled)
        /// <summary>
        /// Invoked when a primary weapon is fired. Passes the Transform of the fire point used.
        /// </summary>
        public event Action<Transform> OnPrimaryFired;
        /// <summary>
        /// Invoked when a secondary weapon is fired. Passes the Transform of the fire point used.
        /// </summary>
        public event Action<Transform> OnSecondaryFired;
        /// <summary>
        /// Invoked when a bombardment weapon is dropped. Passes the Transform of the drop point used.
        /// </summary>
        public event Action<Transform> OnBombDropped;


        [Header("Weapon Configurations")]
        [Tooltip("Configuration stats for the primary weapon system (e.g., lasers).")]
        public WeaponStats primaryWeaponStats;
        [Tooltip("Configuration stats for the secondary weapon system (e.g., rockets or missiles). Can be configured as guided or unguided.")]
        public WeaponStats secondaryWeaponStats;
        [Tooltip("Configuration stats for the bombardment weapon system (e.g., bombs).")]
        public WeaponStats bombardmentWeaponStats;

        [Header("Firing Points")]
        [Tooltip("List of Transforms representing the exit points for primary projectiles. Firing alternates between these points.")]
        public List<Transform> primaryFirePoints;
        [Tooltip("List of Transforms representing the exit points for secondary projectiles.")]
        public List<Transform> secondaryFirePoints;
        [Tooltip("List of Transforms representing the release points for bombardment weapons.")]
        public List<Transform> bombDropPoints;

        // Gimbaling and Targeting
        [Header("Gimbaling and Targeting")]
        [Tooltip("The maximum angle (in degrees) that primary and secondary weapons can pivot (gimbal) towards the driver's aim point from their forward direction.")]
        public float maxGimbalAngle = 15f;

        /// <summary>
        /// The currently selected target GameObject. Populated by the active IShipDriver (Player or AI).
        /// Used as the destination for guided weapons when fired (Fire-and-Forget).
        /// </summary>
        public GameObject CurrentTarget { get; private set; }

        // Ammo Tracking
        public int CurrentPrimaryAmmo { get; private set; }
        public int CurrentSecondaryAmmo { get; private set; }
        public int CurrentBombAmmo { get; private set; }

        // Fire Rate Timers and Iterators (private fields)
        private float lastPrimaryFireTime = -10f;
        private float lastSecondaryFireTime = -10f;
        private float lastBombFireTime = -10f;
        private int nextPrimaryFirePointIndex = 0;
        private int nextSecondaryFirePointIndex = 0;
        private int nextBombDropPointIndex = 0;
        private HealthComponent _ownerHealth;
        private Rigidbody ownerRigidbody;
        private Transform shipTransform; // Reference to the main ship transform


        void Awake()
        {
            ownerRigidbody = GetComponentInParent<Rigidbody>();


            // Cache our own health component
            _ownerHealth = GetComponentInParent<HealthComponent>();


            // Determine the main transform of the entity (for checking firing cones)
            if (ownerRigidbody != null)
            {
                shipTransform = ownerRigidbody.transform;
            }
            else
            {
                // Fallback for stationary objects (like turrets)
                shipTransform = this.transform;
            }

            // Initialize ammo on first load.
            InitializeAmmo();
        }

        void OnEnable()
        {
            ResetFiringState();
        }

        /// <summary>
        /// Resets the ammunition counts for all weapons back to their maximum capacity defined in their respective WeaponStats.
        /// </summary>
        public void ResetAmmunition()
        {
            InitializeAmmo();
        }

        private void InitializeAmmo()
        {
            if (primaryWeaponStats != null) CurrentPrimaryAmmo = primaryWeaponStats.maxAmmo;
            if (secondaryWeaponStats != null) CurrentSecondaryAmmo = secondaryWeaponStats.maxAmmo;
            if (bombardmentWeaponStats != null) CurrentBombAmmo = bombardmentWeaponStats.maxAmmo;
        }

        private void ResetFiringState()
        {
            lastPrimaryFireTime = Time.time - 0.1f;
            lastSecondaryFireTime = Time.time - 0.1f;
            lastBombFireTime = Time.time - 0.1f;
            nextPrimaryFirePointIndex = 0;
            nextSecondaryFirePointIndex = 0;
            nextBombDropPointIndex = 0;
            ClearTarget();
        }

        // --- Targeting Methods ---
        // These methods are called by the IShipDriver (PlayerShipDriver or AI drivers).

        /// <summary>
        /// Sets the current target for guided weapons and tactical awareness.
        /// </summary>
        /// <param name="newTarget">The GameObject to target. Can be null to clear the target.</param>
        public void SetTarget(GameObject newTarget)
        {
            // We allow setting null to clear the target.
            if (newTarget == null)
            {
                CurrentTarget = null;
                return;
            }

            // Basic validation: We rely on the driver to provide valid targets (e.g., via FactionManager),
            // but we still check if the target is alive as a safeguard (useful for AI compatibility).
            if (newTarget.TryGetComponent(out HealthComponent health) && !health.IsAlive)
            {
                CurrentTarget = null;
                return;
            }
            CurrentTarget = newTarget;
        }

        /// <summary>
        /// Clears the current target selection.
        /// </summary>
        public void ClearTarget()
        {
            CurrentTarget = null;
        }
        // ------------------------------

        /// <summary>
        /// Attempts to fire the primary weapon towards the specified aim position.
        /// </summary>
        /// <param name="aimPosition">The world-space position to aim at.</param>
        /// <returns>True if the weapon fired successfully.</returns>
        public bool FirePrimary(Vector3 aimPosition)
        {
            int ammo = CurrentPrimaryAmmo;
            // Primary weapons are always fired as unguided.
            bool fired = FireWeapon(primaryWeaponStats, primaryFirePoints, ref nextPrimaryFirePointIndex, ref lastPrimaryFireTime, ref ammo, aimPosition, OnPrimaryFired);
            CurrentPrimaryAmmo = ammo;
            return fired;
        }

        /// <summary>
        /// Attempts to fire the secondary weapon.
        /// If the weapon is configured as guided AND the driver is actively engaged, it will fire as guided towards the CurrentTarget.
        /// Otherwise, it will fire as unguided towards the aimPosition.
        /// </summary>
        /// <param name="aimPosition">The world-space point being aimed at (used for unguided firing).</param>
        /// <param name="isEngaged">Indicates if the driver is actively engaging a target (required for guided firing).</param>
        /// <returns>True if the weapon fired, false otherwise.</returns>
        public bool FireSecondary(Vector3 aimPosition, bool isEngaged)
        {
            if (secondaryWeaponStats == null) return false;

            bool fired;
            int ammo = CurrentSecondaryAmmo;

            // --- Unified Weapon Logic ---
            // Determine if the weapon should be fired as guided or unguided.
            // It must be configured as guided AND the driver must be actively engaged.
            if (secondaryWeaponStats.isGuided && isEngaged)
            {
                // Handle Guided Firing (uses CurrentTarget set by the driver)
                fired = FireGuided(secondaryWeaponStats, secondaryFirePoints, ref nextSecondaryFirePointIndex, ref lastSecondaryFireTime, ref ammo, OnSecondaryFired);
            }
            else
            {
                // Handle Unguided Firing (uses aimPosition, even if the weapon *could* be guided)
                // This allows firing guided missiles as unguided rockets if not engaged.
                fired = FireWeapon(secondaryWeaponStats, secondaryFirePoints, ref nextSecondaryFirePointIndex, ref lastSecondaryFireTime, ref ammo, aimPosition, OnSecondaryFired);
            }

            CurrentSecondaryAmmo = ammo;
            return fired;
        }

        /// <summary>
        /// Attempts to drop a bombardment weapon.
        /// </summary>
        /// <returns>True if the weapon was dropped successfully.</returns>
        public bool FireBomb()
        {
            if (bombardmentWeaponStats == null || bombDropPoints.Count == 0) return false;
            if (Time.time - lastBombFireTime < bombardmentWeaponStats.refireRate) return false;
            if (bombardmentWeaponStats.maxAmmo > 0 && CurrentBombAmmo <= 0) return false;

            Transform dropPoint = GetNextFirePoint(bombDropPoints, ref nextBombDropPointIndex);
            if (dropPoint == null) return false;

            // Safety check for prefab
            if (bombardmentWeaponStats.projectilePrefab == null) return false;

            GameObject bombInstance = Instantiate(bombardmentWeaponStats.projectilePrefab, dropPoint.position, dropPoint.rotation);

            // --- NEW: PASS OWNER HEALTH TO BOMB ---
            // This is the critical fix to prevent self-destruction
            if (bombInstance.TryGetComponent(out ExplosiveImpact impactScript))
            {
                impactScript.OwnerHealth = _ownerHealth;
            }
            // --- END NEW ---

            if (bombInstance.TryGetComponent(out Rigidbody bombRb))
            {
                if (ownerRigidbody != null)
                {
                    // Inherit velocity from the owner Rigidbody (using linearVelocity for Unity 6+)
                    bombRb.linearVelocity = ownerRigidbody.linearVelocity;
                }
            }

            lastBombFireTime = Time.time;
            if (bombardmentWeaponStats.maxAmmo > 0)
            {
                CurrentBombAmmo--;
            }

            // Invoke the event
            OnBombDropped?.Invoke(dropPoint);

            return true;
        }

        // Handles firing standard (unguided) weapons with gimbaling.
        private bool FireWeapon(WeaponStats stats, List<Transform> firePoints, ref int firePointIndex, ref float lastFireTime, ref int currentAmmo, Vector3 aimPosition, Action<Transform> fireEvent)
        {
            if (stats == null || firePoints.Count == 0) return false;
            if (Time.time - lastFireTime < stats.refireRate) return false;
            if (stats.maxAmmo > 0 && currentAmmo <= 0) return false;

            Transform firePoint = GetNextFirePoint(firePoints, ref firePointIndex);
            if (firePoint == null) return false;

            Vector3 rawDirection = (aimPosition - firePoint.position).normalized;

            // Gimbaling starts from the fire point's forward vector (+Z).
            Vector3 fireDirection = Vector3.RotateTowards(firePoint.forward, rawDirection, Mathf.Deg2Rad * maxGimbalAngle, 0.0f);

            if (stats.type == WeaponType.Laser)
            {
                fireDirection = ApplyAccuracyModel(fireDirection, stats);
            }

            // Spawn the projectile (passing null for target as it's unguided)
            SpawnProjectile(stats.projectilePrefab, firePoint.position, fireDirection, null);

            lastFireTime = Time.time;
            if (stats.maxAmmo > 0)
            {
                currentAmmo--;
            }

            // Invoke the passed event delegate
            fireEvent?.Invoke(firePoint);

            return true;
        }

        // Handles firing guided weapons. Requires a valid CurrentTarget and checks lock constraints.
        private bool FireGuided(WeaponStats stats, List<Transform> firePoints, ref int firePointIndex, ref float lastFireTime, ref int currentAmmo, Action<Transform> fireEvent)
        {
            if (stats == null || firePoints.Count == 0 || !stats.isGuided) return false;
            if (Time.time - lastFireTime < stats.refireRate) return false;
            if (stats.maxAmmo > 0 && currentAmmo <= 0) return false;

            // Pre-Launch Checks: Ensure the target is valid.
            if (CurrentTarget == null || !CurrentTarget.activeInHierarchy) return false;

            // Check if the target is alive (if it has a HealthComponent)
            if (CurrentTarget.TryGetComponent(out HealthComponent health) && !health.IsAlive)
            {
                // If the target is dead, clear it and abort launch.
                ClearTarget();
                return false;
            }

            // Check Distance/Angle (Retained for AI compatibility and balance constraints)
            Vector3 directionToTarget = CurrentTarget.transform.position - shipTransform.position;
            float distanceToTarget = directionToTarget.magnitude;
            if (distanceToTarget > stats.maxLockDistance) return false;

            // Check the lock-on angle against the ship's forward vector (+Z).
            float angleToTarget = Vector3.Angle(shipTransform.forward, directionToTarget.normalized);
            if (angleToTarget > stats.maxLockAngle) return false;

            // All checks passed, fire the rocket.
            Transform firePoint = GetNextFirePoint(firePoints, ref firePointIndex);
            if (firePoint == null) return false;


            // Guided rockets launch straight from the fire point's forward direction (+Z). Guidance takes over after launch.
            Vector3 launchDirection = firePoint.forward;

            // Spawn the projectile, passing the CurrentTarget reference for initialization.
            // This ensures the missile knows what to track (Fire-and-Forget).
            SpawnProjectile(stats.projectilePrefab, firePoint.position, launchDirection, CurrentTarget);

            lastFireTime = Time.time;
            if (stats.maxAmmo > 0)
            {
                currentAmmo--;
            }

            // Invoke the passed event delegate
            fireEvent?.Invoke(firePoint);

            return true;
        }

        // Modified SpawnProjectile to handle optional target parameter for guided weapons initialization.
        private void SpawnProjectile(GameObject prefab, Vector3 position, Vector3 direction, GameObject target)
        {
            if (prefab == null) return;

            Quaternion rotation = Quaternion.LookRotation(direction);
            GameObject projectileInstance = Instantiate(prefab, position, rotation);

            // Calculate inherited velocity (using linearVelocity for Unity 6+)
            Vector3 inheritedVelocity = ownerRigidbody != null ? ownerRigidbody.linearVelocity : Vector3.zero;

            // Initialize the projectile.
            // Note: Assumes GuidedRocketProjectile and ProjectileBase classes exist elsewhere in the project.
            // Check if it's a guided projectile and if a target was provided
            if (target != null && projectileInstance.TryGetComponent(out GuidedRocketProjectile guidedProjectile))
            {
                // Use the specialized guided initialization
                // This is the "Fire-and-Forget" handoff. The projectile now owns the target reference.
                guidedProjectile.InitializeGuided(this.gameObject, inheritedVelocity, target);
            }
            // Fallback to standard projectile initialization
            else if (projectileInstance.TryGetComponent(out ProjectileBase projectile))
            {
                projectile.Initialize(this.gameObject, inheritedVelocity);
            }
        }

        private Transform GetNextFirePoint(List<Transform> firePoints, ref int index)
        {
            if (firePoints == null || firePoints.Count == 0) return null;
            Transform point = firePoints[index];
            index = (index + 1) % firePoints.Count;
            return point;
        }

        private Vector3 ApplyAccuracyModel(Vector3 direction, WeaponStats stats)
        {
            float distance = stats.maxRange;
            float accuracy;
            float falloffStartDist = stats.maxRange * stats.accuracyFalloffStart;

            if (distance <= falloffStartDist)
            {
                accuracy = 1f;
            }
            else
            {
                float falloffRange = stats.maxRange - falloffStartDist;
                float distancePastFalloff = distance - falloffStartDist;
                float falloffPercent = Mathf.Clamp01(distancePastFalloff / falloffRange);
                accuracy = Mathf.Lerp(1f, stats.minAccuracy, falloffPercent);
            }

            // Assuming max deviation of 5 degrees at 0 accuracy
            float maxDeviationAngle = (1f - accuracy) * 5f;

            if (maxDeviationAngle > 0.01f)
            {
                Quaternion randomSpread = UnityEngine.Random.rotationUniform;
                Quaternion spreadRotation = Quaternion.Slerp(Quaternion.identity, randomSpread, maxDeviationAngle / 360f);
                return spreadRotation * direction;
            }

            return direction;
        }
    }
}