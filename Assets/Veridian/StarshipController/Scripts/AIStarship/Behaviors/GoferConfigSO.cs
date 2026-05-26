using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Behavior: Gofer. A simple navigation behavior that moves the ship directly towards the AiBrain's MoveTarget.
    /// Used for "Go To" commands, returning to base, or moving to waypoints.
    /// </summary>
    public class BehaviorGofer : SimpleAiBehaviorBase
    {
        private readonly GoferConfigSO _config;
        private Transform _currentTarget; // Cached reference for debug visualization

        /// <summary>
        /// Initializes the BehaviorGofer with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration parameters for the Gofer behavior.</param>
        public BehaviorGofer(GoferConfigSO config)
        {
            _config = config;
        }

        public override string GetName() => "Gofer";

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // Read the target directly and exclusively from the Brain.
            Transform target = pilot.Brain != null ? pilot.Brain.MoveTarget : null;
            _currentTarget = target; // Update debug reference

            if (target == null)
            {
                // If no target is assigned by the Brain, idle at the current position.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Move towards the target position.
            return new NavigationGoal
            {
                TargetPosition = target.position,
                DesiredSpeed = null, // Use max speed allowed by personality.
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius // Use the value from the ConfigSO
            };
        }

        public override Transform GetCurrentTargetObject() => _currentTarget;
    }

    /// <summary>
    /// Configuration ScriptableObject for the Gofer behavior.
    /// Defines parameters for simple navigation towards a target.
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Gofer", menuName = "Starship AI/Behavior Config/Gofer")]
    public class GoferConfigSO : BehaviorConfigSO
    {
        [Tooltip("The distance (in meters) at which the AI considers the destination reached.")]
        public float ArrivalTolerance = 10f;

        // Note: Fallback targets are not implemented in this configuration, as target management is exclusively handled by the AiBrain's actions.

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            // Pass this configuration instance to the BehaviorGofer constructor.
            return new BehaviorGofer(this);
        }
    }
}