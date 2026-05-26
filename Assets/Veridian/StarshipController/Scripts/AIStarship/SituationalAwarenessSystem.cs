using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// The central intelligence hub for the AI system. Provides fast, centralized lookups for environmental and tactical data
    /// by leveraging the FactionManager's registries. Decouples individual AI brains from direct scene searches.
    /// </summary>
    public static class SituationalAwarenessSystem
    {

        /// <summary>
        /// Processes a query request from an AI agent and returns the relevant tactical information.
        /// </summary>
        /// <param name="request">The query details.</param>
        /// <returns>The response containing the status and found data.</returns>
        public static QueryResponse ProcessQuery(QueryRequest request)
        {
            // FactionManager is required for all remaining query types.
            if (FactionManager.Instance == null)
            {
                Debug.LogError("SituationalAwarenessSystem cannot function because FactionManager instance is missing.");
                return QueryResponse.Failure();
            }

            // Ensure the requester is valid and alive.
            if (request.Requester == null || !request.Requester.IsAlive)
            {
                return QueryResponse.Failure();
            }

            switch (request.Query)
            {
                case QueryType.CheckDanger: return CheckDanger(request);
                case QueryType.FindClosestHostile: return FindClosestHostile(request);
                case QueryType.FindClosestFriendlyLeader: return FindClosestFriendlyLeader(request);
                case QueryType.FindNearestHomeBase: return FindNearestHomeBase(request);
                case QueryType.CheckIfNearLeader: return CheckIfNearLeader(request);
                case QueryType.CheckIfTooFarFromHome: return CheckIfTooFarFromHome(request);
                // Removed FindNextRaceRing case

                default:
                    Debug.LogWarning($"Unknown QueryType: {request.Query}");
                    return QueryResponse.Failure();
            }
        }

        // Removed the FindNextRaceRing method entirely

        /// <summary>
        /// Checks if the requester is in immediate danger based on hostile proximity, incorporating hysteresis.
        /// </summary>
        private static QueryResponse CheckDanger(QueryRequest request)
        {
            StarshipIdentity requester = request.Requester;
            Vector3 requesterPos = requester.CachedTransform.position;
            bool currentlyInDanger = request.CurrentDangerState;

            List<StarshipIdentity> hostiles = FactionManager.Instance.GetHostiles(requester.FactionID);

            // Apply hysteresis: Use the Disengage distance if already in danger, otherwise use the Engage distance.
            float thresholdSqr = currentlyInDanger ? requester.DisengageDangerDistanceSqr : requester.EngageDangerDistanceSqr;

            for (int i = 0; i < hostiles.Count; i++)
            {
                StarshipIdentity hostile = hostiles[i];
                // Rely on FactionManager to provide only active/alive hostiles, but double-check for safety.
                if (hostile == null || !hostile.IsAlive) continue;

                float distSqr = (hostile.CachedTransform.position - requesterPos).sqrMagnitude;

                if (distSqr <= thresholdSqr)
                {
                    return QueryResponse.Boolean(true);
                }
            }
            return QueryResponse.Boolean(false);
        }

        /// <summary>
        /// Finds the closest active hostile ship to the requester.
        /// </summary>
        private static QueryResponse FindClosestHostile(QueryRequest request)
        {
            List<StarshipIdentity> hostiles = FactionManager.Instance.GetHostiles(request.Requester.FactionID);
            StarshipIdentity closest = FindClosestInList(request.Requester.CachedTransform.position, hostiles);
            return QueryResponse.Identity(closest);
        }

        /// <summary>
        /// Finds the closest active friendly leader (or player) to the requester.
        /// </summary>
        private static QueryResponse FindClosestFriendlyLeader(QueryRequest request)
        {
            List<StarshipIdentity> leaders = FactionManager.Instance.GetFriendlyLeaders(request.Requester.FactionID);
            // Exclude the requester itself if they happen to be a leader.
            StarshipIdentity closest = FindClosestInList(request.Requester.CachedTransform.position, leaders, request.Requester);
            return QueryResponse.Identity(closest);
        }

        /// <summary>
        /// Finds the nearest home base registered for the requester's faction.
        /// </summary>
        private static QueryResponse FindNearestHomeBase(QueryRequest request)
        {
            Vector3 requesterPos = request.Requester.CachedTransform.position;
            List<Transform> bases = FactionManager.Instance.GetHomeBases(request.Requester.FactionID);

            if (bases == null || bases.Count == 0)
            {
                return QueryResponse.Failure();
            }

            Transform closestBase = null;
            float minDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < bases.Count; i++)
            {
                Transform homeBase = bases[i];
                if (homeBase == null) continue;

                float distSqr = (homeBase.position - requesterPos).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestBase = homeBase;
                }
            }

            // Use the helper constructor to ensure the Transform is returned.
            return QueryResponse.TransformTarget(closestBase);
        }

        /// <summary>
        /// Checks if the requester is within the ArrivalRadius of their closest friendly leader.
        /// </summary>
        private static QueryResponse CheckIfNearLeader(QueryRequest request)
        {
            QueryResponse leaderResponse = FindClosestFriendlyLeader(request);

            if (!leaderResponse.Status)
            {
                // No leader found.
                return QueryResponse.Boolean(false);
            }

            Vector3 leaderPos = leaderResponse.FoundPosition;
            Vector3 requesterPos = request.Requester.CachedTransform.position;

            float arrivalRadiusSqr = request.Requester.ArrivalRadiusSqr;
            float distSqr = (leaderPos - requesterPos).sqrMagnitude;

            return QueryResponse.Boolean(distSqr <= arrivalRadiusSqr);
        }

        /// <summary>
        /// Checks if the requester has strayed beyond the MaxHomeDistance from their nearest home base.
        /// </summary>
        private static QueryResponse CheckIfTooFarFromHome(QueryRequest request)
        {
            QueryResponse baseResponse = FindNearestHomeBase(request);

            if (!baseResponse.Status)
            {
                // No base found, assume not too far (or behavior handles lack of base).
                return QueryResponse.Boolean(false);
            }

            Vector3 basePos = baseResponse.FoundPosition;
            Vector3 requesterPos = request.Requester.CachedTransform.position;

            float maxHomeDistSqr = request.Requester.MaxHomeDistanceSqr;
            float distSqr = (basePos - requesterPos).sqrMagnitude;

            return QueryResponse.Boolean(distSqr > maxHomeDistSqr);
        }

        /// <summary>
        /// Helper method to find the closest StarshipIdentity in a list from a given origin point.
        /// </summary>
        private static StarshipIdentity FindClosestInList(Vector3 origin, List<StarshipIdentity> targets, StarshipIdentity exclude = null)
        {
            StarshipIdentity closestTarget = null;
            float minDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < targets.Count; i++)
            {
                StarshipIdentity target = targets[i];

                // Ensure the target is valid, alive, and not the excluded identity.
                if (target == null || !target.IsAlive || target == exclude) continue;

                float distSqr = (target.CachedTransform.position - origin).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestTarget = target;
                }
            }
            return closestTarget;
        }
    }
}