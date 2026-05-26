using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// ScriptableObject defining the base flight characteristics and physical properties of a specific starship model.
    /// Used to initialize the runtime ShipProperties component.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStarshipProfile", menuName = "Veridian/Starship/Starship Profile")]
    public class StarshipProfileSO : ScriptableObject
    {
        [Header("Core Physics")]
        [Tooltip("The mass of the ship in kilograms. Affects inertia, acceleration, and collision forces.")]
        public float mass = 5000f;
        [Tooltip("The maximum speed the ship can achieve in meters per second (m/s) under normal thrust (without boost).")]
        public float maxSpeed = 900f;
        [Tooltip("Custom damping applied to angular velocity in FixedUpdate. Higher values help the ship stop rotating faster and stabilize.")]
        public float rotationDamping = 3f;
        [Tooltip("The built-in angular drag applied by the Rigidbody physics system. Affects how quickly rotation slows down naturally.")]
        public float angularDrag = 1f;
        [Tooltip("Determines if the ship's Rigidbody is affected by the global gravity.")]
        public bool useGravity = true;

        [Header("Thrust Settings (+Z Forward)")]
        [Tooltip("The maximum force applied for forward movement (Local +Z axis).")]
        public float forwardThrustPower = 300000f;
        [Tooltip("The maximum force applied for reverse movement or braking (Local -Z axis).")]
        public float reverseThrustPower = 150000f;
        [Tooltip("The maximum force applied for vertical movement (Local +Y axis).")]
        public float verticalThrustPower = 150000f;
        [Tooltip("The speed at which the main and vertical thrusters ramp up and down towards the desired input level.")]
        public float thrustRampUpSpeed = 2.5f;

        [Header("Rotation Settings")]
        [Tooltip("The maximum torque applied for pitching (Rotation around Local X axis).")]
        public float pitchPower = 1000000f;
        [Tooltip("The maximum torque applied for yawing (Rotation around Local Y axis).")]
        public float yawPower = 1000000f;
        [Tooltip("The maximum torque applied for rolling (Rotation around Local Z axis).")]
        public float rollPower = 1000000f;

        [Header("Flight Model & Maneuverability")]
        [Tooltip("Defines the maneuverability multiplier based on the ship's current speed. X-axis: Speed (m/s), Y-axis: Multiplier (0.0 to 1.0). Affects Pitch and Yaw power.")]
        // Initialize with a default curve that provides constant maneuverability (1.0) up to a reasonable max speed if one isn't set yet.
        public AnimationCurve maneuverabilityCurve = AnimationCurve.Constant(0f, 1000f, 1.0f);

        [Tooltip("Enables the G-force safety limiter system, which automatically reduces thrust and torque during extreme maneuvers to stay within limits.")]
        public bool useGForceLimiter = false;

        [Tooltip("The maximum G-force the ship can sustain before the limiter begins to engage. (e.g., 9-12G for typical fighters).")]
        public float maxOverallGForce = 10.0f;

        [Header("Aerodynamics")]
        [Tooltip("A multiplier applied to the environment's base drag coefficients. Reflects the aerodynamic efficiency of the ship's design. (1.0 = standard drag).")]
        public float aerodynamicDragModifier = 1.0f;

        [Header("Ground Effect")]
        [Tooltip("The maximum altitude (in meters) at which the ground effect cushion is active.")]
        public float groundEffectMaxAltitude = 100f;
        [Tooltip("The maximum upward force applied by the ground effect when the ship is at zero altitude.")]
        public float groundEffectMaxForce = 200000f;

        [Header("Input Settings")]
        [Tooltip("Multiplier for player mouse input sensitivity. Applied in the PlayerShipDriver.")]
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
        // Initialize LayerMask to "Default" layer if possible, otherwise layer 0.
        public LayerMask lerpGroundMask = 1;
    }
}