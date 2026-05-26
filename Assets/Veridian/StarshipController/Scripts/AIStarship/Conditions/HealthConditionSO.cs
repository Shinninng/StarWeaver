using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Condition that checks the health or shield status of the ship against a normalized threshold.
    /// </summary>
    [CreateAssetMenu(fileName = "Condition_Health", menuName = "Starship AI/Conditions/Health")]
    public class HealthConditionSO : AiConditionSO
    {
        /// <summary>
        /// The type of health value to check.
        /// </summary>
        public enum HealthType
        {
            Health,
            Shields
        }

        [Tooltip("The specific health attribute to evaluate (Health or Shields).")]
        public HealthType Type = HealthType.Health;

        [Tooltip("The comparison operation to perform against the threshold (e.g., LessThan, GreaterThan).")]
        public ComparisonType Comparison = ComparisonType.LessThan;

        [Range(0f, 1f)]
        [Tooltip("The normalized threshold (0.0 to 1.0) to compare the health/shield value against. E.g., 0.3 means 30%.")]
        public float Threshold = 0.5f;

        protected override bool CheckCondition(AiBrain brain)
        {
            IHealthProvider healthProvider = brain.HealthProvider;

            // If the health provider is missing or the ship is already dead, the condition fails.
            if (healthProvider == null || !healthProvider.IsAlive)
            {
                return false;
            }

            float currentValue;

            switch (Type)
            {
                case HealthType.Health:
                    // Fails if the ship doesn't support health (e.g., an invulnerable object).
                    if (!healthProvider.HasHealthCapability) return false;
                    currentValue = healthProvider.CurrentHealthNormalized;
                    break;
                case HealthType.Shields:
                    // Fails if the ship doesn't support shields.
                    if (!healthProvider.HasShieldCapability) return false;
                    currentValue = healthProvider.CurrentShieldsNormalized;
                    break;
                default:
                    return false;
            }

            // Perform the comparison
            return Comparison switch
            {
                ComparisonType.LessThan => currentValue < Threshold,
                ComparisonType.LessThanOrEqual => currentValue <= Threshold,
                ComparisonType.GreaterThan => currentValue > Threshold,
                ComparisonType.GreaterThanOrEqual => currentValue >= Threshold,
                ComparisonType.Equal => Mathf.Approximately(currentValue, Threshold),
                _ => false // Default case - switch expressions must be exhaustive
            };

        }
    }
}