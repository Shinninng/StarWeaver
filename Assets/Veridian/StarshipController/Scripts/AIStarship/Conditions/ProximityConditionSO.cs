using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Condition that checks the proximity of a specific type of target (found via SAS) against a distance threshold.
    /// </summary>
    [CreateAssetMenu(fileName = "Condition_Proximity", menuName = "Starship AI/Conditions/Proximity")]
    public class ProximityConditionSO : AiConditionSO
    {
        [Tooltip("The type of target to check the distance against. This determines the query sent to the Situational Awareness System.")]
        public ProximityTargetType TargetType = ProximityTargetType.ClosestHostile;

        [Tooltip("The comparison operation to perform against the distance threshold (e.g., LessThan, GreaterThan).")]
        public ComparisonType Comparison = ComparisonType.LessThan;

        [Tooltip("The distance threshold (in meters). The condition evaluates based on whether the actual distance meets the comparison criteria relative to this value.")]
        public float Distance = 100f;

        protected override bool CheckCondition(AiBrain brain)
        {
            if (brain.Identity == null) return false;

            Vector3 requesterPos = brain.Identity.CachedTransform.position;
            Vector3 targetPos;

            // Handle standard Situational Awareness System (SAS) queries.
            QueryType queryType;
            switch (TargetType)
            {
                case ProximityTargetType.ClosestHostile:
                    queryType = QueryType.FindClosestHostile;
                    break;
                case ProximityTargetType.ClosestFriendlyLeader:
                    queryType = QueryType.FindClosestFriendlyLeader;
                    break;
                case ProximityTargetType.NearestHomeBase:
                    queryType = QueryType.FindNearestHomeBase;
                    break;
                // Note: RacecourseCenter case has been removed.
                default:
                    // Handles any unimplemented or removed types.
                    Debug.LogWarning($"ProximityConditionSO on {this.name} encountered an unhandled TargetType: {TargetType}", this);
                    return false;
            }

            QueryRequest request = new(brain.Identity, queryType);
            QueryResponse response = SituationalAwarenessSystem.ProcessQuery(request);

            if (!response.Status)
            {
                return false; // Target not found via SAS.
            }
            targetPos = response.FoundPosition;


            // Perform the distance check using squared values for performance optimization.
            float distanceSqr = (requesterPos - targetPos).sqrMagnitude;
            float thresholdSqr = Distance * Distance;

            return Comparison switch
            {
                ComparisonType.LessThan => distanceSqr < thresholdSqr,
                ComparisonType.LessThanOrEqual => distanceSqr <= thresholdSqr,
                ComparisonType.GreaterThan => distanceSqr > thresholdSqr,
                ComparisonType.GreaterThanOrEqual => distanceSqr >= thresholdSqr,
                ComparisonType.Equal => Mathf.Approximately(distanceSqr, thresholdSqr),
                _ => false // Default case
            };

        }
    }
}