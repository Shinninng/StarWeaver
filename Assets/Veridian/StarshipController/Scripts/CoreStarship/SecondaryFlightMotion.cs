using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Applies secondary visual motion (like banking/rolling) to a child model when the ship is maneuvering.
    /// This is purely visual and does not affect the physics simulation of the parent Rigidbody.
    /// </summary>
    /// <remarks>
    /// This component may be disabled by the AtmosphericStarshipController when in LERP mode for performance optimization.
    /// </remarks>
    public class SecondaryFlightMotion : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("The child transform that holds the ship's visual model. This transform will be rotated locally relative to the parent.")]
        public Transform visualModel;

        [Header("Banking Effect")]
        [Tooltip("The maximum angle (in degrees) the visual model will bank automatically when the ship is turning (yawing).")]
        public float maxBankAngle = 25f;
        [Tooltip("The speed at which the visual model banks into a turn. Higher values create more responsive visuals.")]
        public float bankSpeed = 4f;
        [Tooltip("The speed at which the visual model returns to a level orientation when the ship stops turning.")]
        public float levelSpeed = 3f;
        [Tooltip("Sensitivity factor linking the ship's physical turn rate (angular velocity) to the visual bank angle. Typically negative for realistic banking (banking into the turn).")]
        public float turnRateSensitivity = -25f;

        [Header("Manual Roll Override")]
        [Tooltip("The duration (in seconds) that automatic banking is temporarily disabled after the pilot performs a manual roll input.")]
        public float manualRollCooldown = 1.5f;

        private AtmosphericStarshipController controller;

        private float currentVisualRoll;
        private float manualRollTimer;

        void Start()
        {
            controller = GetComponent<AtmosphericStarshipController>();

            if (visualModel == null)
            {
                Debug.LogError("VisualAutoRoll: The 'visualModel' transform has not been assigned!", this);
                this.enabled = false;
            }
        }

        /// <summary>
        /// Resets the visual model's local rotation to identity and clears the internal roll state immediately.
        /// Called by AtmosphericStarshipController, for example, when switching to LERP mode.
        /// </summary>
        public void ResetVisuals()
        {
            currentVisualRoll = 0f;
            if (visualModel != null)
            {
                // Instantly reset the visual model's rotation
                visualModel.localRotation = Quaternion.identity;
            }
        }


        void Update()
        {
            // We allow Update to run even if input is null (for AI ships), but HandleManualRollCooldown will exit early.
            if (controller == null || visualModel == null) return;

            HandleManualRollCooldown();

            float targetRoll = 0f;
            // Only apply auto-bank if the manual roll cooldown is not active.
            if (manualRollTimer <= 0)
            {
                // Get the ship's Rigidbody to read its physics state.
                Rigidbody shipRb = controller.RigidbodyComponent;

                // Ensure we are in physics mode (this component should be disabled otherwise, but we check defensively).
                // We check rb.isKinematic as the most direct indicator of physics simulation being active.
                if (shipRb != null && !shipRb.isKinematic)
                {
                    // Get the turning speed around the ship's local 'up' axis (Y-axis).
                    // Using angularVelocity for Unity 6 (2025.1+) compatibility.
                    float turnRate = transform.InverseTransformDirection(shipRb.angularVelocity).y;

                    // Calculate the target roll based on the actual turn rate and sensitivity.
                    targetRoll = Mathf.Clamp(turnRate * turnRateSensitivity, -maxBankAngle, maxBankAngle);
                }
            }

            // Determine which speed to use for interpolation.
            float speed = (Mathf.Abs(targetRoll) > 0.1f) ? bankSpeed : levelSpeed;

            // Smoothly interpolate the current visual roll towards the target roll.
            currentVisualRoll = Mathf.LerpAngle(currentVisualRoll, targetRoll, speed * Time.deltaTime);

            // Apply roll to the Z-axis for the +Z forward convention.
            visualModel.localRotation = Quaternion.Euler(0, 0, currentVisualRoll);
        }

        private void HandleManualRollCooldown()
        {
            // Check if the driver is providing manual roll input by reading from the controller.
            if (Mathf.Abs(controller.ManualRollInput) > 0.01f)
            {
                // If they are, reset the cooldown timer.
                manualRollTimer = manualRollCooldown;
            }
            // If they are not rolling manually, count down the timer.
            else if (manualRollTimer > 0)
            {
                manualRollTimer -= Time.deltaTime;
            }
        }
    }
}