using UnityEngine;

namespace Veridian.Starship.AI
{
    [CreateAssetMenu(fileName = "Condition_Danger", menuName = "Starship AI/Conditions/Danger")]
    public class DangerConditionSO : AiConditionSO
    {
        [Tooltip("The state we are checking for. True means we check if the ship IS in danger.")]
        public bool IsInDanger = true;

        protected override bool CheckCondition(AiBrain brain)
        {
            // The AiBrain updates this state during its sensory phase.
            return brain.IsInDanger == IsInDanger;
        }
    }
}