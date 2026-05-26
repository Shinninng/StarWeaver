using UnityEngine;

namespace Veridian.Starship.Core
{
    // Centralized definition of AI Behavior Modes, formerly in SimpleAiController.
    public enum AiMode
    {
        Idle,
        Sentry,
        Loiterer,
        Guardian,
        Gofer,
        Attack,
        Flee,
        TeamPatrol,
        AltitudeCorrection,
        Takeoff,
        Landing,
    }

    // Patrol pattern definition, formerly associated with SimpleAiController data classes.
    // Renamed from SimplePatrolPattern to PatrolPattern for centralized use.
    public enum PatrolPattern
    {
        Loop,
        Sequential,
        Random
    }

    // (Enums below remain unchanged from context)
    public enum ComparisonType
    {
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal
    }

    public enum ProximityTargetType
    {
        ClosestHostile,
        ClosestFriendlyLeader,
        NearestHomeBase,
        RacecourseCenter
    }

    // Used by AiActionSO to specify how to acquire a target when the action activates
    public enum TargetAcquisitionType
    {
        None,
        FindClosestHostile,
        FindClosestFriendlyLeader,
        FindNearestHomeBase,
        FindNextRaceRing,
        FindRacecourseCenter,
        FindRandomTeamWaypoint,
        FindPlayer
    }

    public enum TargetSlot
    {
        MoveTarget,
        AttackTarget
    }

    public enum Faction
    {
        TeamA,
        TeamB
    }

    public enum RoleType
    {
        Standard,
        Follower,
        Leader,
        Player,
        Turret
    }

    public enum QueryType
    {
        CheckDanger,
        FindClosestHostile,
        FindClosestFriendlyLeader,
        FindNearestHomeBase,
        CheckIfNearLeader,
        CheckIfTooFarFromHome,
        FindNextRaceRing
    }

    // NEW INTERFACE
    /// <summary>
    /// Interface for components (typically in the AI assembly) that need to reset their
    /// internal initialization state when the object is being respawned (triggered by Core).
    /// This ensures that OnEnable performs a full reset during respawn, but preserves state
    /// during distance-based reactivation.
    /// </summary>
    public interface IRespawnResettable
    {
        void PrepareForRespawn();
    }

    public struct QueryRequest
    {
        public StarshipIdentity Requester;
        public QueryType Query;
        public bool CurrentDangerState;

        public QueryRequest(StarshipIdentity requester, QueryType query, bool currentDangerState = false)
        {
            Requester = requester;
            Query = query;
            CurrentDangerState = currentDangerState;
        }
    }

    public struct QueryResponse
    {
        public bool Status;
        public StarshipIdentity FoundIdentity;
        public Vector3 FoundPosition;
        public Transform FoundTransform;
        public readonly GameObject FoundObject => FoundTransform != null ? FoundTransform.gameObject : null;
        public static QueryResponse Failure() => new() { Status = false };

        public static QueryResponse Boolean(bool status) =>
            new()
            { Status = status };

        public static QueryResponse Identity(StarshipIdentity identity)
        {
            if (identity == null)
                return Failure();
            return new QueryResponse
            {
                Status = true,
                FoundIdentity = identity,
                FoundTransform = identity.CachedTransform,
                FoundPosition = identity.CachedTransform.position
            };
        }

        public static QueryResponse TransformTarget(Transform target)
        {
            if (target == null)
                return Failure();
            return new QueryResponse
            {
                Status = true,
                FoundTransform = target,
                FoundPosition = target.position,
                FoundIdentity = target.GetComponent<StarshipIdentity>()
            };
        }

        public static QueryResponse Position(Vector3 position) =>
            new()
            { Status = true, FoundPosition = position };
    }
}