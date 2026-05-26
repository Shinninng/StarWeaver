using UnityEngine;

namespace Veridian.Starship.AI
{
    // Structure to hold the desired navigation state provided by behaviors
    public struct NavigationGoal
    {
        // The immediate target position the AI should move towards.
        public Vector3 TargetPosition;

        // The desired speed for this movement. If null, the pilot uses max speed based on personality.
        public float? DesiredSpeed;

        // The distance at which the AI considers this goal reached.
        public float ArrivalTolerance;

        // The radius at which the pilot should start slowing down. 0 disables slowdown.
        public float SlowDownRadius;

        public static NavigationGoal Idle(Vector3 position)
        {
            return new NavigationGoal
            {
                TargetPosition = position,
                DesiredSpeed = 0,
                ArrivalTolerance = 1f,
                SlowDownRadius = 0f
            };
        }
    }

    public interface ISimpleAiBehavior
    {
        string GetName();
        void Initialize(SimpleAiPilot pilot);
        // Update the behavior logic and return the current navigation goal.
        NavigationGoal UpdateGoal(SimpleAiPilot pilot);
        void OnExit(SimpleAiPilot pilot);

        // Optional method for behaviors to report their current target object (for debugging).
        Transform GetCurrentTargetObject();
    }
}