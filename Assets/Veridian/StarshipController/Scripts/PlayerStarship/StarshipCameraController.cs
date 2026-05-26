using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// Manages the third-person camera behavior for following a starship, including rotation, zooming, dynamic effects, and collision detection.
    /// </summary>
    public class StarshipCameraController : MonoBehaviour
    {
        [Header("Target Configuration")]
        [Tooltip("The Transform representing the ship that the camera should follow. This is typically set automatically by the PlayerShipDriver or GameInitializer.")]
        public Transform Target;

        [SerializeField, Tooltip("The settings defining the camera's behavior and appearance.")]
        private CameraSettings cameraSettings = new();

        private InputProvider _input;
        private Transform _cameraTransform;
        private Camera _cameraComponent;
        private AtmosphericStarshipController shipController;
        private float currentLagDistance;

        // Reference to the FactionManager for handling respawn events
        private FactionManager _factionManager;

        // G-Force effect state variables
        private float latestGForceValue = 0f;
        private float currentGForceIntensity = 0f;
        private float baseFOV;
        private Vector3 cameraShakeOffset = Vector3.zero;


        /// <summary>
        /// Defines the configuration settings for the starship camera behavior.
        /// </summary>
        [System.Serializable]
        public class CameraSettings
        {
            [Header("Positioning")]
            [Tooltip("The offset from the target's origin where the camera pivots (the anchor point). Default: (0, 2.5, 0).")]
            public Vector3 anchorOffset = new(0f, 2.5f, 0f);
            [Tooltip("The default distance the camera maintains from the anchor point. Default: 15.")]
            public float defaultDistance = 15f;

            [Header("Smoothing")]
            [Tooltip("Speed at which the camera rotation smooths towards the target rotation (using exponential decay). Higher values are snappier. Default: 15.")]
            public float rotationSmoothSpeed = 15f;

            [Header("Rotation Sensitivity (Legacy - Currently unused)")]
            [Tooltip("LEGACY: Horizontal rotation sensitivity (Yaw). Currently unused as the camera auto-aligns.")]
            public float sensitivityX = 50f;
            [Tooltip("LEGACY: Vertical rotation sensitivity (Pitch). Currently unused as the camera auto-aligns.")]
            public float sensitivityY = 30f;
            [Tooltip("LEGACY: Minimum vertical angle (Pitch).")]
            public float minPitch = -85f;
            [Tooltip("LEGACY: Maximum vertical angle (Pitch).")]
            public float maxPitch = 85f;

            [Header("Zoom")]
            [Tooltip("The minimum distance the camera can zoom in towards the target. Default: 5.")]
            public float minDistance = 5f;
            [Tooltip("The maximum distance the camera can zoom out from the target. Default: 50.")]
            public float maxDistance = 50f;
            [Tooltip("The speed at which the camera zooms in or out based on input. Default: 10.")]
            public float zoomSpeed = 10f;

            [Header("Auto-Leveling (Pitch)")]
            [Tooltip("Speed at which the camera returns to the horizon (pitch=0). Default: 2.")]
            public float returnToHorizonSpeed = 2f;

            [Header("Auto-Align (Yaw)")]
            [Tooltip("If checked, the camera will automatically align behind the ship. Default: true.")]
            public bool autoAlignYaw = true;
            [Tooltip("LEGACY: Delay before auto-aligning starts (no longer relevant as manual rotation is disabled). Default: 2.5.")]
            public float autoAlignDelay = 2.5f;
            [Tooltip("How quickly the camera returns to its default yaw position (directly behind the ship). Default: 1.2.")]
            public float autoAlignSpeed = 1.2f;

            [Header("Dynamic Effects - Acceleration Lag")]
            [Tooltip("The maximum distance the camera lags behind the ship during forward acceleration. Default: 4.")]
            public float accelerationLagAmount = 4f;
            [Tooltip("How quickly the camera lags and then catches back up. Default: 5.")]
            public float accelerationLagSpeed = 5f;

            [Header("Dynamic Effects - G-Force")]
            [Tooltip("The G-force level at which camera effects (shake/FOV) begin. Default: 5.0G.")]
            public float gForceEffectThreshold = 5.0f;
            [Tooltip("The maximum intensity of camera shake applied at high G-forces. Default: 0.15.")]
            public float gForceShakeAmount = 0.15f;
            [Tooltip("The maximum change in Field of View (FOV) applied at high G-forces to simulate strain. Default: 5.0.")]
            public float gForceFovChange = 5.0f;
            [Tooltip("How quickly the G-force effects ramp up and down. Default: 3.0.")]
            public float gForceEffectSpeed = 3.0f;

            [Header("Collision Detection")]
            [Tooltip("If enabled, the camera will attempt to avoid clipping through scene geometry. Default: true.")]
            public bool collisionEnabled = true;
            [Tooltip("The physics layers the camera will collide with.")]
            public LayerMask collisionLayers = Physics.DefaultRaycastLayers;
            [Tooltip("The radius of the sphere used for collision detection. Helps prevent clipping at edges. Default: 0.5.")]
            public float collisionRadius = 0.5f;
            [Tooltip("A small offset to pull the camera slightly away from the collision point. Default: 0.2.")]
            public float collisionOffset = 0.2f;
        }

        // Camera State Variables
        private float m_CameraYaw, m_CameraPitch;
        private float m_CurrentDistance;
        private float cameraResetCooldownTimer;
        private Quaternion m_DesiredOrbitalRotation;

        void Awake()
        {
            if (Target == null)
            {
                // Fallback: Attempt to find the PlayerShipDriver in the scene.
                PlayerShipDriver driver = FindFirstObjectByType<PlayerShipDriver>();
                if (driver != null)
                {
                    Target = driver.transform;
                    // Debug.Log("StarshipCameraController: Target not assigned, automatically linked to PlayerShipDriver.", this);
                }
                // If not found, we remain inactive until a target is assigned (e.g., by GameInitializer)
            }

            _cameraTransform = this.transform;

            // Get the Camera component
            _cameraComponent = GetComponent<Camera>();
            if (_cameraComponent == null)
            {
                _cameraComponent = GetComponentInChildren<Camera>();
            }

            // Store base FOV
            if (_cameraComponent != null)
            {
                baseFOV = _cameraComponent.fieldOfView;
            }
        }

        void Start()
        {
            _input = InputProvider.Instance;
            if (_input == null)
            {
                Debug.LogError("StarshipCameraController: InputProvider instance not found.");
                this.enabled = false;
                return;
            }

            // Initialize the link to the current target (if any)
            InitializeShipControllerLink();

            m_CurrentDistance = cameraSettings.defaultDistance;

            // Initialize FactionManager link and subscribe to respawn event.
            InitializeFactionManagerLink();
        }

        /// <summary>
        /// Initializes the link to the FactionManager to monitor player respawn events.
        /// </summary>
        private void InitializeFactionManagerLink()
        {
            // If already linked and subscribed, do nothing.
            if (_factionManager != null) return;

            _factionManager = FactionManager.Instance;

            if (_factionManager != null)
            {
                // Subscribe to the respawn event
                // Ensure we don't double subscribe (safe initialization)
                _factionManager.OnPlayerShipRespawned -= HandlePlayerRespawn;
                _factionManager.OnPlayerShipRespawned += HandlePlayerRespawn;
            }
            else
            {
                //   Debug.LogWarning("StarshipCameraController: FactionManager instance not found. Respawn handling will be inactive until FactionManager is available.");
            }
        }

        /// <summary>
        /// Handles the event when the player's ship is respawned by the FactionManager.
        /// </summary>
        /// <param name="newPlayerTransform">The transform of the newly respawned ship.</param>
        private void HandlePlayerRespawn(Transform newPlayerTransform)
        {
            if (newPlayerTransform == null) return;

            // Update the target to the newly spawned ship instance.
            Target = newPlayerTransform;

            // Re-initialize the link to the new ship's controller (essential for G-Force updates).
            InitializeShipControllerLink();

            // Immediately snap the camera to the new target.
            SnapToTarget();
        }


        /// <summary>
        /// Initializes the link to the target ship's controller and subscribes to necessary events (like G-Force updates).
        /// </summary>
        private void InitializeShipControllerLink()
        {
            if (shipController != null)
            {
                // Unsubscribe first if already subscribed (safe initialization)
                shipController.OnGForceUpdate -= HandleGForceUpdate;
            }

            if (Target != null)
            {
                shipController = Target.GetComponent<AtmosphericStarshipController>();
                if (shipController != null)
                {
                    // Subscribe to the G-Force update event
                    shipController.OnGForceUpdate += HandleGForceUpdate;
                }
            }
            else
            {
                shipController = null;
            }
        }

        void OnEnable()
        {
            m_CurrentDistance = cameraSettings.defaultDistance;

            // Ensure links are established when enabled
            InitializeShipControllerLink();
            InitializeFactionManagerLink();

            if (Target != null)
            {
                // Initialize rotation to be behind the ship
                m_DesiredOrbitalRotation = GetAnchorBaseRotation();
                UpdateCameraAnglesFromDesiredRotation();
                // Snap immediately to ensure synchronization
                SnapToTarget();
            }
        }

        void OnDisable()
        {
            // Clean up subscriptions when the camera is disabled.
            if (shipController != null)
            {
                shipController.OnGForceUpdate -= HandleGForceUpdate;
            }

            // Unsubscribe from FactionManager
            if (_factionManager != null)
            {
                _factionManager.OnPlayerShipRespawned -= HandlePlayerRespawn;
                _factionManager = null; // Clear reference
            }
        }

        void OnDestroy()
        {
            // Ensure cleanup happens if the camera object is destroyed.
            OnDisable();
        }

        void Update()
        {
            if (_input == null || Target == null) return;

            HandleInput();
            HandleCameraRotation();
            UpdateDynamicEffects();
        }

        /// <summary>
        /// Updates dynamic camera effects like acceleration lag and G-force feedback.
        /// </summary>
        private void UpdateDynamicEffects()
        {
            // Calculate the target lag based on the ship's forward thrust and boost
            float targetLag = 0f;

            if (shipController != null)
            {
                // --- Acceleration Lag ---
                // Only consider forward thrust (value > 0)
                float forwardThrust = Mathf.Max(0, shipController.CurrentThrustLevel);
                // The boost multiplier will increase the effect
                float thrustFactor = forwardThrust * shipController.CurrentBoostMultiplier;

                targetLag = thrustFactor * cameraSettings.accelerationLagAmount;
            }

            // Smoothly interpolate the current lag value towards the target
            currentLagDistance = Mathf.Lerp(currentLagDistance, targetLag, cameraSettings.accelerationLagSpeed * Time.deltaTime);

            // Apply G-Force Effects (Shake and FOV)
            ApplyGForceEffects();
        }

        void LateUpdate()
        {
            if (Target == null) return;

            // HandleMovement runs in LateUpdate. Because the target Rigidbody uses Interpolation,
            // its Transform position/rotation will be smoothly updated when accessed here, eliminating jitter.
            HandleCameraMovement();
        }

        /// <summary>
        /// Event handler for G-Force updates (runs during FixedUpdate context).
        /// </summary>
        /// <param name="gForce">The new G-force value.</param>
        private void HandleGForceUpdate(float gForce)
        {
            // Simply store the latest value. Processing happens in Update for smooth visuals.
            latestGForceValue = gForce;
        }

        /// <summary>
        /// Applies G-Force effects (camera shake and FOV changes) based on the current G-force intensity.
        /// </summary>
        private void ApplyGForceEffects()
        {
            // 1. Calculate the target intensity based on the latest G-force value.
            float targetIntensity = 0f;
            if (latestGForceValue > cameraSettings.gForceEffectThreshold)
            {
                // Calculate intensity based on how much the threshold is exceeded.
                float excessGForce = latestGForceValue - cameraSettings.gForceEffectThreshold;
                // Normalize the excess G-force (assuming effects max out around 10G excess).
                targetIntensity = Mathf.Clamp01(excessGForce / 10.0f);
            }

            // 2. Smoothly interpolate the current intensity towards the target.
            currentGForceIntensity = Mathf.Lerp(currentGForceIntensity, targetIntensity, cameraSettings.gForceEffectSpeed * Time.deltaTime);

            // Optimization: If intensity is negligible, ensure it's zeroed out.
            if (currentGForceIntensity < 0.001f)
            {
                currentGForceIntensity = 0f;
                cameraShakeOffset = Vector3.zero;
            }
            else
            {
                // 3. Calculate Camera Shake
                float shakeAmount = currentGForceIntensity * cameraSettings.gForceShakeAmount;
                // Use Perlin noise for procedural, subtle, and smooth shake
                float time = Time.time * 10f; // Adjust frequency (10f) as needed
                float shakeX = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * shakeAmount;
                float shakeY = (Mathf.PerlinNoise(0f, time) * 2f - 1f) * shakeAmount;
                // We apply shake only on X and Y (screen space) for this effect.
                cameraShakeOffset = new Vector3(shakeX, shakeY, 0f);
            }

            // 4. Apply FOV Change
            if (_cameraComponent != null)
            {
                float targetFOV = baseFOV + (currentGForceIntensity * cameraSettings.gForceFovChange);
                // We don't need to lerp FOV here as currentGForceIntensity is already lerped.
                _cameraComponent.fieldOfView = targetFOV;
            }
        }

        /// <summary>
        /// Handles player input related to camera control (e.g., zooming).
        /// </summary>
        private void HandleInput()
        {
            float zoomAxisInput = _input.GetZoomInput();

            if (Mathf.Abs(zoomAxisInput) > 0.01f)
            {
                float zoomAmount = -zoomAxisInput * cameraSettings.zoomSpeed * Time.deltaTime;

                m_CurrentDistance += zoomAmount;
                m_CurrentDistance = Mathf.Clamp(m_CurrentDistance, cameraSettings.minDistance, cameraSettings.maxDistance);
            }
        }

        /// <summary>
        /// Manages the camera's rotation logic, primarily handling the auto-alignment behavior.
        /// </summary>
        public void HandleCameraRotation()
        {
            // Always run the auto-align logic.

            if (cameraResetCooldownTimer > 0)
            {
                cameraResetCooldownTimer -= Time.deltaTime;
            }
            else // Cooldown is over, now we can start auto-aligning.
            {
                // Auto-align Pitch (return to horizon).
                if (cameraSettings.returnToHorizonSpeed > 0)
                {
                    m_CameraPitch = Mathf.LerpAngle(m_CameraPitch, 0f, cameraSettings.returnToHorizonSpeed * Time.deltaTime);
                }

                // Auto-align Yaw (return to directly behind the ship).
                if (cameraSettings.autoAlignYaw)
                {
                    m_CameraYaw = Mathf.LerpAngle(m_CameraYaw, 0f, cameraSettings.autoAlignSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Calculates and applies the final camera position and rotation for the frame.
        /// </summary>
        public void HandleCameraMovement()
        {
            Transform anchorTransform = Target;

            Vector3 desiredPosition = CalculateDesiredCameraPosition();

            // Apply acceleration lag
            Vector3 laggedPos = desiredPosition - (_cameraTransform.forward * currentLagDistance);

            // Apply G-force shake offset (transformed into camera space)
            Vector3 finalPos = laggedPos + _cameraTransform.TransformDirection(cameraShakeOffset);

            // Calculate the look rotation towards the pivot point
            Vector3 pivot = CalculatePivotPoint(anchorTransform);
            Vector3 directionToPivot = pivot - finalPos;
            if (directionToPivot.magnitude < 0.01f)
            {
                // Handle edge case where camera is exactly at the pivot
                directionToPivot = m_DesiredOrbitalRotation * Vector3.forward;
            }
            Quaternion lookRotation = Quaternion.LookRotation(directionToPivot.normalized, m_DesiredOrbitalRotation * Vector3.up);

            // Apply smoothing to the rotation
            float t = 1f - Mathf.Exp(-cameraSettings.rotationSmoothSpeed * Time.deltaTime);
            Quaternion smoothedRot = Quaternion.Slerp(_cameraTransform.rotation, lookRotation, t);

            _cameraTransform.SetPositionAndRotation(finalPos, smoothedRot);
        }

        /// <summary>
        /// Calculates the pivot point (anchor) around which the camera orbits.
        /// </summary>
        private Vector3 CalculatePivotPoint(Transform anchorTransform)
        {
            return anchorTransform.position +
                   anchorTransform.up * cameraSettings.anchorOffset.y +
                   anchorTransform.right * cameraSettings.anchorOffset.x +
                   anchorTransform.forward * cameraSettings.anchorOffset.z;
        }

        /// <summary>
        /// Calculates the ideal camera position before applying dynamic effects and smoothing, including collision detection.
        /// </summary>
        private Vector3 CalculateDesiredCameraPosition()
        {
            if (Target == null) return _cameraTransform.position;
            Transform anchorTransform = Target;

            Vector3 pivot = CalculatePivotPoint(anchorTransform);

            // Calculate the desired orbital rotation based on ship orientation and camera angles
            Quaternion baseRotation = GetAnchorBaseRotation();
            Quaternion orbitalRotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            m_DesiredOrbitalRotation = baseRotation * orbitalRotation;

            Vector3 desiredOffset = m_DesiredOrbitalRotation * new Vector3(0, 0, -m_CurrentDistance);
            Vector3 desiredPosition = pivot + desiredOffset;

            // Handle camera collision
            if (cameraSettings.collisionEnabled)
            {
                Vector3 dir = (desiredPosition - pivot).normalized;
                float dist = m_CurrentDistance;
                // Use SphereCast for robust collision detection
                if (Physics.SphereCast(pivot, cameraSettings.collisionRadius, dir, out RaycastHit hit, dist, cameraSettings.collisionLayers))
                {
                    // Adjust position based on collision point and offset
                    float adjustedDist = hit.distance - cameraSettings.collisionOffset;
                    if (adjustedDist < 0f) adjustedDist = 0f;
                    desiredPosition = pivot + dir * adjustedDist;
                }
            }

            return desiredPosition;
        }

        /// <summary>
        /// Calculates the base rotation aligned with the ship's orientation.
        /// </summary>
        private Quaternion GetAnchorBaseRotation()
        {
            if (Target == null) return Quaternion.identity;
            Transform anchorTransform = Target;
            return Quaternion.LookRotation(anchorTransform.forward, anchorTransform.up);
        }

        /// <summary>
        /// Updates the internal camera pitch and yaw angles based on the desired orbital rotation.
        /// </summary>
        private void UpdateCameraAnglesFromDesiredRotation()
        {
            if (Target == null) return;
            Quaternion baseRot = GetAnchorBaseRotation();
            Quaternion inverseBase = Quaternion.Inverse(baseRot);
            // Calculate the local rotation relative to the base rotation
            Quaternion localRotation = inverseBase * m_DesiredOrbitalRotation;
            Vector3 euler = localRotation.eulerAngles;
            m_CameraPitch = NormalizeAngle(euler.x);
            m_CameraYaw = NormalizeAngle(euler.y);
        }

        /// <summary>
        /// Normalizes an angle to the range [-180, 180].
        /// </summary>
        protected float NormalizeAngle(float angle)
        {
            if (angle > 180) return angle - 360;
            if (angle < -180) return angle + 360;
            return angle;
        }

        /// <summary>
        /// Instantly moves the camera to the target position behind the ship and resets all dynamic effects.
        /// </summary>
        public void SnapToTarget()
        {
            if (Target == null) return;

            // Reset dynamic effects on snap
            currentLagDistance = 0f;
            currentGForceIntensity = 0f;
            cameraShakeOffset = Vector3.zero;
            latestGForceValue = 0f;
            if (_cameraComponent != null)
            {
                _cameraComponent.fieldOfView = baseFOV;
            }

            m_DesiredOrbitalRotation = GetAnchorBaseRotation();
            UpdateCameraAnglesFromDesiredRotation();
            m_CurrentDistance = cameraSettings.defaultDistance;

            Vector3 desiredPosition = CalculateDesiredCameraPosition();
            Vector3 pivot = CalculatePivotPoint(Target);
            Vector3 directionToPivot = pivot - desiredPosition;

            if (directionToPivot.magnitude < 0.01f)
            {
                directionToPivot = m_DesiredOrbitalRotation * Vector3.forward;
            }
            Quaternion lookRotation = Quaternion.LookRotation(directionToPivot.normalized, m_DesiredOrbitalRotation * Vector3.up);

            _cameraTransform.SetPositionAndRotation(desiredPosition, lookRotation);
        }
    }
}