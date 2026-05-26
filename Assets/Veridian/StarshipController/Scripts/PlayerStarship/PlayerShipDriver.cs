using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Veridian.Starship.Core;
using Veridian.Starship.Weapons;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// Implements the IShipDriver interface to allow a human player to control a starship using input devices.
    /// It also manages player-specific targeting and communicates with the camera and game state systems.
    /// </summary>
    [RequireComponent(typeof(ShipProperties))]
    [RequireComponent(typeof(ShipWeaponController))]
    [RequireComponent(typeof(StarshipIdentity))]
    public class PlayerShipDriver : MonoBehaviour, IShipDriver, IPlayerActivityProvider
    {
        // Private references to core components
        private AtmosphericStarshipController controller;
        private InputProvider input;
        private ShipProperties properties;
        private ShipWeaponController weaponController;
        private StarshipIdentity shipIdentity;

        // Tracks if the player intends to control the ship.
        private bool isActivelyControlling = true;
        // Tracks if this driver is currently initialized and managing the player view (camera/cursor).
        private bool _isViewInitialized = false;

        [Header("Debug Information")]
        [SerializeField, Tooltip("DEBUGGING ONLY. Displays the FactionManager instance found at runtime, used for targeting validation.")]
        private FactionManager debugFactionManager;

        // Reference to the camera controller and its associated camera, managed by this driver.
        private StarshipCameraController _starshipCamera;
        private Camera _playerCamera;

        /// <summary>
        /// Implementation of IPlayerActivityProvider. Indicates if the player is actively playing (i.g., the view system is initialized and controlling the cursor/camera).
        /// </summary>
        public bool IsPlayerActivelyPlaying => _isViewInitialized;


        [Header("Aiming and Targeting Configuration")]
        [Tooltip("The maximum distance (in meters) for the aiming raycast when not engaged. This should generally match the maximum weapon range.")]
        public float maxAimDistance = 1000f;

        [Tooltip("The angle (in degrees) of the cone in front of the ship used for automatic target engagement when the engagement button (default: RMB) is held.")]
        public float targetingConeAngle = 15f;

        /// <summary>
        /// True if the player is holding the engagement button and a valid hostile target is within the targeting cone.
        /// </summary>
        public bool IsTargetEngaged { get; private set; }

        [Header("Debug - Current Target")]
        [SerializeField, Tooltip("DEBUGGING ONLY. Displays the GameObject currently engaged as the target.")]
        private GameObject currentTarget;
        // Cached HealthComponent of the target for managing the target visualizer activation.
        private HealthComponent currentTargetHealth;

        void Awake()
        {
            properties = GetComponent<ShipProperties>();
            weaponController = GetComponent<ShipWeaponController>();
            shipIdentity = GetComponent<StarshipIdentity>();

            if (properties == null || weaponController == null || shipIdentity == null)
            {
                Debug.LogError("PlayerShipDriver requires ShipProperties, ShipWeaponController, and StarshipIdentity components.", this);
                this.enabled = false;
                return;
            }

            // Ensure the associated controller knows this is not an AI ship.
            if (TryGetComponent<AtmosphericStarshipController>(out var shipController))
            {
                shipController.isAI = false;
            }
            else
            {
                Debug.LogError("PlayerShipDriver must be attached to a GameObject with AtmosphericStarshipController.", this);
                this.enabled = false;
                return;
            }
        }

        void Start()
        {
            // Attempt to get the input provider instance
            input = InputProvider.Instance;

            // Ensure GameStateManager exists
            if (GameStateManager.Instance == null)
            {
                if (FindFirstObjectByType<GameStateManager>() == null)
                {
                    Debug.LogWarning("PlayerShipDriver: GameStateManager instance not found. Cursor management may be impaired.", this);
                }
            }

            // Ensure FactionManager exists and assign to debug field
            if (FactionManager.Instance == null)
            {
                Debug.LogError("PlayerShipDriver: FactionManager instance not found during Start. Targeting relies on the manager being present.", this);
                debugFactionManager = null;
            }
            else
            {
                debugFactionManager = FactionManager.Instance;
            }
        }

        /// <summary>
        /// Initializes the player's view system, linking it with the camera controller.
        /// This is typically called by the GameInitializer after the scene is ready.
        /// </summary>
        /// <param name="starshipCam">The StarshipCameraController instance to link with this driver.</param>
        public void InitializePlayerView(StarshipCameraController starshipCam)
        {
            if (starshipCam == null)
            {
                Debug.LogError("PlayerShipDriver initialization failed: StarshipCameraController is null.", this);
                this.enabled = false;
                return;
            }

            _starshipCamera = starshipCam;

            // We need the Camera component reference for aiming calculations (raycasting).
            _playerCamera = _starshipCamera.GetComponent<Camera>();
            if (_playerCamera == null)
            {
                _playerCamera = _starshipCamera.GetComponentInChildren<Camera>();
            }

            if (_playerCamera == null)
            {
                Debug.LogError("PlayerShipDriver: StarshipCameraController does not have a Camera component attached or in children.", this);
                this.enabled = false;
                return;
            }

            // Configure the StarshipCameraController.
            _starshipCamera.Target = this.transform;
            _starshipCamera.enabled = true;
            _starshipCamera.SnapToTarget();

            // Register as the activity provider with the GameStateManager.
            // This signals that the player is now active, allowing GameStateManager to enforce the cursor lock.
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.RegisterPlayerActivityProvider(this);
            }
            else
            {
                Debug.LogError("PlayerShipDriver: GameStateManager.Instance is null during InitializePlayerView. Cursor management relies on the manager being present.", this);
            }
            _isViewInitialized = true;
        }

        void Update()
        {
            // If 'input' is null, try to find the InputProvider instance again.
            if (input == null)
            {
                input = InputProvider.Instance;
                // If it's still null after checking the instance, log an error and disable the component.
                if (input == null)
                {
                    Debug.LogError("PlayerShipDriver.Update: InputProvider.Instance is STILL null. Input cannot be processed.", this);
                    this.enabled = false;
                    return;
                }
            }

            // Check essential components are available.
            if (_playerCamera == null) return;

            // Prevent input processing if the game is paused.
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
            {
                SetEngagedTarget(null); // Ensure engagement clears on pause
                return;
            }

            UpdateControlState();

            if (FactionManager.Instance != null)
            {
                UpdateEngagement();
            }
            else
            {
                // FactionManager missing, ensure target cleared.
                SetEngagedTarget(null);
            }
        }

        #region IShipDriver Implementation

        /// <summary>
        /// Assigns the AtmosphericStarshipController that this driver will control.
        /// </summary>
        /// <param name="newController">The controller instance.</param>
        public void AssignController(AtmosphericStarshipController newController)
        {
            controller = newController;
            // Refresh references if needed
            if (controller != null)
            {
                // Ensure the controller is explicitly set to non-AI when assigned to the player driver.
                controller.isAI = false;
                if (properties == null) properties = controller.GetComponent<ShipProperties>();
                if (weaponController == null) weaponController = controller.GetComponent<ShipWeaponController>();
                if (shipIdentity == null) shipIdentity = controller.GetComponent<StarshipIdentity>();
            }
        }

        /// <summary>
        /// Releases control of the current AtmosphericStarshipController.
        /// </summary>
        public void ReleaseController()
        {
            controller = null;
            // If the controller is released, we should stop managing the view.
            CleanupView();
        }

        /// <summary>
        /// Determines if the driver is actively controlling the ship (e.g., not in a menu).
        /// </summary>
        /// <returns>True if active control is enabled.</returns>
        public bool IsActivelyControlling()
        {
            return isActivelyControlling;
        }

        /// <summary>
        /// Calculates the desired input state based on the player's current inputs.
        /// </summary>
        /// <returns>The desired ShipInputState for the current frame.</returns>
        public ShipInputState GetDesiredInputState()
        {
            // If the game is paused, return an empty input state to prevent ship movement.
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
            {
                // Ensure the weapon controller target is also cleared during pause
                if (weaponController != null) weaponController.SetTarget(null);
                return new ShipInputState();
            }

            if (input == null || properties == null)
            {
                if (weaponController != null) weaponController.SetTarget(null);
                return new ShipInputState();
            }

            ShipInputState state = new();

            // Read inputs that persist regardless of control mode.
            Vector2 movementInput = input.GetMovementInput();
            state.Thrust = movementInput.y;
            state.Vertical = input.GetVerticalMovement();
            state.Roll = input.GetRollInput();
            state.Boost = input.IsBoostHeld();
            state.FireBomb = input.IsFireBombPressed();
            state.FirePrimary = input.IsFirePrimaryHeld();
            state.FireSecondary = input.IsFireSecondaryHeld();

            // 1. Calculate Aim Position
            // This depends on whether a target is engaged.
            state.AimPosition = CalculateAimPosition();

            // 2. Set Engagement State for Weapons
            // This informs the WeaponController whether secondary weapons should be guided.
            state.IsTargetEngaged = IsTargetEngaged;

            // 3. Update Weapon Controller Target
            // Crucial: This ensures the WeaponController knows what to fire guided missiles at.
            if (weaponController != null)
            {
                weaponController.SetTarget(currentTarget);
            }

            // Rotational inputs (Pitch/Yaw)
            // Mouse input is read here because the cursor is locked (by GameStateManager), providing continuous delta input.
            Vector2 mouseInput = input.GetLookDelta();

            if (isActivelyControlling)
            {
                // Normal Flight Mode: Mouse and Keyboard control ship.

                // 1. Handle Keyboard Yaw (A/D)
                float keyboardYaw = movementInput.x;

                // 2. Handle Mouse Look
                float sensitivity = properties.mouseSensitivity;
                float mouseYaw = mouseInput.x * sensitivity;
                float mousePitch = mouseInput.y * sensitivity;

                // 3. Combine Yaw inputs (Prioritize keyboard if active)
                if (Mathf.Abs(keyboardYaw) > 0.01f)
                {
                    state.Yaw = keyboardYaw;
                }
                else
                {
                    state.Yaw = mouseYaw;
                }
                state.Pitch = mousePitch;
            }
            else
            {
                // Inactive state: No ship control.
                state.Pitch = 0f;
                state.Yaw = 0f;
            }

            return state;
        }

        /// <summary>
        /// Provides a formatted string describing the current key bindings.
        /// </summary>
        /// <returns>A multi-line string of controls.</returns>
        public string GetControlDescription()
        {
            // Updated control description for the new system.
            string dropCrateKey = "G";
            string firePrimaryKey = "LMB";
            string fireSecondaryKey = "F";
            string engageModeKey = "RMB";
            string pauseKey = "Esc";

            if (input != null)
            {
                try
                {
                    if (input.FireBombAction != null) dropCrateKey = input.FireBombAction.GetBindingDisplayString(0)?.ToUpper() ?? dropCrateKey;
                    if (input.FirePrimaryAction != null) firePrimaryKey = input.FirePrimaryAction.GetBindingDisplayString(0)?.ToUpper() ?? firePrimaryKey;
                    if (input.FireSecondaryAction != null) fireSecondaryKey = input.FireSecondaryAction.GetBindingDisplayString(0)?.ToUpper() ?? fireSecondaryKey;
                    if (input.AimModeAction != null) engageModeKey = input.AimModeAction.GetBindingDisplayString(0)?.ToUpper() ?? engageModeKey;
                    if (input.PauseAction != null) pauseKey = input.PauseAction.GetBindingDisplayString(0)?.ToUpper() ?? pauseKey;

                }
                catch (System.Exception) { }
            }

            return $"W/S: Thrust | A/D: Yaw | Q/E: Roll | Space/Ctrl: Vertical | L-Shift: Boost\n" +
                   $"Mouse: Pitch/Yaw\n" +
                   $"Hold [{engageModeKey}]: Engage Target (Auto-Aim/Missile Lock)\n" +
                   $"[{firePrimaryKey}]: Fire Primary | [{fireSecondaryKey}]: Fire Secondary | [{dropCrateKey}]: Drop Bomb\n" +
                   $"[{pauseKey}]: Pause";
        }
        #endregion

        #region View Management (Internal)

        /// <summary>
        /// Cleans up the view system when this driver is no longer controlling the ship.
        /// </summary>
        private void CleanupView()
        {
            // Only run cleanup if the view was previously initialized.
            if (!_isViewInitialized) return;

            // Ensure target engagement is cleared when view is cleaned up
            SetEngagedTarget(null);

            if (_starshipCamera != null)
            {
                // Disable the camera tracking if we lose control.
                _starshipCamera.enabled = false;
            }

            // Unregister from the GameStateManager.
            // This signals that the player is no longer active, allowing GameStateManager to release the cursor lock if appropriate.
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.UnregisterPlayerActivityProvider(this);
            }

            // Clear references.
            _playerCamera = null;
            _isViewInitialized = false;
        }

        // Ensure cleanup happens if the driver component is disabled (e.g., ship destroyed).
        void OnDisable()
        {
            CleanupView();
        }

        #endregion

        #region Input Processing and Unified Targeting Logic

        /// <summary>
        /// Updates the control state (e.g., checking for Free Look, although currently removed).
        /// </summary>
        private void UpdateControlState()
        {
            // Free Look functionality is currently removed. Player is always considered in control.
            isActivelyControlling = true;
        }

        /// <summary>
        /// Manages the target engagement logic based on player input and scene context.
        /// </summary>
        private void UpdateEngagement()
        {
            // The input previously used for "Aiming Mode" (RMB) now triggers "Engage".
            bool isEngageRequested = input.IsAimModeHeld();

            if (isEngageRequested && isActivelyControlling)
            {
                // Player is holding RMB and able to fly. Attempt to find a target.
                FindTargetToEngage();
            }
            else
            {
                // Player released RMB. Clear engagement.
                SetEngagedTarget(null);
            }

            // Additional check: If the target was destroyed or became inactive while engaged.
            if (IsTargetEngaged && (currentTarget == null || !currentTarget.activeInHierarchy || (currentTargetHealth != null && !currentTargetHealth.IsAlive)))
            {
                SetEngagedTarget(null);
            }
        }

        /// <summary>
        /// Scans the area within the targeting cone to find the closest valid hostile target.
        /// </summary>
        private void FindTargetToEngage()
        {
            // Basic checks
            if (shipIdentity == null || FactionManager.Instance == null || weaponController == null)
            {
                SetEngagedTarget(null);
                return;
            }

            // Ensure we have a valid fire point to use as the origin of the scan.
            if (weaponController.primaryFirePoints == null || weaponController.primaryFirePoints.Count == 0 || weaponController.primaryFirePoints[0] == null)
            {
                SetEngagedTarget(null);
                return;
            }

            // Define the targeting cone based on the ship's forward direction.
            Transform firePoint = weaponController.primaryFirePoints[0];
            Vector3 coneOrigin = firePoint.position;
            Vector3 coneDirection = transform.forward;

            // Get the list of active hostiles from the FactionManager.
            List<StarshipIdentity> hostiles = FactionManager.Instance.GetHostiles(shipIdentity.FactionID);

            if (hostiles.Count == 0)
            {
                SetEngagedTarget(null);
                return;
            }

            GameObject bestTarget = null;
            float closestDistanceSqr = float.MaxValue;

            // Iterate through hostiles to find the closest one within the cone.
            foreach (var hostile in hostiles)
            {
                // Validate the hostile target.
                if (hostile == null || !hostile.IsAlive || hostile.gameObject == this.gameObject)
                {
                    continue;
                }

                Vector3 targetPosition = hostile.CachedTransform.position;
                Vector3 directionToTarget = targetPosition - coneOrigin;
                float distanceSqr = directionToTarget.sqrMagnitude;

                // Check if this target is closer than the current best target.
                if (distanceSqr < closestDistanceSqr)
                {
                    // Calculate the angle between the cone direction and the target.
                    float angle = Vector3.Angle(coneDirection, directionToTarget.normalized);

                    // Check if the target is within the defined cone angle.
                    if (angle <= targetingConeAngle)
                    {
                        closestDistanceSqr = distanceSqr;
                        bestTarget = hostile.gameObject;
                    }
                }
            }

            // Set the result.
            SetEngagedTarget(bestTarget);
        }

        /// <summary>
        /// Helper method to manage the engagement state change and the target visualizer activation/deactivation.
        /// </summary>
        /// <param name="target">The new target GameObject, or null to disengage.</param>
        private void SetEngagedTarget(GameObject target)
        {
            if (currentTarget == target)
            {
                // Target hasn't changed, no need to update visualizers or state.
                return;
            }

            // Handle Disengagement of the Previous Target
            if (currentTarget != null)
            {
                // Deactivate the visualizer on the old target using the cached HealthComponent.
                if (currentTargetHealth != null && currentTargetHealth.playerTargetVisualizer != null)
                {
                    // Check if the GameObject still exists before accessing it (it might have been destroyed)
                    if (currentTargetHealth.playerTargetVisualizer)
                    {
                        currentTargetHealth.playerTargetVisualizer.SetActive(false);
                    }
                }
            }

            // Handle Engagement of the New Target
            currentTarget = target;

            if (currentTarget != null)
            {
                IsTargetEngaged = true;
                // Cache the HealthComponent and activate the visualizer.
                currentTargetHealth = currentTarget.GetComponent<HealthComponent>();
                if (currentTargetHealth != null && currentTargetHealth.playerTargetVisualizer != null)
                {
                    currentTargetHealth.playerTargetVisualizer.SetActive(true);
                }
            }
            else
            {
                IsTargetEngaged = false;
                currentTargetHealth = null;
            }
        }

        /// <summary>
        /// Calculates the world-space position the player is aiming at, used for weapon targeting.
        /// </summary>
        /// <returns>The aim position vector.</returns>
        private Vector3 CalculateAimPosition()
        {
            // 1. If engaged, the aim position is the exact position of the target.
            // This makes unguided weapons automatically track the engaged target.
            if (IsTargetEngaged && currentTarget != null)
            {
                return currentTarget.transform.position;
            }

            // 2. If not engaged, perform the standard center-screen raycast.
            if (_playerCamera == null)
            {
                // Fallback if camera is missing: aim forward from the ship
                return transform.position + transform.forward * maxAimDistance;
            }

            // Create a ray from the center of the screen.
            Vector2 screenCenter = new(Screen.width / 2f, Screen.height / 2f);
            Ray ray = _playerCamera.ScreenPointToRay(screenCenter);

            // We simply project the ray far into the distance.
            return ray.origin + ray.direction * maxAimDistance;
        }

        #endregion
    }
}