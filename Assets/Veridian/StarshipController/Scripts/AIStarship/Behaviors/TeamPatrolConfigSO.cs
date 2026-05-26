using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    public class BehaviorTeamPatrol : SimpleAiBehaviorBase
    {
        private readonly TeamPatrolConfigSO _config; // NEW

        // Internal state for managing waypoints
        private List<Transform> _waypoints = new();
        private int _currentWaypointIndex = -1;
        private Transform _currentTarget;

        public BehaviorTeamPatrol(TeamPatrolConfigSO config) // NEW
        {
            _config = config;
        }

        public override string GetName() => "Team Patrol (Sequential)";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);

            // Fetch waypoints from FactionManager based on the pilot's faction.
            // This behavior relies on global data (FactionManager), not Brain.MoveTarget.
            if (FactionManager.Instance != null && pilot.Brain != null && pilot.Brain.Identity != null)
            {
                Faction faction = pilot.Brain.Identity.FactionID;
                _waypoints = FactionManager.Instance.GetTeamWaypoints(faction);

                if (_waypoints == null || _waypoints.Count == 0)
                {
                    return;
                }

                // Optimization: Start at the waypoint closest to the ship's current position.
                FindNearestWaypoint();
            }
            else
            {
                Debug.LogError("BehaviorTeamPatrol: Missing FactionManager, AiBrain, or StarshipIdentity. Cannot initialize.", pilot.gameObject);
            }
        }

        private void FindNearestWaypoint()
        {
            float minDistanceSqr = float.MaxValue;
            int nearestIndex = -1;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i] == null) continue;

                float distanceSqr = (_waypoints[i].position - _pilot.Transform.position).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    nearestIndex = i;
                }
            }

            if (nearestIndex != -1)
            {
                _currentWaypointIndex = nearestIndex;
                _currentTarget = _waypoints[_currentWaypointIndex];
            }
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // This behavior manages its own targets internally.

            if (_waypoints == null || _waypoints.Count == 0)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // Handle case where the current target might have been destroyed
            if (_currentTarget == null)
            {
                if (!AdvanceToNextWaypoint())
                {
                    return NavigationGoal.Idle(pilot.Transform.position);
                }
            }

            // Check if we have arrived at our destination.
            float distanceToTargetSqr = (_currentTarget.position - pilot.Transform.position).sqrMagnitude;
            if (distanceToTargetSqr < _config.ArrivalTolerance * _config.ArrivalTolerance)
            {
                AdvanceToNextWaypoint();
            }

            // Check again if the target is null after advancing
            if (_currentTarget == null)
            {
                return NavigationGoal.Idle(pilot.Transform.position);
            }

            return new NavigationGoal
            {
                TargetPosition = _currentTarget.position,
                DesiredSpeed = null,
                ArrivalTolerance = _config.ArrivalTolerance,
                SlowDownRadius = _config.SlowDownRadius
            };
        }

        private bool AdvanceToNextWaypoint()
        {
            if (_waypoints.Count == 0) return false;

            // Robustly find the next valid (non-null) waypoint in the sequence, looping if necessary.
            int attempts = 0;
            do
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _waypoints.Count)
                {
                    _currentWaypointIndex = 0; // Loop back to the start
                }
                _currentTarget = _waypoints[_currentWaypointIndex];
                attempts++;

            } while (_currentTarget == null && attempts < _waypoints.Count);

            return _currentTarget != null;
        }

        public override Transform GetCurrentTargetObject()
        {
            return _currentTarget;
        }

        public override void OnExit(SimpleAiPilot pilot)
        {
            // Clean up internal state when the behavior is stopped.
            base.OnExit(pilot);
            _waypoints = null;
            _currentTarget = null;
            _currentWaypointIndex = -1;
        }
    }
    [CreateAssetMenu(fileName = "Config_TeamPatrol", menuName = "Starship AI/Behavior Config/Team Patrol")]
    public class TeamPatrolConfigSO : BehaviorConfigSO
    {
        public float ArrivalTolerance = 25f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            return new BehaviorTeamPatrol(this);
        }
    }
}