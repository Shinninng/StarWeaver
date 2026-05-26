using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Represents the aggregated input state requested by a driver (Player or AI) for the starship controller in a single frame.
    /// </summary>
    [System.Serializable]
    public struct ShipInputState
    {
        [Tooltip("Forward (1) or Reverse (-1) thrust input. Normalized -1 to 1.")]
        public float Thrust;
        [Tooltip("Vertical Up (1) or Down (-1) thrust input. Normalized -1 to 1.")]
        public float Vertical;
        [Tooltip("Roll input. Right (1) or Left (-1). Normalized -1 to 1.")]
        public float Roll;

        [Tooltip("Pitch input (Nose Up/Down). Scaled input value (e.g., mouse delta * sensitivity).")]
        public float Pitch;
        [Tooltip("Yaw input (Nose Left/Right). Scaled input value.")]
        public float Yaw;

        [Tooltip("Is the boost function requested? (Boolean state).")]
        public bool Boost;

        [Tooltip("Is the bomb drop requested this frame? (Trigger event).")]
        public bool FireBomb;

        [Tooltip("Is the primary weapon fire requested (e.g., laser held down)?")]
        public bool FirePrimary;
        [Tooltip("Is the secondary weapon fire requested (e.g., rocket launch)?")]
        public bool FireSecondary;

        [Tooltip("The world-space position the driver is currently aiming at. Calculated by the driver (Player or AI).")]
        public Vector3 AimPosition;

        [Tooltip("Indicates if the driver is actively engaging a specific target. Used by the WeaponController to determine if secondary weapons should utilize guidance.")]
        public bool IsTargetEngaged;
    }
}