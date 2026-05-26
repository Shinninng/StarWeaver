using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// ScriptableObject defining the configuration parameters for the ShipSensorySystem, including detection ranges, scanning parameters, and avoidance behavior settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSensorySettings", menuName = "Veridian/Starship/Sensory Settings")]
    public class SensorySettingsSO : ScriptableObject
    {
        [Header("Ground Detection")]
        [Tooltip("The maximum distance (in meters) the system checks for ground directly below the ship.")]
        public float maxGroundCheckDistance = 5000f;

        [Header("Obstacle Detection")]
        [Tooltip("The distance (in meters) ahead of the ship that the system proactively checks for obstacles. This is the trigger distance for initiating avoidance.")]
        public float forwardCheckDistance = 300f;
        [Tooltip("The radius (in meters) of the sphere cast used for obstacle detection, representing the ship's approximate collision volume.")]
        public float checkRadius = 10f;

        [Header("Avoidance Scanning")]
        [Tooltip("The total angle (in degrees) of the cone used for the active avoidance scan when an obstacle is detected.")]
        public float coneAngle = 30f;
        [Tooltip("The number of rays cast within the scan cone to find an optimal escape route.")]
        public int coneRayCount = 8;

        [Header("Avoidance Behavior")]
        [Tooltip("The duration (in seconds) the system remains in the 'Avoiding' state after determining an escape vector.")]
        public float avoidanceDuration = 2.0f;
        [Tooltip("The duration (in seconds) the system waits (Cooldown) after completing an avoidance maneuver before resuming proactive obstacle searching.")]
        public float cooldownDuration = 1.0f;

        [Header("Decision Bias (Optional)")]
        [Tooltip("A bonus score added to potential escape paths that point generally upwards (away from the ground).")]
        public float upwardBiasBonus = 150f;
        [Tooltip("A bonus score added to escape paths that align with the ship's current turning inertia (angular velocity), favoring continued movement in the same direction.")]
        public float turningInertiaBiasBonus = 200f;
    }
}