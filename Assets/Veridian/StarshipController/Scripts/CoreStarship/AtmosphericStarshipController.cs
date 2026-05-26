using System;
using UnityEngine;
using Veridian.Starship.Weapons;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Defines the available movement modes for the starship controller, balancing fidelity and performance.
    /// </summary>
    public enum StarshipMovementMode
    {
        /// <summary>
        /// Full Rigidbody physics simulation (high fidelity, higher CPU cost).
        /// </summary>
        Physics,
        /// <summary>
        /// Kinematic interpolation (low fidelity, near-zero CPU cost, optimized for distant AI).
        /// </summary>
        Lerp
    }

    /// <summary>
    /// The core physics controller for atmospheric starship movement.
    /// It processes input from an IShipDriver and applies forces for thrust, rotation, aerodynamics, boosting, and environmental effects.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ShipProperties))]
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(ShipWeaponController))]
    public class AtmosphericStarshipController : MonoBehaviour
    {
        /// <summary>
        /// Invoked when G-Force is calculated. Passes the new G-force value.
        /// </summary>
        public event Action<float> OnGForceUpdate;

        [Header("Configuration")]
        [Tooltip("If true, this controller is typically managed by AI. If false, it is typically the player's ship. This affects Rigidbody interpolation settings for performance/smoothness.")]
        public bool isAI = true;

        [Header("Performance Optimization")]
        [Tooltip("The current movement mode. Physics uses the Rigidbody for high-fidelity simulation. Lerp uses kinematic movement for low-fidelity, optimized performance.")]
        [SerializeField] private StarshipMovementMode _currentMovementMode = StarshipMovementMode.Physics;

        private ShipProperties properties;
        private Rigidbody rb;
        private ShipSensorySystem sensorySystem;

        [Header("Driver Configuration")]
        [Tooltip("The default driver component (must implement IShipDriver). If null, the controller will attempt to find one on the GameObject during Awake.")]
        public MonoBehaviour defaultDriverComponent;

        private IShipDriver currentDriver;


        // Public properties for external reading (HUD, Camera)
        /// <summary>
        /// The number of crates currently carried by the ship (if applicable to the game mode).
        /// </summary>
        public int CurrentCrates { get; private set; }

        /// <summary>
        /// Indicates if the assigned driver is actively controlling the ship (e.g., the player is not in a menu or free-look mode).
        /// </summary>
        public bool IsControllingShip { get; private set; } = true;

        /// <summary>
        /// The current speed of the ship in meters per second.
        /// In Physics mode, this is the Rigidbody velocity magnitude. In LERP mode, this is the simulated velocity magnitude.
        /// </summary>
        public float CurrentSpeed => _currentMovementMode == StarshipMovementMode.Physics
            ? (rb != null && !rb.isKinematic ? rb.linearVelocity.magnitude : 0f)
            : _lerpVelocity.magnitude;

        /// <summary>
        /// Helper to determine if the ship is currently operating in an atmosphere (where Physics.gravity is non-zero).
        /// </summary>
        private bool IsInAtmosphere => Physics.gravity.sqrMagnitude > Mathf.Epsilon;


        /// <summary>
        /// Reference to the ship's HealthComponent.
        /// </summary>
        public HealthComponent Health { get; private set; }
        /// <summary>
        /// Reference to the ship's ShipWeaponController.
        /// </summary>
        public ShipWeaponController Weapons { get; private set; }
        public float CurrentThrustInput => currentInputState.Thrust;
        public float CurrentBoost => currentBoost;
        public float MaxBoost => properties != null ? properties.maxBoost : 0f;
        /// <summary>
        /// The current smoothed thrust level (ramped value) being applied.
        /// </summary>
        public float CurrentThrustLevel { get; private set; }
        /// <summary>
        /// The current smoothed vertical thrust level (ramped value) being applied.
        /// </summary>
        public float CurrentVerticalThrustLevel { get; private set; }
        /// <summary>
        /// The current multiplier applied to thrust and max speed due to boosting.
        /// </summary>
        public float CurrentBoostMultiplier { get; private set; } = 1f;
        public float YawInput => currentInputState.Yaw;
        public float PitchInput => currentInputState.Pitch;
        /// <summary>
        /// The current G-force experienced by the ship.
        /// </summary>
        public float CurrentGForce { get; private set; }
        public float ManualRollInput { get; private set; }
        public bool IsTryingToBoost => isTryingToBoost;

        // Component accessors
        /// <summary>
        /// Reference to the ship's ShipSensorySystem.
        /// </summary>
        public ShipSensorySystem SensorySystem { get; private set; }
        /// <summary>
        /// Reference to the ship's ShipProperties.
        /// </summary>
        public ShipProperties Properties => properties;
        /// <summary>
        /// Reference to the ship's Rigidbody component.
        /// </summary>
        public Rigidbody RigidbodyComponent => rb;
        /// <summary>
        /// The current operational movement mode of the controller.
        /// </summary>
        public StarshipMovementMode CurrentMovementMode => _currentMovementMode;

        // Internal state variables
        private Vector3 lastVelocity;
        private float currentBoost;
        private float boostRechargeCooldown;
        private bool isTryingToBoost;
        private float currentGForceGovernor = 1.0f; // The current G-force restriction multiplier (1.0 = no restriction)

        // Stores the current desired inputs from the driver
        private ShipInputState currentInputState;

        // LERP state variables
        private Vector3 _lerpVelocity; // Simulated velocity used in LERP mode.
        private float _lerpAltitudeCheckTimer = 0f;
        private const float LERP_ALTITUDE_CHECK_INTERVAL = 1.0f; // Check altitude infrequently.


        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            properties = GetComponent<ShipProperties>();
            SensorySystem = GetComponent<ShipSensorySystem>();
            Health = GetComponent<HealthComponent>();
            Weapons = GetComponent<ShipWeaponController>();
            sensorySystem = GetComponent<ShipSensorySystem>();

            if (properties == null)
            {
                Debug.LogError("AtmosphericStarshipController requires a ShipProperties component.", this);
                this.enabled = false;
                return;
            }

            InitializeController();
        }

        private void InitializeController()
        {
            InitializeDriver();

            properties.LoadProfile();
            if (SensorySystem != null)
            {
                SensorySystem.LoadSettings();
            }

            // Initialize state variables.
            currentBoost = properties.maxBoost;
            CurrentBoostMultiplier = 1f;
            currentGForceGovernor = 1f;
            // Stagger altitude checks
            _lerpAltitudeCheckTimer = UnityEngine.Random.Range(0f, LERP_ALTITUDE_CHECK_INTERVAL);
        }

        void OnEnable()
        {
            // Ensure the movement mode settings and Rigidbody state are applied correctly when the component is enabled.
            SetMovementMode(_currentMovementMode, true);
        }

        void OnDisable()
        {
            // When deactivated (e.g., by FactionManager), it is expected to make the Rigidbody kinematic
            // to prevent unintended physics interactions while inactive.
            if (rb)
            {
                // Ensure residual forces are cleared before making it kinematic.
                ResetAndClearPhysicsState();
                rb.isKinematic = true;
            }
        }

        /// <summary>
        /// Immediately halts all motion (linear and angular velocity) of the Rigidbody and resets internal movement tracking.
        /// Essential for ensuring a clean state when respawning or reactivating a ship.
        /// </summary>
        public void ResetAndClearPhysicsState()
        {
            if (rb != null)
            {
                // Only attempt to set velocity if the Rigidbody is not kinematic (e.g., if in Physics mode).
                if (!rb.isKinematic)
                {
                    // Using linearVelocity and angularVelocity for Unity 6 (2025.1+) compatibility.
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            // Also clear the internal velocity trackers.
            _lerpVelocity = Vector3.zero;
            lastVelocity = Vector3.zero;

            // Resetting flight states is also advisable for a full physics reset
            CurrentThrustLevel = 0f;
            CurrentVerticalThrustLevel = 0f;
            CurrentBoostMultiplier = 1f;
            CurrentGForce = 0f;
        }


        private void InitializeDriver()
        {
            if (currentDriver != null) return;

            IShipDriver driverToAssign = null;

            // 1. Try to use the Inspector-assigned driver
            if (defaultDriverComponent != null)
            {
                driverToAssign = defaultDriverComponent as IShipDriver;
                if (driverToAssign == null)
                {
                    Debug.LogError("The assigned Default Driver Component does not implement IShipDriver.", this);
                }
            }

            // 2. If Inspector assignment failed or was null, search the GameObject
            driverToAssign ??= GetComponent<IShipDriver>();

            if (driverToAssign != null)
            {
                SetDriver(driverToAssign);
            }
            else
            {
                Debug.LogWarning($"AtmosphericStarshipController on {gameObject.name} initialized without an IShipDriver.", this);
            }
        }

        /// <summary>
        /// Public method to switch the active driver (e.g., switching from Player to AI).
        /// </summary>
        /// <param name="newDriver">The new IShipDriver instance to assign.</param>
        public void SetDriver(IShipDriver newDriver)
        {
            currentDriver?.ReleaseController();

            currentDriver = newDriver;

            if (currentDriver != null)
            {
                currentDriver.AssignController(this);
            }
            else
            {
                // If no driver is present, ensure the input state is zeroed out.
                currentInputState = new ShipInputState();
            }

            // Reconfigure Rigidbody when the driver changes, as the isAI flag might have changed.
            if (Application.isPlaying && this.enabled)
            {
                ConfigureRigidbody();
            }
        }

        /// <summary>
        /// Gets the currently active driver instance.
        /// </summary>
        /// <returns>The active IShipDriver.</returns>
        public IShipDriver GetDriver() => currentDriver;

        void OnValidate()
        {
            if (Application.isPlaying && rb != null && properties != null && this.enabled)
            {
                bool isActuallyKinematic = rb.isKinematic;
                StarshipMovementMode actualMode = isActuallyKinematic ? StarshipMovementMode.Lerp : StarshipMovementMode.Physics;

                // If the desired mode (_currentMovementMode) differs from the actual mode, apply the change.
                if (_currentMovementMode != actualMode)
                {
                    SetMovementMode(_currentMovementMode);
                }

                ConfigureRigidbody();
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb == null || properties == null) return;
            properties.LoadProfile();

            rb.mass = properties.mass;

            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                rb.useGravity = properties.useGravity;
                // Set built-in linear damping (drag) to 0 so our custom aerodynamic drag system has full control.
                // Using linearDamping for Unity 6 (2025.1+) compatibility.
                rb.linearDamping = 0f;
                rb.angularDamping = properties.angularDrag;

                // Enable Rigidbody interpolation for the player's ship.
                // This ensures the Transform is smoothly updated between FixedUpdate calls, allowing the camera (in LateUpdate) to follow smoothly.
                if (!isAI)
                {
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                else
                {
                    // AI ships generally do not need interpolation, saving performance.
                    rb.interpolation = RigidbodyInterpolation.None;
                }
            }
        }

        /// <summary>
        /// Switches the controller between Physics (Rigidbody simulation) and LERP (kinematic movement) modes.
        /// </summary>
        /// <param name="newMode">The desired movement mode.</param>
        /// <param name="force">If true, reapplies the settings even if the mode hasn't changed.</param>
        public void SetMovementMode(StarshipMovementMode newMode, bool force = false)
        {
            if (_currentMovementMode == newMode && !force) return;

            // Capture the state before the switch
            StarshipMovementMode previousMode = _currentMovementMode;
            _currentMovementMode = newMode;

            // Only apply Rigidbody changes if the component is currently enabled.
            bool shouldApplyRigidbodyChanges = this.enabled;


            if (newMode == StarshipMovementMode.Lerp)
            {
                // Transitioning into LERP
                if (previousMode == StarshipMovementMode.Physics && shouldApplyRigidbodyChanges && rb != null && !rb.isKinematic)
                {
                    _lerpVelocity = rb.linearVelocity;
                }

                if (shouldApplyRigidbodyChanges && rb != null)
                {
                    rb.isKinematic = true;
                }

                // Disable non-essential components
                if (sensorySystem != null)
                {
                    sensorySystem.enabled = false;
                }

            }
            else // newMode == StarshipMovementMode.Physics
            {
                if (shouldApplyRigidbodyChanges && rb != null)
                {
                    rb.isKinematic = false;
                }

                ConfigureRigidbody();

                // Transitioning into Physics
                if (previousMode == StarshipMovementMode.Lerp && shouldApplyRigidbodyChanges && rb != null)
                {
                    rb.linearVelocity = _lerpVelocity;
                    rb.angularVelocity = Vector3.zero;
                }

                if (sensorySystem != null)
                {
                    sensorySystem.enabled = true;
                }

            }
        }

        void Update()
        {
            // Check if ship is alive
            // Note: The FactionManager now controls the active state of the GameObject. If the ship is destroyed,
            // this script will likely be disabled along with the GameObject.
            if (Health != null && !Health.IsAlive)
            {
                // If the ship is destroyed but somehow still active (e.g., crash sequence if implemented),
                // ensure we are in physics mode.
                if (_currentMovementMode == StarshipMovementMode.Lerp)
                {
                    SetMovementMode(StarshipMovementMode.Physics);
                }
                // Stop processing further updates if destroyed.
                return;
            }
            //possibly move to fixedupdate
            UpdateInputFromDriver();

            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                // These systems are only relevant for the physics simulation.
                HandleBoostLogic();
                HandleRamping();
                HandleWeapons();
            }
            else // LERP Mode
            {
                // Bypasses expensive calculations.
                // Reset physics-specific state variables when in LERP mode.
                CurrentThrustLevel = 0;
                CurrentVerticalThrustLevel = 0;
                CurrentBoostMultiplier = 1f;
                isTryingToBoost = false;
                currentGForceGovernor = 1.0f; // Reset governor in LERP mode

                // Execute simplified flight behavior based on AI inputs.
                UpdateLerpMovement();
            }
        }

        void FixedUpdate()
        {

            /*
                        if (Health != null && !Health.IsAlive)
                        {
                            if (_currentMovementMode == StarshipMovementMode.Lerp)
                            {
                                SetMovementMode(StarshipMovementMode.Physics);
                            }
                            return; // Stop all physics processing
                        }


                        UpdateInputFromDriver(); 
            */
            // Ensure physics updates only run in Physics mode.
            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                // Calculate G-Force first, as it impacts the subsequent force applications.
                CalculateGForce();
                // Calculate the G-force limiter based on the new G-force value.
                CalculateGForceLimiter();

                ApplyThrust();
                ApplyGroundEffect();
                ApplyRotation();
                ApplyAerodynamicDrag();
                ClampVelocity();
            }
        }

        private void UpdateInputFromDriver()
        {
            if (currentDriver != null)
            {
                currentInputState = currentDriver.GetDesiredInputState();
                IsControllingShip = currentDriver.IsActivelyControlling();
                ManualRollInput = currentInputState.Roll;
            }
            else
            {
                currentInputState = new ShipInputState();
                IsControllingShip = false;
                ManualRollInput = 0f;
            }
        }

        /// <summary>
        /// Updates the ship's position and rotation using simplified kinematic movement (LERP mode).
        /// </summary>
        private void UpdateLerpMovement()
        {
            // 1. Apply Rotation based on inputs
            float rotationRate = properties.lerpRotationSpeed;

            // Pitch input is typically inverted in the physics system, so we invert it here too for consistency.
            float pitch = -currentInputState.Pitch * rotationRate * Time.deltaTime;
            float yaw = currentInputState.Yaw * rotationRate * Time.deltaTime;
            float roll = currentInputState.Roll * rotationRate * Time.deltaTime;

            // Apply the rotation locally.
            if (Mathf.Abs(pitch) > 0.001f || Mathf.Abs(yaw) > 0.001f || Mathf.Abs(roll) > 0.001f)
            {
                Quaternion deltaRotation = Quaternion.Euler(pitch, yaw, roll);
                transform.rotation *= deltaRotation;
            }

            // 2. Calculate Desired Speeds
            float forwardSpeed = currentInputState.Thrust * properties.lerpCruiseSpeed;
            float verticalSpeed = currentInputState.Vertical * properties.lerpVerticalSpeed;

            // 3. Simplified Altitude Maintenance
            _lerpAltitudeCheckTimer -= Time.deltaTime;
            if (_lerpAltitudeCheckTimer <= 0f)
            {
                _lerpAltitudeCheckTimer = LERP_ALTITUDE_CHECK_INTERVAL;

                // Perform a simple raycast downwards.
                float checkDistance = properties.lerpCruisingAltitude * 1.5f;
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkDistance, properties.lerpGroundMask))
                {
                    float altitude = hit.distance;
                    if (altitude < properties.lerpCruisingAltitude)
                    {
                        // We are below cruising altitude, calculate corrective upward speed.
                        float proximityRatio = 1.0f - (altitude / properties.lerpCruisingAltitude);
                        float requiredVerticalSpeed = proximityRatio * properties.lerpVerticalSpeed;
                        verticalSpeed = Mathf.Max(verticalSpeed, requiredVerticalSpeed);
                    }
                }
            }

            // 4. Move the ship
            Vector3 forwardMovement = forwardSpeed * Time.deltaTime * transform.forward;
            Vector3 verticalMovement = Time.deltaTime * verticalSpeed * transform.up;
            Vector3 totalMovement = forwardMovement + verticalMovement;
            transform.position += totalMovement;

            // 5. Update simulated velocity and G-Force for readouts and feedback
            if (Time.deltaTime > 0)
            {
                _lerpVelocity = totalMovement / Time.deltaTime;
                // Calculate G-force based on the change in _lerpVelocity
                CurrentGForce = (_lerpVelocity - lastVelocity).magnitude / (Time.deltaTime * 9.81f);
                lastVelocity = _lerpVelocity;

                // Invoke the event in LERP mode as well.
                OnGForceUpdate?.Invoke(CurrentGForce);
            }
        }

        /// <summary>
        /// Manages the consumption, recharge, and cooldown logic for the boost system.
        /// </summary>
        private void HandleBoostLogic()
        {
            isTryingToBoost = currentInputState.Boost && currentBoost > 0f && CurrentThrustLevel > 0.1f;

            if (isTryingToBoost)
            {
                currentBoost -= properties.boostDrainRate * Time.deltaTime;
                boostRechargeCooldown = properties.boostRechargeDelay;
            }
            else
            {
                if (boostRechargeCooldown > 0f)
                {
                    boostRechargeCooldown -= Time.deltaTime;
                }
                else if (currentBoost < properties.maxBoost)
                {
                    currentBoost += properties.boostRechargeRate * Time.deltaTime;
                }
            }
            currentBoost = Mathf.Clamp(currentBoost, 0f, properties.maxBoost);
        }

        /// <summary>
        /// Smoothly ramps thrust levels and the boost multiplier towards the desired input values over time.
        /// </summary>
        private void HandleRamping()
        {
            // Ramp up or down the main and vertical thrust based on input
            float targetThrust = currentInputState.Thrust;
            float targetVerticalThrust = currentInputState.Vertical;

            CurrentThrustLevel = Mathf.Lerp(CurrentThrustLevel, targetThrust, Time.deltaTime * properties.thrustRampUpSpeed);
            CurrentVerticalThrustLevel = Mathf.Lerp(CurrentVerticalThrustLevel, targetVerticalThrust, Time.deltaTime * properties.thrustRampUpSpeed);

            // Default to a multiplier of 1 (no boost effect)
            float targetBoostMultiplier = 1f;

            // Check if the driver is attempting to boost
            if (isTryingToBoost)
            {
                // 1. Get the ship's speed along its forward axis.
                float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

                // 2. Use 0 speed for the curve if we are stationary or reversing.
                float speedForCurve = Mathf.Max(0f, forwardSpeed);

                // 3. Normalize the speed to a 0-1 range based on maxSpeed.
                float normalizedSpeed = Mathf.Clamp01(speedForCurve / properties.maxSpeed);

                // 4. Get the modifier value from the animation curve based on the normalized speed.
                float curveModifier = properties.boostSpeedModifierCurve.Evaluate(normalizedSpeed);

                // 5. Calculate the final target multiplier by applying the curve's modifier to the base multiplier.
                targetBoostMultiplier = properties.boostMultiplier * curveModifier;
            }

            // Smoothly ramp the current boost multiplier towards the calculated target.
            CurrentBoostMultiplier = Mathf.Lerp(CurrentBoostMultiplier, targetBoostMultiplier, Time.deltaTime * properties.boostRampUpSpeed);
        }

        /// <summary>
        /// Applies forward and vertical thrust forces to the Rigidbody based on the current ramped levels, boost multiplier, and G-force governor.
        /// </summary>
        private void ApplyThrust()
        {
            float effectiveGovernor = currentGForceGovernor;

            if (Mathf.Abs(CurrentThrustLevel) > 0.01f)
            {
                float power = CurrentThrustLevel > 0 ? properties.forwardThrustPower : properties.reverseThrustPower;
                // Apply thrust along the forward axis (+Z)
                rb.AddForce(CurrentBoostMultiplier * CurrentThrustLevel * effectiveGovernor * power * transform.forward, ForceMode.Force);
            }
            if (Mathf.Abs(CurrentVerticalThrustLevel) > 0.01f)
            {
                rb.AddForce(CurrentVerticalThrustLevel * effectiveGovernor * properties.verticalThrustPower * transform.up, ForceMode.Force);
            }
        }

        /// <summary>
        /// Applies an upward force when close to the ground, simulating a repulsor lift or ground effect cushion.
        /// </summary>
        private void ApplyGroundEffect()
        {
            // Exit if we don't have a sensory system or if it's disabled.
            if (sensorySystem == null || !sensorySystem.enabled) return;

            float maxAltitude = properties.groundEffectMaxAltitude;
            if (maxAltitude <= 0f) return;

            // Read the altitude directly from the sensory system.
            float altitude = sensorySystem.AltitudeAboveGround;

            // Only apply the effect if the ground is detected within the effect's max range.
            if (altitude < maxAltitude)
            {
                float proximityPercent = 1.0f - (altitude / maxAltitude);
                float forceMultiplier = properties.groundEffectCurve.Evaluate(proximityPercent);
                float upwardForce = properties.groundEffectMaxForce * forceMultiplier;
                rb.AddForce(Vector3.up * upwardForce, ForceMode.Force);
            }
        }

        /// <summary>
        /// Applies custom aerodynamic drag forces based on the environment profile, ship properties, and current velocity components (forward vs. sideways).
        /// </summary>
        private void ApplyAerodynamicDrag()
        {
            if (rb.linearVelocity.sqrMagnitude < 0.01f) return;

            // Note: This assumes an EnvironmentManager singleton exists to provide environmental properties.
            if (EnvironmentManager.Instance == null || EnvironmentManager.Instance.CurrentProfile == null) return;

            EnvironmentProfileSO env = EnvironmentManager.Instance.CurrentProfile;
            float shipModifier = properties.aerodynamicDragModifier;
            Vector3 velocity = rb.linearVelocity;
            Vector3 forwardDir = transform.forward;

            Vector3 forwardVelocity = Vector3.Project(velocity, forwardDir);
            Vector3 sidewaysVelocity = velocity - forwardVelocity;
            Vector3 totalDragForce = Vector3.zero;

            // Calculate forward drag
            if (forwardVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 linearForwardDrag = env.baseForwardLinearDrag * shipModifier * -forwardVelocity;
                Vector3 quadraticForwardDrag = env.baseForwardQuadraticDrag * forwardVelocity.sqrMagnitude * shipModifier * -forwardVelocity.normalized;
                totalDragForce += linearForwardDrag + quadraticForwardDrag;
            }

            // Calculate sideways drag
            if (sidewaysVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 linearSidewaysDrag = env.baseSidewaysLinearDrag * shipModifier * -sidewaysVelocity;
                Vector3 quadraticSidewaysDrag = env.baseSidewaysQuadraticDrag * shipModifier * sidewaysVelocity.sqrMagnitude * -sidewaysVelocity.normalized;
                totalDragForce += linearSidewaysDrag + quadraticSidewaysDrag;
            }

            rb.AddForce(totalDragForce, ForceMode.Force);
        }

        /// <summary>
        /// Applies rotational torque to the Rigidbody based on input, modulated by the speed-based maneuverability curve and the G-force limiter.
        /// </summary>
        private void ApplyRotation()
        {
            // 1. Calculate maneuverability multiplier based on speed
            float currentSpeed = rb.linearVelocity.magnitude;
            float maxSpeed = properties.maxSpeed;
            float normalizedSpeed = (maxSpeed > 0) ? currentSpeed / maxSpeed : 0f;
            float maneuverabilityMultiplier = properties.maneuverabilityCurve.Evaluate(Mathf.Clamp01(normalizedSpeed));

            // 2. Get the G-force governor multiplier
            float effectiveGovernor = currentGForceGovernor;

            // 3. Calculate torques
            // Roll is unaffected by speed-based maneuverability, but is affected by the G-force governor.
            float rollTorque = currentInputState.Roll * properties.rollPower * effectiveGovernor;
            float pitchTorque = 0f;
            float yawTorque = 0f;

            // Apply rotational inputs only if the driver indicates active control.
            if (IsControllingShip)
            {
                // Yaw is affected by both speed maneuverability and the G-force governor.
                yawTorque = currentInputState.Yaw * properties.yawPower * maneuverabilityMultiplier * effectiveGovernor;

                // Pitch input is typically inverted (pull back/negative input to go up)
                pitchTorque = -currentInputState.Pitch * properties.pitchPower * maneuverabilityMultiplier * effectiveGovernor;
            }

            // 4. Apply torques
            Vector3 torque = new(pitchTorque, yawTorque, rollTorque);
            rb.AddRelativeTorque(torque, ForceMode.Force);

            // Damping
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, properties.rotationDamping * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Clamps the Rigidbody's linear velocity to the maximum allowed speed, considering the current boost multiplier.
        /// </summary>
        private void ClampVelocity()
        {
            if (rb.linearVelocity.sqrMagnitude <= 0.01f) return;

            float currentMaxSpeed = properties.maxSpeed * CurrentBoostMultiplier;
            if (rb.linearVelocity.magnitude > currentMaxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
            }
        }

        /// <summary>
        /// Calculates the current G-force experienced by the ship based on the change in velocity (acceleration).
        /// </summary>
        private void CalculateGForce()
        {
            Vector3 currentVelocity = rb.linearVelocity;

            // Prevent division by zero if physics time step is 0
            if (Time.fixedDeltaTime > 0)
            {
                Vector3 currentAcceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;
                CurrentGForce = currentAcceleration.magnitude / 9.81f;
            }
            lastVelocity = currentVelocity;

            OnGForceUpdate?.Invoke(CurrentGForce);
        }

        /// <summary>
        /// Calculates the G-force governor multiplier to create a soft limit on maneuverability and thrust when G-forces exceed the maximum threshold.
        /// </summary>
        private void CalculateGForceLimiter()
        {
            if (!properties.useGForceLimiter || properties.maxOverallGForce <= 0)
            {
                currentGForceGovernor = 1.0f;
                return;
            }

            float maxG = properties.maxOverallGForce;

            if (CurrentGForce <= maxG)
            {
                // If G-force is within limits, gradually release the governor.
                currentGForceGovernor = Mathf.Lerp(currentGForceGovernor, 1.0f, Time.fixedDeltaTime * 5.0f);
            }
            else
            {
                // G-force exceeds the limit, so calculate the restriction.
                float overloadRatio = CurrentGForce / maxG;

                // Use an inverse function for a smooth, asymptotic limit.
                float targetGovernor = 1.0f / overloadRatio;

                currentGForceGovernor = targetGovernor;
            }

            // Ensure the governor is clamped to prevent complete loss of control.
            currentGForceGovernor = Mathf.Clamp(currentGForceGovernor, 0.05f, 1.0f);
        }

        /// <summary>
        /// Handles the firing of weapons by interfacing with the ShipWeaponController based on the current input state.
        /// </summary>
        private void HandleWeapons()
        {
            if (Weapons == null) return;

            Vector3 aimPosition = currentInputState.AimPosition;
            bool isEngaged = currentInputState.IsTargetEngaged;

            if (currentInputState.FirePrimary)
            {
                // Primary weapons always use the aimPosition (which tracks the target if engaged).
                Weapons.FirePrimary(aimPosition);
            }

            if (currentInputState.FireSecondary)
            {
                Weapons.FireSecondary(aimPosition, isEngaged);
            }
            if (currentInputState.FireBomb)
            {
                Weapons.FireBomb();
            }
        }
    }
}