using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// The central profile component for a starship, containing all configuration data and dynamic state.
    /// </summary>
    [DisallowMultipleComponent]
    public class StarshipIdentity : MonoBehaviour
    {
        [Header("Identity and Allegiance")]
        public Faction FactionID = Faction.TeamA;
        public RoleType Role = RoleType.Standard;

        // NEW FIELDS: Scene-specific patrol configuration (moved from SimpleAiController).
        [Header("Scene Patrol Configuration (Sentry)")]
        [Tooltip("The waypoints defining the patrol route for the Sentry behavior, specific to this scene instance.")]
        public List<Transform> PatrolWaypoints = new();
        [Tooltip("The pattern used to navigate the waypoints.")]
        public PatrolPattern PatrolPattern = PatrolPattern.Loop;

        [Header("Lifecycle Management")]
        [Tooltip("If checked, the FactionManager will respawn this ship after it is destroyed.")]
        public bool CanRespawn = false;

        [Tooltip("If checked, the ship's ammunition will be reset to maximum when the HealthComponent is Initialized (e.g., upon respawn).")]
        public bool ResetWeaponsOnRespawn = false;

        [Tooltip("If checked, this ship can be automatically deactivated by the FactionManager when far from the player to save performance. Requires AI components.")]
        public bool AllowDistanceBasedDeactivation = false;

        [Header("Sensory Configuration (Hysteresis)")]
        [SerializeField, Tooltip("The range at which this ship first considers itself 'in danger'. (Engage)")]
        private float EngageDangerDistance = 3000f;

        [SerializeField, Tooltip("The range an enemy must exceed for this ship to consider itself 'safe' again. (Disengage)")]
        private float DisengageDangerDistance = 4000f;

        [Header("Tactical Configuration (Leash/Arrival)")]
        [SerializeField, Tooltip("The maximum distance from a friendly base. Beyond this triggers a return.")]
        private float MaxHomeDistance = 4000f;

        [SerializeField, Tooltip("How close the ship must get to a leader/player to consider its 'Follow' task complete.")]
        private float ArrivalRadius = 150f;

        [Header("Dynamic State (Managed by other Systems)")]
        [SerializeField, Tooltip("The definitive flag for whether this ship is alive. Set by the health system.")]
        private bool _isAlive = true;

        // Public Accessors for Dynamic State
        public bool IsAlive => _isAlive;



        // Optimized Squared Distances
        public float EngageDangerDistanceSqr { get; private set; }
        public float DisengageDangerDistanceSqr { get; private set; }
        public float MaxHomeDistanceSqr { get; private set; }
        public float ArrivalRadiusSqr { get; private set; }

        // Cached Transform
        public Transform CachedTransform { get; private set; }

        private void Awake()
        {
            CachedTransform = transform;
        }

        private void OnEnable()
        {
            CalculateSquaredDistances();

            // FactionManager logic assumed from context
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.Register(this);
            }
            else
            {
                StartCoroutine(DelayedRegistration());
            }
        }

        private System.Collections.IEnumerator DelayedRegistration()
        {
            yield return null;
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.HandleShipDisabled(this);
            }
        }

        private void OnValidate()
        {
            if (DisengageDangerDistance < EngageDangerDistance)
            {
                DisengageDangerDistance = EngageDangerDistance;
            }
            CalculateSquaredDistances();
        }

        private void CalculateSquaredDistances()
        {
            EngageDangerDistanceSqr = EngageDangerDistance * EngageDangerDistance;
            DisengageDangerDistanceSqr = DisengageDangerDistance * DisengageDangerDistance;
            MaxHomeDistanceSqr = MaxHomeDistance * MaxHomeDistance;
            ArrivalRadiusSqr = ArrivalRadius * ArrivalRadius;
        }

        public bool IsLeader()
        {
            return Role == RoleType.Leader || Role == RoleType.Player;
        }

        public void SetAliveStatus(bool alive)
        {
            _isAlive = alive;
        }

        //  Gizmo visualization for the scene-specific patrol route.
        void OnDrawGizmosSelected()
        {
            if (PatrolWaypoints != null && PatrolWaypoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < PatrolWaypoints.Count; i++)
                {
                    if (PatrolWaypoints[i] == null)
                        continue;

                    // Use a generic radius for visualization
                    Gizmos.DrawWireSphere(PatrolWaypoints[i].position, 10f);

                    if (i < PatrolWaypoints.Count - 1 && PatrolWaypoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(PatrolWaypoints[i].position, PatrolWaypoints[i + 1].position);
                    }
                    else if (i == PatrolWaypoints.Count - 1 && PatrolPattern == PatrolPattern.Loop && PatrolWaypoints[0] != null)
                    {
                        Gizmos.DrawLine(PatrolWaypoints[i].position, PatrolWaypoints[0].position);
                    }
                }
            }
        }
    }
}