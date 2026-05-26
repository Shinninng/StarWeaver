using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    // (UtilityPIDController implementation remains identical)
    public class UtilityPIDController
    {
        public float Kp, Ki, Kd;
        public float MaxIntegral = 1.0f;
        private float integral;
        private float previousError;
        private bool isFirstUpdate = true;

        public UtilityPIDController(float p, float i, float d)
        {
            Kp = p;
            Ki = i;
            Kd = d;
        }

        public float Update(float error, float deltaTime)
        {
            if (deltaTime <= 0)
                return 0;
            float P = Kp * error;
            integral += error * deltaTime;
            integral = Mathf.Clamp(integral, -MaxIntegral, MaxIntegral);
            float I = Ki * integral;
            float derivative;
            if (isFirstUpdate)
            {
                derivative = 0;
                isFirstUpdate = false;
            }
            else
            {
                derivative = (error - previousError) / deltaTime;
            }
            float D = Kd * derivative;
            previousError = error;
            return P + I + D;
        }

        public void Reset()
        {
            integral = 0;
            previousError = 0;
            isFirstUpdate = true;
        }
    }

    [RequireComponent(typeof(AtmosphericStarshipController))]
    [RequireComponent(typeof(ShipProperties))]
    public class SimpleAiPilot : MonoBehaviour, IShipDriver, IRespawnResettable // Implemented IRespawnResettable
    {
        [Header("Configuration")]
        public FlightPersonalitySO CurrentPersonality;

        [Header("Debug Info")]
        [SerializeField]
        private string _currentBehaviorName = "None";

        [SerializeField]
        private float _alignmentAngle;

        [SerializeField]
        private bool _isAligned;

        private ShipSensorySystem _sensorySystem;
        private AtmosphericStarshipController _controller;
        private Rigidbody _rb;
        private ShipProperties _properties;
        private ShipInputState _inputState;
        private Transform _transform;
        private ISimpleAiBehavior _currentBehavior;

        private UtilityPIDController _pitchController;
        private UtilityPIDController _yawController;
        private UtilityPIDController _rollController;

        private AiWeaponController _weaponController;

#if UNITY_EDITOR
        private float _debugUpdateTimer;
#endif
        public AiBrain Brain { get; private set; }
        public ShipProperties Properties => _properties;
        public Transform Transform => _transform;
        public ISimpleAiBehavior CurrentBehavior => _currentBehavior;

        public Rigidbody Rigidbody => _rb;

        // NEW: Initialization State Flag
        private bool _hasBeenInitialized = false;

        public void SetBrain(AiBrain brain)
        {
            Brain = brain;
        }

        // MODIFIED: Implement OnEnable to handle state reset upon activation/respawn.
        void OnEnable()
        {
            // Check the initialization flag.
            // If this is the first time or a respawn, perform a full reset.
            if (!_hasBeenInitialized)
            {
                ResetPilotState();
            }
            // If this is a distance-based reactivation, skip the reset, preserving the behavior and PID states.
        }

        // NEW METHOD
        void Start()
        {
            // Mark as initialized after the first full setup.
            _hasBeenInitialized = true;
        }

        // NEW METHOD (IRespawnResettable implementation)
        public void PrepareForRespawn()
        {
            // Reset the flag so the next OnEnable triggers a full ResetPilotState.
            _hasBeenInitialized = false;
        }

        // NEW METHOD
        /// <summary>
        /// Resets the pilot's internal tactical state, including PID controllers and the current behavior reference.
        /// </summary>
        private void ResetPilotState()
        {
            // Clear the current behavior. The AiBrain will assign a new one shortly after OnEnable.
            // We use SetBehavior(null) as it handles the OnExit call and PID resets internally.
            SetBehavior(null);

            // Clear the input state buffer.
            _inputState = new ShipInputState();

            // Reset debug info
            _alignmentAngle = 0;
            _isAligned = false;
        }

        #region IShipDriver Implementation

        public void AssignController(AtmosphericStarshipController controller)
        {
            _controller = controller;
            _rb = controller.RigidbodyComponent;
            _properties = controller.Properties; // Use the accessor from the controller
            _transform = controller.transform;
            _sensorySystem = controller.SensorySystem; // Use the accessor from the controller

            // NEW: Get the AiWeaponController associated with this ship
            _weaponController = controller.GetComponent<AiWeaponController>();

            if (_properties == null || CurrentPersonality == null)
            {
                Debug.LogError("SimpleAiPilot requires ShipProperties and a FlightPersonalitySO.", this);
                this.enabled = false;
                return;
            }

            InitializePID();

            // If a behavior was already assigned (e.g., before the controller was ready), initialize it now.
            if (_currentBehavior != null)
            {
                _currentBehavior.Initialize(this);
            }
        }

        // (The rest of the SimpleAiPilot class remains unchanged, implementation omitted for brevity)
        public void ReleaseController()
        {
            _controller = null;
            _rb = null;
            _properties = null;
            _transform = null;
            _sensorySystem = null;
            _weaponController = null; // NEW: Release weapon controller reference
        }

        public ShipInputState GetDesiredInputState()
        {
            if (_controller == null)
                return new ShipInputState();

            // 1. Calculate flight inputs based on behavior
            UpdateAi();

            // 2. Aggregate weapon inputs from the AiWeaponController
            AggregateWeaponInputs();

            return _inputState;
        }

        public bool IsActivelyControlling()
        {
            return this.enabled && CurrentPersonality != null;
        }

        public string GetControlDescription()
        {
            return $"Simple AI Pilot (Behavior: {_currentBehaviorName}, Personality: {CurrentPersonality?.name ?? "None"})";
        }

        #endregion

        #region Initialization and Behavior Management

        void OnValidate()
        {
            if (Application.isPlaying && _controller != null)
            {
                InitializePID();
            }
        }

        private void InitializePID()
        {
            if (CurrentPersonality == null)
                return;

            if (_pitchController == null)
            {
                _pitchController = new UtilityPIDController(CurrentPersonality.PitchKp, CurrentPersonality.PitchKi, CurrentPersonality.PitchKd);
                _yawController = new UtilityPIDController(CurrentPersonality.YawKp, CurrentPersonality.YawKi, CurrentPersonality.YawKd);
                _rollController = new UtilityPIDController(CurrentPersonality.RollKp, CurrentPersonality.RollKi, CurrentPersonality.RollKd);
            }
            else
            {
                _pitchController.Kp = CurrentPersonality.PitchKp;
                _pitchController.Ki = CurrentPersonality.PitchKi;
                _pitchController.Kd = CurrentPersonality.PitchKd;

                _yawController.Kp = CurrentPersonality.YawKp;
                _yawController.Ki = CurrentPersonality.YawKi;
                _yawController.Kd = CurrentPersonality.YawKd;

                _rollController.Kp = CurrentPersonality.RollKp;
                _rollController.Ki = CurrentPersonality.RollKi;
                _rollController.Kd = CurrentPersonality.RollKd;
            }

            _pitchController.MaxIntegral = CurrentPersonality.IntegralClamp;
            _yawController.MaxIntegral = CurrentPersonality.IntegralClamp;
            _rollController.MaxIntegral = CurrentPersonality.IntegralClamp;
        }

        public void SetBehavior(ISimpleAiBehavior newBehavior)
        {
            if (_currentBehavior == newBehavior)
                return;

            // If we have an active behavior and a valid controller context, notify it that we are exiting.
            if (_currentBehavior != null && _controller != null)
            {
                _currentBehavior.OnExit(this);
            }

            _currentBehavior = newBehavior;

            // Reset PIDs when behavior changes, as the required responsiveness might differ.
            // Also called when SetBehavior(null) is used during ResetPilotState.
            if (_pitchController != null)
            {
                _pitchController.Reset();
                _yawController.Reset();
                _rollController.Reset();
            }

            if (_currentBehavior != null)
            {
                _currentBehaviorName = _currentBehavior.GetName();
                // Initialize the new behavior if the controller is ready.
                if (_controller != null)
                {
                    _currentBehavior.Initialize(this);
                }
            }
            else
            {
                _currentBehaviorName = "None";
            }
        }

        #endregion

        #region AI Update Loop

        private void UpdateAi()
        {
            if (_currentBehavior == null)
            {
                // If no behavior is set (e.g., right after respawn before Brain thinks), ensure inputs are zeroed.
                _inputState = new ShipInputState();
                return;
            }

            // Ensure essential references are still valid
            if (_transform == null || _properties == null || CurrentPersonality == null)
                return;

            // The behavior determines the movement goal.
            NavigationGoal goal = _currentBehavior.UpdateGoal(this);
            Vector3 desiredVelocity = CalculateDesiredVelocity(goal);

            float desiredSpeed = desiredVelocity.magnitude;
            Vector3 desiredDirection = (desiredSpeed > 0.01f) ? desiredVelocity / desiredSpeed : Vector3.zero;

            // Reset input state for flight controls
            _inputState = new ShipInputState();

            // Calculate flight inputs based on the movement goal
            CalculateRotationInputs(desiredSpeed, desiredDirection);
            CalculateThrustInputs(desiredSpeed);

            // Apply safety overrides
            MaintainMinimumAltitude();
        }

        // (The following methods remain unchanged from the provided context)

        private void AggregateWeaponInputs()
        {
            // Check if the weapon controller exists and is enabled
            if (_weaponController != null && _weaponController.enabled)
            {
                // The AiWeaponController runs its own Update() loop to determine these values.
                // We simply read the results here.
                _inputState.FirePrimary = _weaponController.FirePrimary;
                _inputState.FireSecondary = _weaponController.FireSecondary;
                _inputState.AimPosition = _weaponController.AimPosition;
                // Bomb logic can be added here if implemented in AiWeaponController.
            }
            else
            {
                // If no weapon controller, provide a default forward aim position
                // Check if _transform is initialized before accessing it
                if (_transform != null)
                {
                    _inputState.AimPosition = _transform.position + _transform.forward * 1000f;
                }
            }
        }

        private Vector3 CalculateDesiredVelocity(NavigationGoal goal)
        {
            Vector3 toTarget = goal.TargetPosition - _transform.position;
            float distance = toTarget.magnitude;

            if (distance < goal.ArrivalTolerance)
            {
                // If we are supposed to stop (DesiredSpeed near 0), then return zero velocity.
                if (goal.DesiredSpeed.HasValue && goal.DesiredSpeed.Value < 0.1f)
                    return Vector3.zero;
            }

            float maxSpeed = _properties.maxSpeed * CurrentPersonality.SpeedMultiplier;
            float desiredSpeed = goal.DesiredSpeed ?? maxSpeed;
            desiredSpeed = Mathf.Min(desiredSpeed, maxSpeed);

            // Apply slowdown radius logic only if the behavior requests it.
            // The new predictive BehaviorRace manages speed itself and sets SlowDownRadius to 0.
            if (goal.SlowDownRadius > 0 && distance < goal.SlowDownRadius)
            {
                float speedRamp = desiredSpeed * (distance / goal.SlowDownRadius);
                desiredSpeed = Mathf.Max(speedRamp, desiredSpeed * 0.05f);
            }

            return toTarget.normalized * desiredSpeed;
        }

        private void CalculateRotationInputs(float desiredSpeed, Vector3 desiredDirection)
        {
            if (desiredSpeed < 0.1f)
            {
                _isAligned = true;
                _alignmentAngle = 0;
                _inputState.Pitch = 0;
                _inputState.Yaw = 0;
                if (CurrentPersonality.Style == FlightStyle.Calculated)
                {
                    _pitchController.Reset();
                    _yawController.Reset();
                }
            }
            else
            {
                // --- REAL-TIME CALCULATION (Used for Flight Logic) ---
                float currentAlignmentAngle = Vector3.Angle(_transform.forward, desiredDirection);
                bool isCurrentlyAligned = currentAlignmentAngle < CurrentPersonality.AlignmentConeAngle;

                // --- FLIGHT LOGIC (Runs Every Frame) ---
                if (isCurrentlyAligned)
                {
                    _inputState.Pitch = 0;
                    _inputState.Yaw = 0;
                }
                else
                {
                    if (CurrentPersonality.Style == FlightStyle.Calculated)
                    {
                        CalculateRotationPID(desiredDirection);
                    }
                    else
                    {
                        CalculateRotationProportional(desiredDirection);
                    }
                }

#if UNITY_EDITOR
                _debugUpdateTimer -= Time.deltaTime;
                if (_debugUpdateTimer <= 0f)
                {
                    _debugUpdateTimer = 0.3f;
                    _alignmentAngle = currentAlignmentAngle;
                    _isAligned = isCurrentlyAligned;
                }
#endif
            }

            ApplyUprightCorrection();
        }

        private void CalculateRotationProportional(Vector3 directionToTarget)
        {
            Vector3 rotationAxis = Vector3.Cross(_transform.forward, directionToTarget.normalized);
            Vector3 localTorque = _transform.InverseTransformDirection(rotationAxis);

            float pitchInput = -localTorque.x * CurrentPersonality.RotationAggressiveness;
            float yawInput = localTorque.y * CurrentPersonality.RotationAggressiveness;

            _inputState.Pitch = Mathf.Clamp(pitchInput, -1f, 1f);
            _inputState.Yaw = Mathf.Clamp(yawInput, -1f, 1f);
        }

        private void CalculateRotationPID(Vector3 directionToTarget)
        {
            Vector3 rotationAxis = Vector3.Cross(_transform.forward, directionToTarget.normalized);
            Vector3 localTorque = _transform.InverseTransformDirection(rotationAxis);

            float pitchError = -localTorque.x;
            float yawError = localTorque.y;

            float pitchInput = _pitchController.Update(pitchError, Time.deltaTime);
            float yawInput = _yawController.Update(yawError, Time.deltaTime);

            _inputState.Pitch = Mathf.Clamp(pitchInput, -1f, 1f);
            _inputState.Yaw = Mathf.Clamp(yawInput, -1f, 1f);
        }

        private void ApplyUprightCorrection()
        {
            Vector3 localWorldUp = _transform.InverseTransformDirection(Vector3.up);
            float rollError = Mathf.Atan2(localWorldUp.x, localWorldUp.y);

            float rollInput;
            if (CurrentPersonality.Style == FlightStyle.Calculated)
            {
                rollInput = _rollController.Update(rollError, Time.deltaTime);
            }
            else
            {
                rollInput = rollError * CurrentPersonality.RotationAggressiveness * 0.5f;
            }

            _inputState.Roll = Mathf.Clamp(-rollInput, -1f, 1f);
        }

        private void CalculateThrustInputs(float desiredSpeed)
        {
            // Assuming Unity 2025.1 uses linearVelocity.
            float currentSpeed = (_rb != null) ? _rb.linearVelocity.magnitude : 0;

            // Handle near-zero desired speed (Stopping)
            if (desiredSpeed < 0.1f)
            {
                if (currentSpeed > 1.0f)
                {
                    _inputState.Thrust = -1.0f; // Full reverse thrust
                }
                else
                {
                    _inputState.Thrust = 0;
                }
                _inputState.Boost = false;
                return;
            }

            float speedError = desiredSpeed - currentSpeed;
            float appliedThrust = 0;
            float brakingThreshold = -2.0f;

            if (speedError > 0)
            {
                // Acceleration logic remains based on alignment
                float requiredThrustRatio = Mathf.Clamp01(desiredSpeed / _properties.maxSpeed);

                if (CurrentPersonality.Style == FlightStyle.Reckless)
                {
                    appliedThrust = requiredThrustRatio;
                }
                else if (CurrentPersonality.Style == FlightStyle.Focused)
                {
                    if (_alignmentAngle < CurrentPersonality.FocusedThrustThresholdAngle)
                    {
                        appliedThrust = requiredThrustRatio;
                    }
                    else
                    {
                        appliedThrust = 0.05f;
                    }
                }
                else // Calculated
                {
                    float errorRatio = Mathf.Clamp01(_alignmentAngle / 90f);
                    float alignmentFactor = 1.0f - errorRatio;
                    appliedThrust = requiredThrustRatio * Mathf.Pow(alignmentFactor, 3);
                    appliedThrust = Mathf.Max(0.05f, appliedThrust);
                }
            }
            else if (speedError < brakingThreshold)
            {
                // Deceleration (Active Braking)
                appliedThrust = Mathf.Clamp(speedError / _properties.maxSpeed, -1.0f, 0.0f);
            }

            _inputState.Thrust = appliedThrust;

            if (CurrentPersonality.AllowBoost && desiredSpeed > _properties.maxSpeed * 0.9f && _isAligned && speedError > 0)
            {
                _inputState.Boost = true;
            }
            else
            {
                _inputState.Boost = false;
            }
        }

        // MODIFIED: Implements "Altitude is Law".
        private void MaintainMinimumAltitude()
        {
            if (CurrentPersonality == null || !CurrentPersonality.UseMinimumAltitude)
                return;

            if (_sensorySystem == null || !_sensorySystem.enabled)
            {
                return;
            }

            float currentAltitude = _sensorySystem.AltitudeAboveGround;

            if (currentAltitude < CurrentPersonality.MinimumAltitude && currentAltitude < _sensorySystem.maxGroundCheckDistance)
            {
                // --- VETERAN PILOT SAFETY OVERRIDE ---
                // We must ensure corrective thrust is applied correctly regardless of orientation.

                // Determine the effectiveness of the ship's vertical thrusters (local up) in pushing away from the ground (Vector3.up).
                // If the ship is inverted, this dot product will be negative.
                float verticalEffectiveness = Vector3.Dot(_transform.up, Vector3.up);

                // Calculate the required thrust intensity based on proximity (0 to 1).
                float proximityRatio = 1.0f - (currentAltitude / CurrentPersonality.MinimumAltitude);
                float requiredThrust = proximityRatio;

                // If the ship is mostly upright (effectiveness > 0.1)
                if (verticalEffectiveness > 0.1f)
                {
                    // Apply positive vertical thrust.
                    float finalThrust = requiredThrust * verticalEffectiveness;
                    // Ensure we use the strongest positive input.
                    _inputState.Vertical = Mathf.Max(_inputState.Vertical, finalThrust);
                }
                // If the ship is mostly inverted (effectiveness < -0.1)
                else if (verticalEffectiveness < -0.1f)
                {
                    // Apply negative vertical thrust (downward thrusters) to push away from the ground.
                    float finalThrust = -(requiredThrust * Mathf.Abs(verticalEffectiveness));
                    // Ensure we use the strongest negative input.
                    _inputState.Vertical = Mathf.Min(_inputState.Vertical, finalThrust);
                }

                // If effectiveness is near 0 (banked 90 degrees), vertical thrusters won't help significantly; the pilot relies on rotation logic.
            }
        }

        #endregion
    }
}