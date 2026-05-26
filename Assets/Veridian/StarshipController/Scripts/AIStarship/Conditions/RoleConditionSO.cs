using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    [CreateAssetMenu(fileName = "Condition_Role", menuName = "Starship AI/Conditions/Role")]
    public class RoleConditionSO : AiConditionSO
    {
        [Tooltip("The role required for this condition to pass.")]
        public RoleType RequiredRole = RoleType.Follower;

        protected override bool CheckCondition(AiBrain brain)
        {
            if (brain.Identity == null) return false;

            return brain.Identity.Role == RequiredRole;
        }
    }
}