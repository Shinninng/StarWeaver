using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Condition that checks the validity (existence, active state, alive status) of the targets currently stored in the AiBrain.
    /// </summary>
    [CreateAssetMenu(fileName = "Condition_Target", menuName = "Starship AI/Conditions/Target")]
    public class TargetConditionSO : AiConditionSO
    {
        public enum TargetCheckType
        {
            HasMoveTarget,
            HasAttackTarget
        }

        [Tooltip("The specific target slot in the AiBrain to check (MoveTarget or AttackTarget).")]
        public TargetCheckType CheckType = TargetCheckType.HasAttackTarget;

        [Tooltip("The expected state of the target. True if we are checking that a valid target exists, False if we are checking that it does NOT exist.")]
        public bool ShouldHaveTarget = true;

        protected override bool CheckCondition(AiBrain brain)
        {
            bool hasTarget = false;
            Transform targetTransform = null;

            // Determine which target slot to check
            switch (CheckType)
            {
                case TargetCheckType.HasMoveTarget:
                    targetTransform = brain.MoveTarget;
                    break;
                case TargetCheckType.HasAttackTarget:
                    targetTransform = brain.AttackTarget;
                    break;
            }

            // Validate the target
            if (targetTransform != null)
            {
                // Ensure the target GameObject is active in the hierarchy.
                if (!targetTransform.gameObject.activeInHierarchy)
                {
                    hasTarget = false;
                }
                else
                {
                    // Check if the target is alive if it has a StarshipIdentity
                    // Note: This requires GetComponent, which can have a performance impact if used excessively in conditions.
                    if (targetTransform.TryGetComponent<StarshipIdentity>(out var targetIdentity))
                    {
                        hasTarget = targetIdentity.IsAlive;
                    }
                    else
                    {
                        // If it's a generic Transform (like a base or waypoint), assume it's valid if active.
                        hasTarget = true;
                    }
                }
            }

            // Compare the validation result with the expected state.
            return hasTarget == ShouldHaveTarget;
        }
    }
}