using UnityEngine;

namespace Veridian.Starship.AI
{
    public class BehaviorFlee : SimpleAiBehaviorBase
    {
        private readonly FleeConfigSO _config; // NEW
        private Transform _currentTarget; // For debug visualization

        public BehaviorFlee(FleeConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Flee";

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // Read the destination exclusively from the Brain's MoveTarget.
            Transform target = pilot.Brain != null ? pilot.Brain.MoveTarget : null;
            _currentTarget = target;

            if (target == null)
            {
                // If the Action did not provide a retreat destination (e.g., FindNearestHomeBase failed),
                // the behavior cannot function. Internal targeting logic (fleeing from AttackTarget) is removed.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            Vector3 destination = target.position;

            // Check for arrival
            if (Vector3.Distance(pilot.Transform.position, destination) < _config.ArrivalTolerance)
            {
                // Arrived at the safe spot.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            return new NavigationGoal
            {
                TargetPosition = destination,
                DesiredSpeed = null, // Flee at max speed
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius * 0.5f
            };
        }

        // REMOVED: DetermineFleeDestination method (internal targeting logic).

        public override Transform GetCurrentTargetObject() => _currentTarget;
    }

    [CreateAssetMenu(fileName = "Config_Flee", menuName = "Starship AI/Behavior Config/Flee")]
    public class FleeConfigSO : BehaviorConfigSO
    {
        public float ArrivalTolerance = 30f;
        // Note: The FleeDistance parameter from the old SimpleFleeData is removed,
        // as the destination must now be explicitly provided by the AiActionSO via MoveTarget.

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorFlee(this);
        }
    }
}