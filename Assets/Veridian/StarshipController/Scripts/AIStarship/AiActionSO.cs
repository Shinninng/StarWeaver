using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Represents a high-level tactical decision or 'tactic' the AI can take.
    /// It defines the conditions required to activate a specific behavior (AiMode) and the logic for acquiring targets.
    /// Actions are evaluated in a prioritized list within the AiBrain.
    /// </summary>
    [CreateAssetMenu(fileName = "Action_NewTactic", menuName = "Starship AI/Action (Tactic)")]
    public class AiActionSO : ScriptableObject
    {
        [Header("Behavior Link")]
        [Tooltip("The specific AI Behavior Mode to activate when this action is selected by the AiBrain.")]
        public AiMode BehaviorToActivate = AiMode.Idle;

        [Header("Conditions (Rules)")]
        [Tooltip("A list of conditions (rules) that must all be true for this action to be considered valid. Evaluated every Think cycle.")]
        public List<AiConditionSO> Conditions = new List<AiConditionSO>();

        [Header("Behavioral Inertia (Probability)")]
        [Tooltip("The probability (0-1) that the AI will transition into this action if its conditions are met and it has priority. 1.0 means it will always enter if valid.")]
        [Range(0f, 1f)]
        public float EnterProbability = 1.0f;

        [Tooltip("The probability (0-1) that the AI will remain in this action during the next Think cycle, provided its conditions are still met. Higher values create 'stickier' behaviors and reduce rapid switching.")]
        [Range(0f, 1f)]
        public float StayProbability = 0.95f;

        [Header("Time Constraints (Optional)")]
        [Tooltip("If greater than 0, the AI will be forced to stay in this action for this duration (in seconds), even if conditions become false or a higher priority action becomes valid.")]
        public float ActiveDuration = 0f;

        [Tooltip("If greater than 0, the time (in seconds) after exiting this action before it can be activated again.")]
        public float CooldownDuration = 0f;

        [Header("Targeting (Optional)")]
        [Tooltip("The method used to acquire a target when this action starts or updates. 'None' means no target acquisition is performed by this action. Note: All targets are automatically cleared when switching actions.")]
        public TargetAcquisitionType AcquisitionType = TargetAcquisitionType.None;

        [Tooltip("The slot (MoveTarget or AttackTarget) where the acquired target should be stored in the AiBrain.")]
        public TargetSlot TargetSlot = TargetSlot.MoveTarget;

        // Note on Implementation: AiBrain enforces unconditional clearing of all targets upon action transition.
        // This simplifies action logic, as actions do not need to manage clearing previous targets.

        /// <summary>
        /// Evaluates all conditions associated with this action.
        /// </summary>
        /// <param name="brain">The AiBrain executing the action.</param>
        /// <returns>True if all conditions are met, false otherwise.</returns>
        public bool AreConditionsMet(AiBrain brain)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i] == null) continue;

                if (!Conditions[i].Check(brain))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Executes the targeting logic defined in the action based on the AcquisitionType.
        /// This is called when the action starts and every subsequent Think cycle if the AI remains in this action.
        /// </summary>
        /// <param name="brain">The AiBrain executing the action.</param>
        public void ExecuteTargeting(AiBrain brain)
        {
            // When a new action starts (TransitionToNewAction), AiBrain guarantees targets are cleared before this call.
            // When staying in the same action (Think), targets persist from the previous cycle, allowing persistence logic (like in FindRandomTeamWaypoint) to function.

            if (AcquisitionType == TargetAcquisitionType.None || brain.Identity == null)
            {
                return;
            }

            // Handle specific acquisition types that require persistence or access external systems directly.

            if (AcquisitionType == TargetAcquisitionType.FindRandomTeamWaypoint)
            {
                // PERSISTENCE: If we already have a move target (set during this action's lifetime), do not find a new one.
                // This check specifically handles the case where the target slot is MoveTarget.
                // Note: If TargetSlot was AttackTarget, this persistence logic would need adjustment if similar behavior was desired for attack targets.
                if (brain.MoveTarget != null && TargetSlot == TargetSlot.MoveTarget)
                {
                    return;
                }

                if (FactionManager.Instance != null)
                {
                    var waypoints = FactionManager.Instance.GetTeamWaypoints(brain.Identity.FactionID);
                    if (waypoints != null && waypoints.Count > 0)
                    {
                        Transform randomWaypoint = waypoints[Random.Range(0, waypoints.Count)];
                        SetTarget(brain, randomWaypoint);
                    }
                }
                return;
            }

            if (AcquisitionType == TargetAcquisitionType.FindPlayer)
            {
                if (FactionManager.Instance != null)
                {
                    StarshipIdentity playerIdentity = FactionManager.Instance.GetPlayerIdentity();
                    if (playerIdentity != null && playerIdentity.CachedTransform != null)
                    {
                        SetTarget(brain, playerIdentity.CachedTransform);
                    }
                }
                return;
            }

            // Handle standard Situational Awareness System (SAS) queries.
            QueryType queryType;
            switch (AcquisitionType)
            {
                case TargetAcquisitionType.FindClosestHostile:
                    queryType = QueryType.FindClosestHostile;
                    break;
                case TargetAcquisitionType.FindClosestFriendlyLeader:
                    queryType = QueryType.FindClosestFriendlyLeader;
                    break;
                case TargetAcquisitionType.FindNearestHomeBase:
                    queryType = QueryType.FindNearestHomeBase;
                    break;
                case TargetAcquisitionType.FindNextRaceRing:
                    queryType = QueryType.FindNextRaceRing;
                    break;
                default:
                    // This case handles any new types added to the enum but not implemented here.
                    return;
            }

            QueryRequest request = new QueryRequest(brain.Identity, queryType);
            QueryResponse response = SituationalAwarenessSystem.ProcessQuery(request);

            if (response.Status && response.FoundTransform != null)
            {
                SetTarget(brain, response.FoundTransform);
            }
        }

        /// <summary>
        /// Assigns the acquired transform to the specified target slot in the AiBrain.
        /// </summary>
        private void SetTarget(AiBrain brain, Transform target)
        {
            switch (TargetSlot)
            {
                case TargetSlot.MoveTarget:
                    brain.SetMoveTarget(target);
                    break;
                case TargetSlot.AttackTarget:
                    brain.SetAttackTarget(target);
                    break;
            }
        }
    }
}