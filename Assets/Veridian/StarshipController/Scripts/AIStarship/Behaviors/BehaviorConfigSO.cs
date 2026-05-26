using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Abstract base class for all behavior configuration ScriptableObjects.
    /// Implements the Factory pattern, providing a standardized way to create specific ISimpleAiBehavior instances.
    /// Also holds common parameters shared across many behaviors.
    /// </summary>
    public abstract class BehaviorConfigSO : ScriptableObject
    {
        [Header("Global Behavior Parameters")]
        [Tooltip("The radius (in meters) around the destination where the AI pilot will start slowing down. Used by most behaviors (Gofer, Sentry, Attack, etc.). Set to 0 to disable automatic slowdown.")]
        public float SlowDownRadius = 150f;

        /// <summary>
        /// Factory method responsible for creating the corresponding ISimpleAiBehavior instance.
        /// Must be implemented by derived configuration classes.
        /// </summary>
        /// <param name="brain">The AiBrain instance requesting the behavior. Used to access context required for certain behaviors (e.g., Sentry needs scene-specific data from Identity).</param>
        /// <returns>A new instance of the specific behavior.</returns>
        public abstract ISimpleAiBehavior CreateBehavior(AiBrain brain);
    }
}