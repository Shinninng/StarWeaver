using UnityEngine;


namespace Veridian.Starship.AI
{
    public class BehaviorGuardian : SimpleAiBehaviorBase
    {
        private readonly GuardianConfigSO _config; // NEW
        private Transform _currentTarget;

        public BehaviorGuardian(GuardianConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Guardian";

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // Read target exclusively from the Brain (the entity to guard).
            Transform target = pilot.Brain != null ? pilot.Brain.MoveTarget : null;
            _currentTarget = target;

            if (target == null)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            Vector3 desiredPosition = target.TransformPoint(_config.FormationOffset);

            float? desiredSpeed = null;
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            // Check if Rigidbody exists and is not kinematic before accessing velocity
            if (targetRb != null && !targetRb.isKinematic)
            {
                // Assuming Unity 6 compatibility (e.g., linearVelocity).
                desiredSpeed = targetRb.linearVelocity.magnitude;
            }

            return new NavigationGoal
            {
                TargetPosition = desiredPosition,
                DesiredSpeed = desiredSpeed,
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius * 0.5f // Use config value
            };
        }

        // REMOVED: DetermineTarget method.

        public override Transform GetCurrentTargetObject() => _currentTarget;
    }
    [CreateAssetMenu(fileName = "Config_Guardian", menuName = "Starship AI/Behavior Config/Guardian")]
    public class GuardianConfigSO : BehaviorConfigSO
    {
        public Vector3 FormationOffset = new(-50, 10, 0);
        public float ArrivalTolerance = 5f;

        // Note: Fallback targets are removed.

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorGuardian(this);
        }
    }
}