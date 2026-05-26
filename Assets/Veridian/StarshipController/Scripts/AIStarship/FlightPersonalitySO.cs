using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Defines the core methodology the SimpleAiPilot uses for rotation and thrust control.
    /// </summary>
    public enum FlightStyle
    {
        /// <summary>
        /// Applies full power constantly, turning and burning simultaneously. Uses simple proportional control for rotation. Fast but often overshoots.
        /// </summary>
        Reckless,
        /// <summary>
        /// Prioritizes aligning with the target before committing to significant thrust. Uses simple proportional control. More controlled than Reckless.
        /// </summary>
        Focused,
        /// <summary>
        /// Uses PID controllers for smooth, precise rotation control, minimizing overshoot and oscillation. Thrust is applied gradually based on alignment error.
        /// </summary>
        Calculated
    }

    /// <summary>
    /// ScriptableObject defining the flight characteristics and parameters for the SimpleAiPilot.
    /// Determines how the AI handles rotation, speed, terrain avoidance, and control methodologies (Proportional vs. PID).
    /// </summary>
    [CreateAssetMenu(fileName = "NewFlightPersonality", menuName = "Starship AI/Simple/Flight Personality")]
    public class FlightPersonalitySO : ScriptableObject
    {
        [Header("Core Style")]
        [Tooltip("The fundamental control methodology used for flight. Determines whether Proportional or PID control is used and how thrust is applied relative to alignment.")]
        public FlightStyle Style = FlightStyle.Calculated;

        [Header("General Movement Parameters")]
        [Tooltip("The 'deadzone' angle (degrees). If the target is within this forward-facing cone, the AI considers itself aligned and stops rotating. Prevents wobble/oscillation.")]
        [Range(0.1f, 15f)]
        public float AlignmentConeAngle = 3.5f;

        [Tooltip("Multiplier applied to the ship's base max speed (defined in ShipProperties). Allows AI to fly faster (e.g., 1.2) or slower (e.g., 0.8) than the ship's standard limit.")]
        [Range(0.1f, 2.0f)]
        public float SpeedMultiplier = 1.0f;

        [Tooltip("Whether this personality allows the AI to use the ship's boost capability when trying to reach max speed and aligned with the target.")]
        public bool AllowBoost = true;

        [Header("Focused/Reckless (Proportional Control) Parameters")]
        [Tooltip("How aggressively the ship rotates when using Proportional control (Reckless/Focused styles). Higher values mean snappier turns but increased risk of overshoot. Typical range 3-6.")]
        [Range(1f, 10f)]
        public float RotationAggressiveness = 4.0f;

        [Tooltip("The maximum value for the integral term in PID controllers (Calculated style). Prevents integral windup which can cause circling or instability. A good default is 1.0.")]
        public float IntegralClamp = 1.0f;

        [Header("Terrain Avoidance")]
        [Tooltip("If true, the AI will use vertical thrusters (VTOL) as a safety override to maintain a minimum height above the ground, regardless of the current behavior's goal.")]
        public bool UseMinimumAltitude = true;

        [Tooltip("The desired minimum distance (in meters) to keep from the ground when UseMinimumAltitude is enabled. The AI will apply corrective thrust when below this height.")]
        public float MinimumAltitude = 200f;

        [Tooltip("In Focused mode only: The angle (degrees) off-target at which the AI starts applying significant thrust. Below this angle, thrust is maximized; above it, thrust is minimal (prioritizing rotation).")]
        [Range(5f, 90f)]
        public float FocusedThrustThresholdAngle = 15.0f;

        [Header("Calculated (PID Control) Parameters")]
        [Tooltip("Proportional gain for Pitch (Kp). Determines reaction strength to the current error. Higher Kp results in faster response, but can cause oscillation if too high.")]
        public float PitchKp = 0.5f;
        [Tooltip("Integral gain for Pitch (Ki). Corrects steady-state error over time (drift). Higher Ki reduces drift, but can cause windup and slow response if too high.")]
        public float PitchKi = 0.05f;
        [Tooltip("Derivative gain for Pitch (Kd). Dampens the response based on the rate of change of the error. Higher Kd reduces overshoot and stabilizes the system, but can make it sluggish if too high.")]
        public float PitchKd = 0.8f;

        [Tooltip("Proportional gain for Yaw (Kp). Determines reaction strength to the current error.")]
        public float YawKp = 0.5f;
        [Tooltip("Integral gain for Yaw (Ki). Corrects steady-state error over time.")]
        public float YawKi = 0.05f;
        [Tooltip("Derivative gain for Yaw (Kd). Dampens the response and reduces overshoot.")]
        public float YawKd = 0.8f;

        [Header("Upright Stabilization (Cosmetic)")]
        [Tooltip("If enabled, the AI will use the Roll axis to gently align its local 'up' direction with the world 'up' (Vector3.up).")]
        public bool MaintainUprightOrientation = true;

        // PID for the optional Upright stabilization (Used primarily by Calculated style)
        [Tooltip("Proportional gain (Kp) for Roll stabilization.")]
        public float RollKp = 0.3f;
        [Tooltip("Integral gain (Ki) for Roll stabilization. Usually 0 for roll to prevent windup during maneuvers.")]
        public float RollKi = 0.0f;
        [Tooltip("Derivative gain (Kd) for Roll stabilization.")]
        public float RollKd = 0.4f;
    }
}