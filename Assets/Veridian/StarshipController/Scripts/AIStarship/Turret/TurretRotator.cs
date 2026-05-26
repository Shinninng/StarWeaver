using UnityEngine;
using Veridian.Starship.AI;
using Veridian.Starship.Core;
using Veridian.Starship.Weapons;

// Placing the turret-specific logic in a dedicated namespace.
namespace Veridian.Starship.Defense
{
    /// <summary>
    /// Manages the physical rotation and firing execution for a stationary AI turret.
    /// Reads inputs from AiWeaponController and controls the physical rotation and the ShipWeaponController trigger.
    /// </summary>
    [RequireComponent(typeof(AiWeaponController))]
    [RequireComponent(typeof(ShipWeaponController))]
    [RequireComponent(typeof(StarshipIdentity))]
    public class TurretRotator : MonoBehaviour
    {
        [Header("Turret Configuration")]
        [Tooltip("The Transform representing the rotating part of the turret (head/arm). This handles both Yaw and Pitch and must contain the weapon fire points.")]
        public Transform RotatingPart;

        [Header("Rotation Behavior")]
        [Tooltip("The maximum rotation speed in degrees per second. Controls how quickly the turret tracks targets.")]
        public float RotationSpeed = 75f;

        [Tooltip("If true, the turret will reset to its initial local orientation relative to the base when idle (no active target).")]
        public bool ResetToInitialRotationWhenIdle = true;

        // Internal References
        private AiWeaponController _aiWeaponController;
        private ShipWeaponController _shipWeaponController;
        private StarshipIdentity _identity;
        private Quaternion _initialLocalRotation;
        private Transform _baseTransform; // The transform of the static base (this GameObject)

        void Awake()
        {
            _baseTransform = transform;
            _aiWeaponController = GetComponent<AiWeaponController>();
            _shipWeaponController = GetComponent<ShipWeaponController>();
            _identity = GetComponent<StarshipIdentity>();

            if (RotatingPart == null)
            {
                Debug.LogError($"TurretRotator on {gameObject.name} is missing the RotatingPart reference. Disabling.", this);
                enabled = false;
                return;
            }

            // Store the initial local rotation. This defines the 'forward' orientation when idle.
            _initialLocalRotation = RotatingPart.localRotation;
        }

        void Update()
        {
            // Ensure the turret does not operate if destroyed.
            // FactionManager handles deactivation (distance-based or respawn), which stops Update automatically when the GameObject is inactive.
            if (_identity == null || !_identity.IsAlive)
            {
                return;
            }

            // 1. Determine the target rotation
            Quaternion targetRotation = DetermineTargetRotation();

            // 2. Apply Rotation
            ApplyRotation(targetRotation);

            // 3. Execute Firing Commands
            // Since SimpleAiPilot is absent, this script acts as the driver.
            ExecuteFiring();
        }

        /// <summary>
        /// Determines the desired world-space rotation for the RotatingPart based on engagement status.
        /// </summary>
        private Quaternion DetermineTargetRotation()
        {
            // Check engagement status. ShipWeaponController.CurrentTarget is managed by AiWeaponController and is the authority on the active lock.
            bool isEngaged = _shipWeaponController.CurrentTarget != null;

            if (!isEngaged && ResetToInitialRotationWhenIdle)
            {
                // If idle, return to the initial orientation relative to the base.
                return _baseTransform.rotation * _initialLocalRotation;
            }

            // If engaged (or idle but not resetting), aim towards the calculated AimPosition.
            // AiWeaponController provides the AimPosition (including lead prediction and error).
            Vector3 aimPosition = _aiWeaponController.AimPosition;
            Vector3 direction = aimPosition - RotatingPart.position;

            // Handle edge case where direction vector is too small (e.g., target is exactly at the pivot).
            if (direction.sqrMagnitude < 0.001f)
            {
                return RotatingPart.rotation;
            }

            // Calculate the required rotation. Quaternion.LookRotation handles combined Yaw and Pitch.
            return Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Smoothly rotates the RotatingPart towards the target rotation using a finite speed.
        /// </summary>
        private void ApplyRotation(Quaternion targetRotation)
        {
            if (RotationSpeed <= 0)
            {
                // If speed is 0 or less, snap instantly.
                RotatingPart.rotation = targetRotation;
                return;
            }

            // Use Quaternion.RotateTowards for smooth movement respecting the configured speed.
            RotatingPart.rotation = Quaternion.RotateTowards(
                RotatingPart.rotation,
                targetRotation,
                RotationSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Executes firing commands based on the flags set by the AiWeaponController.
        /// </summary>
        private void ExecuteFiring()
        {
            // The AiWeaponController determines readiness based on CombatPersonalitySO and firing solutions.

            Vector3 aimPosition = _aiWeaponController.AimPosition;

            if (_aiWeaponController.FirePrimary)
            {
                // Pass the calculated AimPosition. ShipWeaponController handles gimbaling internally.
                _shipWeaponController.FirePrimary(aimPosition);
            }

            if (_aiWeaponController.FireSecondary)
            {
                // Secondary weapons (e.g., guided missiles) require engagement status for launch logic.
                bool isEngaged = _shipWeaponController.CurrentTarget != null;
                _shipWeaponController.FireSecondary(aimPosition, isEngaged);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (RotatingPart != null)
            {
                // Visualize the current forward direction.
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(RotatingPart.position, RotatingPart.position + RotatingPart.forward * 5f);

                // Visualize the aim position from the AI when running.
                if (Application.isPlaying && _aiWeaponController != null)
                {
                    // Change color based on firing status for better visualization.
                    Gizmos.color = (_aiWeaponController.FirePrimary || _aiWeaponController.FireSecondary) ? Color.red : Color.yellow;

                    Vector3 aimPos = _aiWeaponController.AimPosition;
                    Gizmos.DrawWireSphere(aimPos, 0.5f);
                    Gizmos.DrawLine(RotatingPart.position, aimPos);
                }
            }
        }
    }
}