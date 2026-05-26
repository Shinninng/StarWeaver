using UnityEngine;

namespace Veridian.Starship.AI
{
    public class BehaviorLoiterer : SimpleAiBehaviorBase
    {
        private readonly LoiterConfigSO _config; // NEW
        private Vector3 _currentTargetPosition;
        private Vector3 _centerPosition;
        private Transform _centerTransform; // Used if the center is a moving object (MoveTarget)
        private float _repathTimer;
        private bool _isInitialized = false;

        public BehaviorLoiterer(LoiterConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Loiterer";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);
            // Initialization deferred to the first UpdateGoal call to ensure Brain.MoveTarget is potentially set by the Action.
        }

        private void InitializeLoiterZone(SimpleAiPilot pilot)
        {
            if (_isInitialized) return;

            // Determine the initial center of the loiter zone.
            UpdateCenterPosition(pilot);

            // If the center position is still zero (meaning UpdateCenterPosition didn't set it based on target),
            // default to the ship's current position (loiter in place).
            if (_centerPosition == Vector3.zero)
            {
                _centerPosition = pilot.Transform.position;
            }

            PickNewTargetPoint();
            _isInitialized = true;
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            InitializeLoiterZone(pilot);

            // Update the center position continuously in case the MoveTarget moves or is lost.
            UpdateCenterPosition(pilot);

            float distance = Vector3.Distance(pilot.Transform.position, _currentTargetPosition);
            _repathTimer -= Time.deltaTime;

            // Check if we arrived or timer expired.
            if (distance <= _config.ArrivalTolerance || _repathTimer <= 0)
            {
                PickNewTargetPoint();
            }

            // Check if the current target point is still within the radius of the (potentially moved) center.
            if (Vector3.Distance(_currentTargetPosition, _centerPosition) > _config.Radius)
            {
                PickNewTargetPoint();
            }

            return new NavigationGoal
            {
                TargetPosition = _currentTargetPosition,
                DesiredSpeed = pilot.Properties.maxSpeed * 0.6f, // Loiter speed
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius
            };
        }

        private void UpdateCenterPosition(SimpleAiPilot pilot)
        {
            Transform target = pilot.Brain != null ? pilot.Brain.MoveTarget : null;

            if (target != null)
            {
                // If MoveTarget is set, use it as the center.
                _centerTransform = target;
                _centerPosition = target.position;
            }
            else
            {
                // If MoveTarget is null, we stop tracking a transform, but continue using the last known _centerPosition.
                _centerTransform = null;
            }
        }

        private void PickNewTargetPoint()
        {
            Vector3 randomOffset = Random.insideUnitSphere * _config.Radius;
            _currentTargetPosition = _centerPosition + randomOffset;
            _repathTimer = Random.Range(10f, 30f);
        }

        // The target object is the center of the loiter zone.
        public override Transform GetCurrentTargetObject() => _centerTransform;
    }
    [CreateAssetMenu(fileName = "Config_Loiter", menuName = "Starship AI/Behavior Config/Loiter")]
    public class LoiterConfigSO : BehaviorConfigSO
    {
        public float Radius = 200f;
        public float ArrivalTolerance = 30f;

        // Note: CenterPoint Transform is removed. The behavior uses AiBrain.MoveTarget or activation location.

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorLoiterer(this);
        }
    }
}