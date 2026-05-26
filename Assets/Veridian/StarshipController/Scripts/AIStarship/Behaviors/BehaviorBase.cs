using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// An abstract base class for AI behaviors, implementing the ISimpleAiBehavior interface.
    /// Provides common functionality like storing a reference to the pilot and default implementations for lifecycle methods.
    /// </summary>
    public abstract class SimpleAiBehaviorBase : ISimpleAiBehavior
    {
        // Protected field to hold the pilot reference, accessible by all derived classes.
        protected SimpleAiPilot _pilot;

        /// <summary>
        /// Returns the name of the behavior for debugging purposes.
        /// </summary>
        public abstract string GetName();

        /// <summary>
        /// Initializes the behavior. Stores the pilot reference.
        /// Derived classes should call base.Initialize(pilot) if they override this method.
        /// </summary>
        /// <param name="pilot">The SimpleAiPilot executing this behavior.</param>
        public virtual void Initialize(SimpleAiPilot pilot)
        {
            _pilot = pilot;
        }

        /// <summary>
        /// Updates the behavior logic and determines the navigation goal for the current frame.
        /// </summary>
        /// <param name="pilot">The SimpleAiPilot executing this behavior.</param>
        /// <returns>The calculated NavigationGoal.</returns>
        public abstract NavigationGoal UpdateGoal(SimpleAiPilot pilot);

        /// <summary>
        /// Called when the behavior is deactivated. Provides a default empty implementation.
        /// Derived classes can override this to perform cleanup logic.
        /// </summary>
        /// <param name="pilot">The SimpleAiPilot executing this behavior.</param>
        public virtual void OnExit(SimpleAiPilot pilot)
        {
            // No default exit behavior.
        }

        /// <summary>
        /// Returns the primary Transform target associated with this behavior (if any) for debugging or external visualization.
        /// </summary>
        public abstract Transform GetCurrentTargetObject();
    }
}