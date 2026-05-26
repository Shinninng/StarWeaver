using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    public class BehaviorAltitudeCorrection : SimpleAiBehaviorBase
    {
        private readonly AltitudeCorrectionConfigSO _config; // NEW
        private ShipSensorySystem _sensorySystem;

        public BehaviorAltitudeCorrection(AltitudeCorrectionConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Altitude Correction (Safety Override)";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);

            // Access the sensory system via the Brain.
            if (pilot.Brain != null && pilot.Brain.SensorySystem != null)
            {
                _sensorySystem = pilot.Brain.SensorySystem;
            }
            else
            {
                Debug.LogError($"BehaviorAltitudeCorrection: ShipSensorySystem not accessible via AiBrain on {pilot.gameObject.name}. Behavior cannot function.", pilot.gameObject);
            }
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // This behavior uses sensory data, not Brain targets.

            if (_sensorySystem == null || !_sensorySystem.enabled)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            float currentAltitude = _sensorySystem.AltitudeAboveGround;

            // 1. Check if we are too low (Highest priority).
            if (currentAltitude < _config.SafeMinimumAltitude)
            {
                // Force rapid climb straight up.
                Vector3 targetPosition = pilot.Transform.position + Vector3.up * (_config.SafeMinimumAltitude - currentAltitude + 100f);

                return new NavigationGoal
                {
                    TargetPosition = targetPosition,
                    DesiredSpeed = null, // Request maximum speed.
                    ArrivalTolerance = _config.ArrivalTolerance,
                    SlowDownRadius = 0f // Do not slow down during emergency climb.
                };
            }

            // 2. Check if we are too high.
            if (currentAltitude > _config.SafeMaximumAltitude && currentAltitude < _sensorySystem.maxGroundCheckDistance)
            {
                // Force controlled descent straight down.
                Vector3 targetPosition = pilot.Transform.position - Vector3.up * (currentAltitude - _config.SafeMaximumAltitude + 100f);

                // Use a controlled descent speed.
                float descentSpeed = pilot.Properties.maxSpeed * 0.6f;

                return new NavigationGoal
                {
                    TargetPosition = targetPosition,
                    DesiredSpeed = descentSpeed,
                    ArrivalTolerance = _config.ArrivalTolerance,
                    SlowDownRadius = _config.SlowDownRadius // Allow controlled slowdown during descent.
                };
            }

            // 3. We are within the Safe Zone.
            return NavigationGoal.Idle(pilot.Transform.position);
        }

        public override Transform GetCurrentTargetObject()
        {
            return null;
        }
    }
    [CreateAssetMenu(fileName = "Config_AltitudeCorrection", menuName = "Starship AI/Behavior Config/Altitude Correction")]
    public class AltitudeCorrectionConfigSO : BehaviorConfigSO
    {
        [Header("Safe Zone (Destination)")]
        [Tooltip("The minimum altitude the behavior aims to achieve when correcting from being too low.")]
        public float SafeMinimumAltitude = 200f;
        [Tooltip("The maximum altitude the behavior aims to achieve when correcting from being too high.")]
        public float SafeMaximumAltitude = 4800f;
        public float ArrivalTolerance = 20f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorAltitudeCorrection(this);
        }
    }
}