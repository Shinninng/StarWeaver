using UnityEngine;

namespace Veridian.Starship.AI
{
    public class BehaviorLanding : SimpleAiBehaviorBase
    {
        private readonly LandingConfigSO _config; // NEW
        private Vector3 _targetPosition;
        private bool _isComplete = false;
        // private ShipSensorySystem _sensorySystem; // Removed as internal targeting (raycasting) is prohibited.

        public BehaviorLanding(LandingConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Landing";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);
            _isComplete = false;

            // Determine the landing target. MUST use MoveTarget according to the strict architecture.
            if (pilot.Brain != null && pilot.Brain.MoveTarget != null)
            {
                _targetPosition = pilot.Brain.MoveTarget.position;
            }
            else
            {
                // Internal targeting logic (raycasting down) is removed.
                // If no MoveTarget is provided by the AiAction, the behavior aborts.
                Debug.LogWarning("BehaviorLanding: Activated without a MoveTarget (Landing Pad). Aborting landing.", pilot.gameObject);
                _isComplete = true;
            }
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            if (_isComplete)
            {
                // Ensure command is cleared if initialization failed or completed
                if (pilot.Brain != null && pilot.Brain.PendingCommand == AiBrain.AiCommand.RequestLanding)
                {
                    pilot.Brain.ClearCommand();
                }
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Update target position if the MoveTarget is dynamic
            if (pilot.Brain != null && pilot.Brain.MoveTarget != null)
            {
                _targetPosition = pilot.Brain.MoveTarget.position;
            }
            else
            {
                // Target lost during landing
                _isComplete = true;
                return NavigationGoal.Idle(pilot.Transform.position);
            }


            // Check for arrival (proximity to target position)
            float distanceSqr = (_targetPosition - pilot.Transform.position).sqrMagnitude;
            if (distanceSqr < _config.ArrivalTolerance * _config.ArrivalTolerance)
            {
                _isComplete = true;
            }

            if (_isComplete)
            {
                // Signal the brain that the command has been executed.
                if (pilot.Brain != null && pilot.Brain.PendingCommand == AiBrain.AiCommand.RequestLanding)
                {
                    pilot.Brain.ClearCommand();
                }
                // Idle (full stop) upon completion.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Move towards the landing position
            return new NavigationGoal
            {
                TargetPosition = _targetPosition,
                DesiredSpeed = _config.LandingSpeed,
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius // Ensure smooth slowdown
            };
        }

        public override Transform GetCurrentTargetObject()
        {
            if (_pilot != null && _pilot.Brain != null)
            {
                return _pilot.Brain.MoveTarget;
            }
            return null;
        }

        public override void OnExit(SimpleAiPilot pilot)
        {
            // Ensure the command is cleared even if interrupted.
            if (pilot.Brain != null && pilot.Brain.PendingCommand == AiBrain.AiCommand.RequestLanding)
            {
                pilot.Brain.ClearCommand();
            }
        }
    }
    [CreateAssetMenu(fileName = "Config_Landing", menuName = "Starship AI/Behavior Config/Landing")]
    public class LandingConfigSO : BehaviorConfigSO
    {
        [Header("Landing Parameters")]
        [Tooltip("Speed used during landing.")]
        public float LandingSpeed = 100f;
        public float ArrivalTolerance = 5f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorLanding(this);
        }
    }
}