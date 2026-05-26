using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Base class for all AI conditions. A condition is a single, reusable rule used by AiActionSO to determine validity.
    /// Conditions are ScriptableObjects, allowing them to be easily configured and shared across different AI setups.
    /// </summary>
    public abstract class AiConditionSO : ScriptableObject
    {
        [Tooltip("If true, the result of the condition check will be inverted (e.g., 'Is Healthy' becomes 'Is NOT Healthy').")]
        public bool Invert = false;

        /// <summary>
        /// The core logic implementation of the condition. Must be implemented by derived classes.
        /// </summary>
        /// <param name="brain">The AiBrain evaluating the condition.</param>
        /// <returns>The raw result of the condition check.</returns>
        protected abstract bool CheckCondition(AiBrain brain);

        /// <summary>
        /// Public entry point for checking the condition. Handles the inversion logic.
        /// </summary>
        /// <param name="brain">The AiBrain evaluating the condition.</param>
        /// <returns>The final result of the condition check after applying inversion.</returns>
        public bool Check(AiBrain brain)
        {
            bool result = CheckCondition(brain);
            return Invert ? !result : result;
        }
    }
}