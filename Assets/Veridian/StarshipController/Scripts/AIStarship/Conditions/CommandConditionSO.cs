using UnityEngine;

namespace Veridian.Starship.AI
{
    [CreateAssetMenu(fileName = "Condition_CommandReceived", menuName = "Starship AI/Conditions/Command Received")]
    public class CommandConditionSO : AiConditionSO
    {
        [Tooltip("The specific command that must be pending in the AiBrain for this condition to return true.")]
        public AiBrain.AiCommand RequiredCommand = AiBrain.AiCommand.RequestTakeoff;

        protected override bool CheckCondition(AiBrain brain)
        {
            // Checks the command state implemented in AiBrain.
            return brain.PendingCommand == RequiredCommand;
        }
    }
}