using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Defines the high-level tactical state during combat, used by BehaviorAttack to select the appropriate maneuver.
    /// </summary>
    public enum CombatState
    {
        /// <summary>
        /// Closing the distance to the target to reach the preferred engagement range.
        /// </summary>
        Engaging,
        /// <summary>
        /// Actively maneuvering within the preferred engagement range to maintain a firing advantage.
        /// </summary>
        Dogfighting,
        /// <summary>
        /// Executing a short-term defensive maneuver to break contact or avoid incoming fire.
        /// </summary>
        Evading
    }

    /// <summary>
    /// Base class for AI combat maneuvers executed within the BehaviorAttack state machine.
    /// Maneuvers define the specific movement patterns used during combat.
    /// </summary>
    public abstract class CombatManeuver
    {
        protected SimpleAiPilot _pilot;
        protected Transform _target;
        // Configuration parameters are provided by the AttackConfigSO associated with the BehaviorAttack instance.
        protected AttackConfigSO _config;
        protected float _slowDownRadius;

        /// <summary>
        /// Initializes the maneuver with the current combat context.
        /// </summary>
        /// <param name="pilot">The pilot executing the maneuver.</param>
        /// <param name="target">The current combat target.</param>
        /// <param name="config">The configuration parameters for the attack behavior.</param>
        /// <param name="slowDownRadius">The radius at which the pilot should start slowing down.</param>
        public virtual void Initialize(SimpleAiPilot pilot, Transform target, AttackConfigSO config, float slowDownRadius)
        {
            _pilot = pilot;
            _target = target;
            _config = config;
            _slowDownRadius = slowDownRadius;
        }

        /// <summary>
        /// Updates the maneuver logic and returns the resulting navigation goal for the pilot.
        /// </summary>
        /// <returns>The navigation goal for the current frame.</returns>
        public abstract NavigationGoal Execute();

        /// <summary>
        /// Called when the maneuver starts (State entry).
        /// </summary>
        public virtual void OnEnter() { }

        /// <summary>
        /// Called when the maneuver ends (State exit).
        /// </summary>
        public virtual void OnExit() { }
    }

    // --- CONCRETE MANEUVERS ---

    /// <summary>
    /// Maneuver: Engage. Closes the distance to the target using a direct approach until within the preferred engagement range.
    /// </summary>
    public class ManeuverEngage : CombatManeuver
    {
        public override NavigationGoal Execute()
        {
            if (_target == null || _config == null)
            {
                return NavigationGoal.Idle(_pilot.Transform.position);
            }

            // Simple direct approach towards the target.
            Vector3 targetPosition = _target.position;

            return new NavigationGoal
            {
                TargetPosition = targetPosition,
                DesiredSpeed = null, // Use max speed to close distance quickly.
                // Arrival tolerance is the preferred range, signaling BehaviorAttack to transition when reached.
                ArrivalTolerance = _config.PreferredEngagementRange,
                SlowDownRadius = _slowDownRadius
            };
        }
    }

    /// <summary>
    /// Maneuver: Dogfight. Standard attack pattern within the preferred engagement range.
    /// Attempts to maintain distance while keeping the target in the firing arc by periodically moving to randomized offset positions relative to the target (strafing/circling/tailing).
    /// </summary>
    public class ManeuverDogfight : CombatManeuver
    {
        private Vector3 _currentTargetPosition;
        private float _repositionTimer;
        // The interval (seconds) at which the AI selects a new relative position around the target.
        private const float REPOSITION_INTERVAL = 3.0f;

        public override void OnEnter()
        {
            _repositionTimer = 0; // Reposition immediately on enter
        }

        public override NavigationGoal Execute()
        {
            if (_target == null || _config == null)
            {
                return NavigationGoal.Idle(_pilot.Transform.position);
            }

            _repositionTimer -= Time.deltaTime;

            if (_repositionTimer <= 0)
            {
                CalculateNewTargetPosition();
                _repositionTimer = REPOSITION_INTERVAL;
            }

            return new NavigationGoal
            {
                TargetPosition = _currentTargetPosition,
                // Use a slightly reduced speed (80%) for better maneuvering control during dogfights.
                DesiredSpeed = _pilot.Properties.maxSpeed * 0.8f,
                ArrivalTolerance = 15f, // Tight tolerance for precise movement to the offset position.
                // Reduced slowdown radius for quicker adjustments during maneuvering.
                SlowDownRadius = _slowDownRadius * 0.5f
            };
        }

        /// <summary>
        /// Calculates a new randomized position relative to the target, biased towards the target's rear hemisphere.
        /// </summary>
        private void CalculateNewTargetPosition()
        {
            // Calculate a position relative to the target that keeps us within range but offset.

            // 1. Define a random offset direction in the target's local space.
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.6f, 0.6f), // Left/Right bias
                Random.Range(-0.4f, 0.4f), // Up/Down bias
                -1f                        // Strong bias towards the rear (-Z)
            ).normalized;

            // 2. Scale the offset by the preferred engagement range.
            Vector3 desiredOffset = randomOffset * _config.PreferredEngagementRange;

            // 3. Calculate the world position by transforming the local offset relative to the target.
            _currentTargetPosition = _target.TransformPoint(desiredOffset);
        }
    }

    /// <summary>
    /// Maneuver: Evade. Executes a short, sharp evasive maneuver (corkscrew) away from the target's general direction.
    /// This maneuver runs for a fixed duration or until the evasion point is reached.
    /// </summary>
    public class ManeuverEvade : CombatManeuver
    {
        private Vector3 _evasionPoint;
        private float _evasionDuration;
        // The distance the AI attempts to travel during the evasion maneuver.
        private const float EVASION_DISTANCE = 150f;

        public override void OnEnter()
        {
            CalculateEvasionPoint();
            // Calculate duration based on distance and estimated speed (e.g., 50% of max speed), ensuring a minimum speed floor of 20 m/s.
            _evasionDuration = EVASION_DISTANCE / Mathf.Max(20f, _pilot.Properties.maxSpeed * 0.5f);
        }

        public override NavigationGoal Execute()
        {
            _evasionDuration -= Time.deltaTime;

            // If the maneuver duration is complete or we reached the evasion point (within 20m), return an Idle goal.
            // The BehaviorAttack state machine will detect the Idle goal (DesiredSpeed == 0) and transition out of the Evading state.
            if (_evasionDuration <= 0 || Vector3.Distance(_pilot.Transform.position, _evasionPoint) < 20f)
            {
                return NavigationGoal.Idle(_pilot.Transform.position);
            }

            return new NavigationGoal
            {
                TargetPosition = _evasionPoint,
                DesiredSpeed = null, // Max speed (boost if possible) for quick evasion.
                ArrivalTolerance = 20f,
                SlowDownRadius = _slowDownRadius * 0.5f // Quick stop if the point is reached early.
            };
        }

        /// <summary>
        /// Calculates the evasion point by finding a direction perpendicular to the target and biasing slightly away.
        /// </summary>
        private void CalculateEvasionPoint()
        {
            Vector3 evasionDirection;

            if (_target != null)
            {
                // Evasion direction perpendicular to the direction to the target (corkscrew motion).
                Vector3 toTarget = (_target.position - _pilot.Transform.position).normalized;
                // Use Random.onUnitSphere for the cross product to introduce randomness in the perpendicular direction.
                Vector3 perpendicular = Vector3.Cross(toTarget, Random.onUnitSphere).normalized;

                // Bias slightly away from the target (30% influence).
                evasionDirection = (perpendicular - toTarget * 0.3f).normalized;
            }
            else
            {
                // Fallback if target is lost during evasion: move in a random direction.
                evasionDirection = Random.onUnitSphere;
            }

            _evasionPoint = _pilot.Transform.position + evasionDirection * EVASION_DISTANCE;
        }
    }
}