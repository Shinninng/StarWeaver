using UnityEngine;

namespace Veridian.Starship.AI
{
    // BehaviorIdle remains simple as it doesn't rely on configuration or targets.
    public class BehaviorIdle : SimpleAiBehaviorBase
    {
        public override string GetName() => "Idle";
        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            return NavigationGoal.Idle(pilot.Transform.position);
        }
        public override Transform GetCurrentTargetObject()
        {
            return null;
        }
    }
    [CreateAssetMenu(fileName = "Config_Idle", menuName = "Starship AI/Behavior Config/Idle")]
    public class IdleConfigSO : BehaviorConfigSO
    {
        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorIdle();
        }
    }
}