using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Behavior: Sentry. Navigates the ship along a predefined patrol route specific to the scene instance.
    /// The route (waypoints and pattern) is injected from the StarshipIdentity component upon creation.
    /// </summary>
    public class BehaviorSentry : SimpleAiBehaviorBase
    {
        private readonly SentryConfigSO _config;
        private readonly List<Transform> _waypoints;
        private readonly PatrolPattern _pattern;

        private int _currentWaypointIndex = 0;
        private bool _isComplete = false;
        // Throttling arrival checks improves performance, as distance checks every frame are unnecessary.
        private const float ArrivalCheckInterval = 0.5f;
        private float _arrivalCheckTimer;

        /// <summary>
        /// Initializes the BehaviorSentry with configuration and scene-specific patrol data.
        /// </summary>
        /// <param name="config">The configuration parameters for the Sentry behavior.</param>
        /// <param name="waypoints">The list of waypoints defining the route (from StarshipIdentity).</param>
        /// <param name="pattern">The patrol pattern (from StarshipIdentity).</param>
        public BehaviorSentry(SentryConfigSO config, List<Transform> waypoints, PatrolPattern pattern)
        {
            _config = config;
            _waypoints = waypoints;
            _pattern = pattern;

            if (_waypoints == null || _waypoints.Count == 0)
            {
                // If no waypoints are provided, the behavior completes immediately.
                _isComplete = true;
                return;
            }

            // Initialize the starting waypoint index.
            if (_pattern == PatrolPattern.Random)
            {
                _currentWaypointIndex = UnityEngine.Random.Range(0, _waypoints.Count);
            }
        }

        public override string GetName() => "Sentry";

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // Sentry behavior manages its own destinations internally. It does not use Brain.MoveTarget.

            if (_isComplete)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Check for arrival periodically.
            _arrivalCheckTimer -= Time.deltaTime;
            if (_arrivalCheckTimer <= 0f)
            {
                _arrivalCheckTimer = ArrivalCheckInterval;
                CheckWaypointArrival(pilot);
            }

            Transform currentTarget = GetCurrentWaypoint();
            if (currentTarget == null || _isComplete)
            {
                // If the current waypoint is invalid or the route is complete, idle.
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            return new NavigationGoal
            {
                TargetPosition = currentTarget.position,
                DesiredSpeed = null, // Use max speed allowed by personality.
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius
            };
        }

        /// <summary>
        /// Checks if the pilot has reached the current waypoint and advances the route if necessary.
        /// </summary>
        private void CheckWaypointArrival(SimpleAiPilot pilot)
        {
            Transform waypoint = GetCurrentWaypoint();
            if (waypoint != null)
            {
                float distanceSqr = (waypoint.position - pilot.Transform.position).sqrMagnitude;
                // Use ArrivalTolerance from ConfigSO
                if (distanceSqr <= _config.ArrivalTolerance * _config.ArrivalTolerance)
                {
                    AdvanceWaypoint();
                }
            }
            else
            {
                // Handle destroyed or missing waypoint by advancing the route.
                AdvanceWaypoint();
            }
        }


        private Transform GetCurrentWaypoint()
        {
            if (_currentWaypointIndex >= 0 && _currentWaypointIndex < _waypoints.Count)
            {
                return _waypoints[_currentWaypointIndex];
            }
            return null;
        }

        /// <summary>
        /// Advances the waypoint index based on the defined patrol pattern.
        /// </summary>
        private void AdvanceWaypoint()
        {
            switch (_pattern)
            {
                case PatrolPattern.Sequential:
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex >= _waypoints.Count)
                    {
                        _isComplete = true;
                    }
                    break;

                case PatrolPattern.Loop:
                    _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
                    break;

                case PatrolPattern.Random:
                    int nextIndex;
                    int attempts = 0;
                    // Attempt to find a new random waypoint that is not the current one (if multiple waypoints exist).
                    do
                    {
                        nextIndex = UnityEngine.Random.Range(0, _waypoints.Count);
                        attempts++;
                    } while (nextIndex == _currentWaypointIndex && _waypoints.Count > 1 && attempts < 10);
                    _currentWaypointIndex = nextIndex;
                    break;
            }
        }

        public override Transform GetCurrentTargetObject() => GetCurrentWaypoint();
    }

    /// <summary>
    /// Configuration ScriptableObject for the Sentry behavior.
    /// This configuration is unique as it requires scene-specific data (waypoints) injected during the factory process.
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Sentry", menuName = "Starship AI/Behavior Config/Sentry (Scene Specific)")]
    public class SentryConfigSO : BehaviorConfigSO
    {
        [Tooltip("The distance (in meters) at which the AI considers the waypoint reached and proceeds to the next one.")]
        public float ArrivalTolerance = 20f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            // Special factory implementation: Retrieve scene-specific data from the ship's StarshipIdentity.

            if (brain == null || brain.Identity == null)
            {
                Debug.LogError($"SentryConfigSO cannot create behavior without access to AiBrain and StarshipIdentity. Defaulting to Idle.");
                // Return null if prerequisites are missing; AiBrain will handle the fallback to Idle.
                return null;
            }

            StarshipIdentity identity = brain.Identity;

            // Read the waypoint list and pattern defined on the specific instance of the ship in the scene.
            List<Transform> waypoints = identity.PatrolWaypoints;
            PatrolPattern pattern = identity.PatrolPattern;

            // Create the BehaviorSentry, passing the configuration and the scene-specific data.
            return new BehaviorSentry(this, waypoints, pattern);
        }
    }
}