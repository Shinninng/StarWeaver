using UnityEngine;


namespace Veridian.Starship.Core
{
    /// <summary>
    /// Holds the runtime flight characteristics and physical properties of the starship.
    /// It facilitates initialization from a StarshipProfileSO and allows for dynamic modification during gameplay (e.g., power-ups or damage effects).
    /// </summary>
    public class ShipProperties : MonoBehaviour
    {
        [Header("Profile Configuration")]
        [Tooltip("The ScriptableObject profile containing the base flight parameters. If assigned and 'Use Profile' is true, parameters are loaded from this profile on Awake.")]
        public StarshipProfileSO profileSO;
        [Tooltip("If true, the runtime parameters will be synchronized with the assigned Profile SO. If false, the values set directly on this component will be used independently.")]
        public bool useProfile = true;

        [Header("--- Runtime Ship Parameters ---")]

        // Fields initialized with default values in case no SO is used.
        [Header("Core Physics")]
        [Tooltip("The mass of the ship in kilograms. Affects inertia, acceleration, and collision forces.")]
        public float mass = 5000f;
        [Tooltip("The maximum speed the ship can achieve in meters per second (m/s) under normal thrust (without boost).")]
        public float maxSpeed = 900f;
        [Tooltip("Custom damping applied to angular velocity in FixedUpdate. Higher values help the ship stop rotating faster and stabilize.")]
        public float rotationDamping = 3f;
        [Tooltip("The built-in angular drag applied by the Rigidbody physics system. Affects how quickly rotation slows down naturally.")]
        public float angularDrag = 1f;
        [Tooltip("Determines if the ship's Rigidbody is affected by the global gravity defined in the EnvironmentManager.")]
        public bool useGravity = true;

        [Header("Thrust")]
        [Tooltip("The maximum force applied for forward movement (Local +Z axis).")]
        public float forwardThrustPower = 300000f;
        [Tooltip("The maximum force applied for reverse movement or braking (Local -Z axis).")]
        public float reverseThrustPower = 150000f;
        [Tooltip("The maximum force applied for vertical movement (Local +Y axis).")]
        public float verticalThrustPower = 150000f;
        [Tooltip("The speed at which the main and vertical thrusters ramp up and down towards the desired input level.")]
        public float thrustRampUpSpeed = 2.5f;

        [Header("Rotation")]
        [Tooltip("The maximum torque applied for pitching (Rotation around Local X axis).")]
        public float pitchPower = 1000000f;
        [Tooltip("The maximum torque applied for yawing (Rotation around Local Y axis).")]
        public float yawPower = 1000000f;
        [Tooltip("The maximum torque applied for rolling (Rotation around Local Z axis).")]
        public float rollPower = 1000000f;

        [Header("Flight Model & Maneuverability")]
        [Tooltip("Defines the maneuverability multiplier based on the ship's current speed. X-axis: Speed (m/s), Y-axis: Multiplier (0.0 to 1.0). Affects Pitch and Yaw power.")]
        public AnimationCurve maneuverabilityCurve = AnimationCurve.Constant(0f, 1000f, 1.0f);

        [Tooltip("Enables the G-force safety limiter system, which automatically reduces thrust and torque during extreme maneuvers to stay within limits.")]
        public bool useGForceLimiter = false;

        [Tooltip("The maximum G-force the ship can sustain before the limiter begins to engage. (e.g., 9-12G for typical fighters).")]
        public float maxOverallGForce = 10.0f;

        [Header("Maneuverability Envelope (AI Limits)")]
        [Tooltip("The tightest possible turn radius (in meters) the ship can achieve when flying at Max Speed. Defines the high-speed maneuvering limit for AI calculations.")]
        public float MinTurnRadiusAtMaxSpeed = 1000f;

        [Tooltip("The tightest possible turn radius (in meters) the ship can achieve when flying at Optimal Maneuvering Speed. Defines the low-speed maneuvering limit.")]
        public float MinTurnRadiusAtOptimalSpeed = 50f;

        [Tooltip("The speed (m/s) at which the ship achieves its minimum turn radius (MinTurnRadiusAtOptimalSpeed).")]
        public float OptimalManeuveringSpeed = 50f;


        [Header("Aerodynamics")]
        [Tooltip("A multiplier applied to the environment's base drag coefficients. Reflects the aerodynamic efficiency of the ship's design. (1.0 = standard drag).")]
        public float aerodynamicDragModifier = 1.0f;

        [Header("Ground Effect")]
        [Tooltip("The maximum altitude (in meters) at which the ground effect cushion is active.")]
        public float groundEffectMaxAltitude = 100f;
        [Tooltip("The maximum upward force applied by the ground effect when the ship is at zero altitude.")]
        public float groundEffectMaxForce = 200000f;
        [Tooltip("The falloff curve for the ground effect force. X-axis is proximity (0=Max Altitude, 1=Ground Level), Y-axis is force multiplier (0 to 1).")]
        // The curve itself is kept on the component rather than the SO, as curves are often tweaked per-instance.
        public AnimationCurve groundEffectCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);


        [Header("Input")]
        [Tooltip("Multiplier for player mouse input sensitivity.")]
        public float mouseSensitivity = 3f;

        [Header("Boost System")]
        [Tooltip("The multiplier applied to forward thrust power and the maximum speed limit when boosting is active.")]
        public float boostMultiplier = 2f;
        [Tooltip("The total capacity of the boost reservoir.")]
        public float maxBoost = 200f;
        [Tooltip("The rate at which boost is consumed per second when active.")]
        public float boostDrainRate = 25f;
        [Tooltip("The rate at which boost recharges per second when inactive and the cooldown has passed.")]
        public float boostRechargeRate = 15f;
        [Tooltip("The delay (in seconds) after boosting stops before recharge begins.")]
        public float boostRechargeDelay = 1.0f;
        [Tooltip("The speed at which the boost multiplier ramps up when activated and down when deactivated.")]
        public float boostRampUpSpeed = 5f;
        [Tooltip("Modulates the base boost multiplier based on current speed. X-axis is normalized speed (0-1), Y-axis is the modulation factor (e.g., 1.0 = 100% of base boost multiplier).")]
        // A flat curve at 1 is a good default, as it won't change the base value.
        public AnimationCurve boostSpeedModifierCurve = AnimationCurve.Constant(0, 1, 1);

        [Header("LERP Mode (AI Optimization)")]
        [Tooltip("The standard cruising speed (m/s) used when the ship is operating in the low-fidelity LERP movement mode.")]
        public float lerpCruiseSpeed = 500f;
        [Tooltip("The preferred altitude (in meters) the ship attempts to maintain in LERP mode using simplified ground checks.")]
        public float lerpCruisingAltitude = 1000f;
        [Tooltip("The maximum vertical speed (m/s) used for altitude adjustments in LERP mode.")]
        public float lerpVerticalSpeed = 150f;
        [Tooltip("The rotation speed (degrees per second) used for orientation adjustments (Pitch, Yaw, Roll) in LERP mode.")]
        public float lerpRotationSpeed = 60f;
        [Tooltip("The LayerMask used for simplified ground detection in LERP mode.")]
        public LayerMask lerpGroundMask = 1; // Default layer

        void Awake()
        {
            LoadProfile();
        }

        void OnValidate()
        {
            // Allow live-reloading the profile in the editor while playing if useProfile is toggled
            if (Application.isPlaying)
            {
                LoadProfile();
            }
            // Preview the SO values in the inspector when not in play mode
            else if (useProfile && profileSO != null)
            {
                LoadProfileData(profileSO);
            }
        }

        /// <summary>
        /// Loads the parameters from the assigned StarshipProfileSO if 'useProfile' is enabled.
        /// </summary>
        public void LoadProfile()
        {
            if (useProfile && profileSO != null)
            {
                LoadProfileData(profileSO);
            }
        }

        private void LoadProfileData(StarshipProfileSO data)
        {
            // Core Physics
            mass = data.mass;
            maxSpeed = data.maxSpeed;
            rotationDamping = data.rotationDamping;
            angularDrag = data.angularDrag;
            useGravity = data.useGravity;

            // Thrust
            forwardThrustPower = data.forwardThrustPower;
            reverseThrustPower = data.reverseThrustPower;
            verticalThrustPower = data.verticalThrustPower;
            thrustRampUpSpeed = data.thrustRampUpSpeed;

            // Rotation
            pitchPower = data.pitchPower;
            yawPower = data.yawPower;
            rollPower = data.rollPower;

            // Flight Model
            // AnimationCurves are reference types. We copy the keys to ensure the runtime instance is independent of the asset.
            if (data.maneuverabilityCurve != null && data.maneuverabilityCurve.length > 0)
            {
                maneuverabilityCurve = new AnimationCurve(data.maneuverabilityCurve.keys);
            }
            else
            {
                // Fallback if the SO curve is invalid or empty, ensuring functionality remains consistent.
                maneuverabilityCurve = AnimationCurve.Constant(0f, data.maxSpeed > 0 ? data.maxSpeed : 1000f, 1.0f);
            }

            // Ensure the wrap mode is clamped as required by the spec (for speeds exceeding the max time)
            maneuverabilityCurve.preWrapMode = WrapMode.ClampForever;
            maneuverabilityCurve.postWrapMode = WrapMode.ClampForever;


            useGForceLimiter = data.useGForceLimiter;
            maxOverallGForce = data.maxOverallGForce;

            // NOTE: We assume that if StarshipProfileSO is updated in the future, the new envelope parameters will be loaded here.

            // Aerodynamics
            aerodynamicDragModifier = data.aerodynamicDragModifier;

            // Ground Effect
            groundEffectMaxAltitude = data.groundEffectMaxAltitude;
            groundEffectMaxForce = data.groundEffectMaxForce;

            // Input
            mouseSensitivity = data.mouseSensitivity;

            // Boost System
            boostMultiplier = data.boostMultiplier;
            maxBoost = data.maxBoost;
            boostDrainRate = data.boostDrainRate;
            boostRechargeRate = data.boostRechargeRate;
            boostRechargeDelay = data.boostRechargeDelay;
            boostRampUpSpeed = data.boostRampUpSpeed;

            // LERP Mode
            lerpCruiseSpeed = data.lerpCruiseSpeed;
            lerpCruisingAltitude = data.lerpCruisingAltitude;
            lerpVerticalSpeed = data.lerpVerticalSpeed;
            lerpRotationSpeed = data.lerpRotationSpeed;
            lerpGroundMask = data.lerpGroundMask;
        }

        /// <summary>
        /// Calculates the estimated minimum turning radius (in meters) the ship can achieve at the given speed, based on the defined maneuverability envelope. Used by AI for predictive pathing.
        /// </summary>
        /// <param name="speed">The speed (m/s) to calculate the radius for.</param>
        /// <returns>The minimum turning radius in meters.</returns>
        public float CalculateMinTurnRadius(float speed)
        {
            // Normalize the speed relative to the defined envelope
            float normalizedSpeed;
            if (speed <= OptimalManeuveringSpeed)
            {
                return MinTurnRadiusAtOptimalSpeed;
            }
            else if (speed >= maxSpeed)
            {
                return MinTurnRadiusAtMaxSpeed;
            }
            else
            {
                // Calculate the interpolation factor between optimal speed and max speed
                normalizedSpeed = (speed - OptimalManeuveringSpeed) / (maxSpeed - OptimalManeuveringSpeed);
            }

            // Linearly interpolate the turning radius. A curve could be used here for more nuance if added to the profile.
            return Mathf.Lerp(MinTurnRadiusAtOptimalSpeed, MinTurnRadiusAtMaxSpeed, normalizedSpeed);
        }
    }
}