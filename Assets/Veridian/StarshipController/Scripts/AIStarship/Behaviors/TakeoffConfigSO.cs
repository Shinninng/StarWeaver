using UnityEngine;

namespace Veridian.Starship.AI
{
    public class BehaviorTakeoff : SimpleAiBehaviorBase
    {
        private readonly TakeoffConfigSO _config; // NEW
        private Vector3 _targetPosition;
        private bool _isComplete = false;

        public BehaviorTakeoff(TakeoffConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Takeoff";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);
            _isComplete = false;

            // Define the target position directly above the ship's starting point.
            // This behavior ignores Brain.MoveTarget.
            _targetPosition = pilot.Transform.position + Vector3.up * _config.TakeoffAltitude;
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            if (_isComplete)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Check for arrival
            float distanceSqr = (_targetPosition - pilot.Transform.position).sqrMagnitude;
            if (distanceSqr < _config.ArrivalTolerance * _config.ArrivalTolerance)
            {
                _isComplete = true;

                // Signal the brain that the command has been executed.
                if (pilot.Brain != null && pilot.Brain.PendingCommand == AiBrain.AiCommand.RequestTakeoff)
                {
                    pilot.Brain.ClearCommand();
                }

                // Idle upon completion.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Move towards the target altitude
            return new NavigationGoal
            {
                TargetPosition = _targetPosition,
                DesiredSpeed = _config.TakeoffSpeed,
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius
            };
        }

        public override Transform GetCurrentTargetObject()
        {
            return null;
        }

        public override void OnExit(SimpleAiPilot pilot)
        {
            // Ensure the command is cleared even if the behavior is interrupted.
            if (pilot.Brain != null && pilot.Brain.PendingCommand == AiBrain.AiCommand.RequestTakeoff)
            {
                pilot.Brain.ClearCommand();
            }
        }
    }
    [CreateAssetMenu(fileName = "Config_Takeoff", menuName = "Starship AI/Behavior Config/Takeoff")]
    public class TakeoffConfigSO : BehaviorConfigSO
    {
        [Header("Takeoff Parameters")]
        [Tooltip("The target altitude for the takeoff maneuver.")]
        public float TakeoffAltitude = 300f;
        [Tooltip("Speed used during takeoff.")]
        public float TakeoffSpeed = 150f;
        public float ArrivalTolerance = 5f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorTakeoff(this);
        }
    }
}