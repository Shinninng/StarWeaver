using System;
using System.Collections; // Required for Coroutines
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Defines the lifecycle status of a ship managed by the FactionManager.
    /// </summary>
    public enum ShipStatus
    {
        Active,
        Inactive,
        Destroyed
    }

    /// <summary>
    /// Internal helper class used by FactionManager to track the lifecycle state and components of a managed ship.
    /// </summary>
    public class ManagedShipState
    {
        public GameObject ShipGameObject;
        public HealthComponent HealthComp;
        public AtmosphericStarshipController Controller;
        public ShipStatus Status;
        public float RespawnTimer;

        public ManagedShipState(GameObject obj, HealthComponent health, AtmosphericStarshipController ctrl)
        {
            ShipGameObject = obj;
            HealthComp = health;
            Controller = ctrl;
            Status = ShipStatus.Active;
            RespawnTimer = 0f;
        }
    }

    /// <summary>
    /// The central, authoritative directory for the entire simulation.
    /// Manages ship rosters, lifecycle (destruction/respawn), and provides fast intelligence lookups for AI and gameplay systems.
    /// Functions as a singleton.
    /// </summary>
    public class FactionManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the FactionManager.
        /// </summary>
        public static FactionManager Instance { get; private set; }

        [Header("Respawn Configuration")]
        [SerializeField, Tooltip("The Transform designated as the primary respawn location for Team A.")]
        private Transform TeamARespawnPoint;

        [SerializeField, Tooltip("The Transform designated as the primary respawn location for Team B.")]
        private Transform TeamBRespawnPoint;

        [SerializeField, Tooltip("The maximum distance (radius in meters) from the respawn point where a ship can reappear. Provides spawn dispersal.")]
        private float RespawnRadius = 50f;

        [SerializeField, Tooltip("The duration in seconds between a ship's destruction and its subsequent respawn.")]
        private float RespawnTime = 5.0f;

        // NEW SECTION: Distance-Based Activation
        [Header("Distance-Based Activation (Optimization)")]
        [SerializeField, Tooltip("Enable the system to automatically deactivate distant AI ships flagged with AllowDistanceBasedDeactivation.")]
        private bool enableDistanceBasedActivation = true;

        [SerializeField, Tooltip("The distance (meters) beyond which eligible AI ships will be deactivated.")]
        private float deactivationDistance = 5000f;

        [SerializeField, Tooltip("The distance (meters) within which deactivated AI ships will be reactivated. Should be slightly smaller than Deactivation Distance.")]
        private float reactivationDistance = 4800f;

        [SerializeField, Tooltip("How often (seconds) the system checks distances. Lower values are more responsive but use more CPU.")]
        private float distanceCheckInterval = 1.5f;

        private float _deactivationDistanceSqr;
        private float _reactivationDistanceSqr;
        private Coroutine _distanceCheckCoroutine;
        // End NEW SECTION

        [SerializeField, Tooltip("Runtime cache of the active player ship's identity. Used for quick lookups by other systems. (Read-only)")]
        private StarshipIdentity _cachedPlayer;

        /// <summary>
        /// Event invoked when the player's ship specifically is respawned. Primarily used by the camera system to re-target.
        /// </summary>
        public event Action<Transform> OnPlayerShipRespawned;

        // Master Lifecycle Registry: The single source of truth for every ship's state.
        private Dictionary<StarshipIdentity, ManagedShipState> _managedShips;

        // --- CORE REGISTRIES (Rosters - Optimized for performance) ---
        // (Registries remain unchanged)
        private Dictionary<Faction, List<StarshipIdentity>> _allShipsRegistry;
        private Dictionary<Faction, List<StarshipIdentity>> _leadersRegistry;
        private Dictionary<Faction, List<StarshipIdentity>> _followersRegistry;

        [Header("Faction Rally Points (Home Bases)")]
        [SerializeField, Tooltip("List of Transforms representing the home bases or safe rally points for Team A.")]
        private List<Transform> TeamABases = new();

        [SerializeField, Tooltip("List of Transforms representing the home bases or safe rally points for Team B.")]
        private List<Transform> TeamBBases = new();

        [Header("Faction Strategic Waypoints")]
        [SerializeField, Tooltip("List of strategic waypoints used by Team A for tactical maneuvers (e.g., TeamPatrol behavior).")]
        private List<Transform> TeamAWaypoints = new();

        [SerializeField, Tooltip("List of strategic waypoints used by Team B for tactical maneuvers.")]
        private List<Transform> TeamBWaypoints = new();

        // --- DEBUG VIEW (For Inspector visibility at runtime) ---
        [Header("DEBUG - Faction Rosters (Runtime)")]
        [SerializeField, Tooltip("Runtime view of Team A's active leaders and players. (Read-only)")]
        private List<StarshipIdentity> _teamALeadersView = new();

        [SerializeField, Tooltip("Runtime view of Team A's active followers. (Read-only)")]
        private List<StarshipIdentity> _teamAFollowersView = new();

        [SerializeField, Tooltip("Runtime view of Team B's active leaders and players. (Read-only)")]
        private List<StarshipIdentity> _teamBLeadersView = new();

        [SerializeField, Tooltip("Runtime view of Team B's active followers. (Read-only)")]
        private List<StarshipIdentity> _teamBFollowersView = new();

        // Static empty lists to prevent unnecessary allocations when returning empty results.
        private static readonly List<StarshipIdentity> EmptyRoster = new();
        private static readonly List<Transform> EmptyBases = new();
        private static readonly List<Transform> EmptyWaypoints = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Duplicate FactionManager detected. Destroying new instance.");
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                InitializeRegistries();
                InitializeDistanceOptimization(); // NEW
            }
        }

        // NEW METHOD
        private void InitializeDistanceOptimization()
        {
            _deactivationDistanceSqr = deactivationDistance * deactivationDistance;
            _reactivationDistanceSqr = reactivationDistance * reactivationDistance;

            if (reactivationDistance >= deactivationDistance)
            {
                Debug.LogWarning("FactionManager: Reactivation Distance should be less than Deactivation Distance to prevent rapid state changes (hysteresis).", this);
            }
        }

        // NEW METHOD
        void OnEnable()
        {
            // Start the distance check coroutine when the manager is enabled.
            if (enableDistanceBasedActivation)
            {
                StartDistanceChecks();
            }
        }

        // NEW METHOD
        void OnDisable()
        {
            // Ensure the coroutine stops if the manager is disabled.
            StopDistanceChecks();
        }

        // NEW METHOD
        public void StartDistanceChecks()
        {
            _distanceCheckCoroutine ??= StartCoroutine(DistanceCheckCoroutine());
        }

        // NEW METHOD
        public void StopDistanceChecks()
        {
            if (_distanceCheckCoroutine != null)
            {
                StopCoroutine(_distanceCheckCoroutine);
                _distanceCheckCoroutine = null;
            }
        }

        private void InitializeRegistries()
        {
            // (Unchanged)
            _managedShips = new Dictionary<StarshipIdentity, ManagedShipState>(); // Initialize the lifecycle registry

            _allShipsRegistry = new Dictionary<Faction, List<StarshipIdentity>>();
            _leadersRegistry = new Dictionary<Faction, List<StarshipIdentity>>();
            _followersRegistry = new Dictionary<Faction, List<StarshipIdentity>>();

            // Initialize lists for all defined factions
            foreach (Faction faction in Enum.GetValues(typeof(Faction)))
            {
                _allShipsRegistry[faction] = new List<StarshipIdentity>();
                _leadersRegistry[faction] = new List<StarshipIdentity>();
                _followersRegistry[faction] = new List<StarshipIdentity>();
            }
        }

        void Update()
        {
            // Process the respawn queue every frame.
            HandleRespawnTimers();
        }

        // NEW METHOD: The core optimization logic
        private IEnumerator DistanceCheckCoroutine()
        {
            // Add a small random delay initially to stagger checks if needed.
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, distanceCheckInterval));

            while (true)
            {
                if (!enableDistanceBasedActivation || _managedShips == null)
                {
                    yield return new WaitForSeconds(distanceCheckInterval);
                    continue;
                }

                // 1. Identify the reference point (Player)
                Transform referenceTransform = GetPlayerTransform();

                if (referenceTransform == null)
                {
                    // If no player is active, we cannot perform distance checks reliably.
                    yield return new WaitForSeconds(distanceCheckInterval);
                    continue;
                }

                Vector3 referencePosition = referenceTransform.position;

                // 2. Iterate through all managed ships
                // Iterating over the Dictionary is efficient.
                foreach (var entry in _managedShips)
                {
                    StarshipIdentity ship = entry.Key;
                    ManagedShipState state = entry.Value;

                    // Skip ships not eligible for this optimization
                    if (!ship.AllowDistanceBasedDeactivation)
                        continue;

                    // Skip ships that are already destroyed (they are already inactive)
                    if (state.Status == ShipStatus.Destroyed)
                        continue;

                    // Calculate squared distance
                    float distanceSqr = (state.ShipGameObject.transform.position - referencePosition).sqrMagnitude;

                    // 3. Evaluate state transitions
                    if (state.Status == ShipStatus.Active)
                    {
                        if (distanceSqr > _deactivationDistanceSqr)
                        {
                            DeactivateShipTemporarily(ship);
                        }
                    }
                    else if (state.Status == ShipStatus.Inactive)
                    {
                        if (distanceSqr < _reactivationDistanceSqr)
                        {
                            ReactivateShip(ship);
                        }
                    }
                }

                // Wait for the next interval
                yield return new WaitForSeconds(distanceCheckInterval);
            }
        }

        // NEW HELPER METHOD
        private Transform GetPlayerTransform()
        {
            // Use the cached player identity provided by GetPlayerIdentity()
            StarshipIdentity player = GetPlayerIdentity();

            if (player != null && player.CachedTransform != null)
            {
                return player.CachedTransform;
            }
            return null;
        }

        // --- Registration and Lifecycle ---
        // (Register, HandleShipDisabled, DeactivateShipTemporarily, ReactivateShip, OnShipDestroyed, HandleRespawnTimers remain unchanged)

        // (Implementation details for unchanged methods omitted for brevity, they are identical to the provided context)
        public void Register(StarshipIdentity ship)
        {
            if (ship == null)
                return;

            if (ship.Role == RoleType.Player)
            {
                _cachedPlayer = ship;
            }

            // Check if the ship is already managed.
            if (_managedShips.ContainsKey(ship))
            {
                // If it's already registered, ensure its state is correctly set (e.g., if pooling and re-enabling).
                ManagedShipState state = _managedShips[ship];
                if (state.Status != ShipStatus.Active && ship.IsAlive)
                {
                    // If the ship is being re-enabled (e.g. via OnEnable) and is alive, we treat it similar to reactivation.
                    // If it was Destroyed, it must go through ExecuteRespawn.
                    if (state.Status == ShipStatus.Inactive)
                    {
                        ReactivateShip(ship);
                    }
                }
                return;
            }

            // 1. Get required components
            HealthComponent health = ship.GetComponent<HealthComponent>();
            // The controller is optional; turrets won't have one.
            AtmosphericStarshipController controller = ship.GetComponent<AtmosphericStarshipController>();

            // We ONLY require a HealthComponent to be registered.
            if (health == null)
            {
                Debug.LogError($"Ship {ship.name} is missing the required HealthComponent and cannot be registered.", ship);
                return;
            }

            // 2. Create and store the managed state (starts as Active by default)
            // Pass the potentially null controller to the state object.
            ManagedShipState newState = new(ship.gameObject, health, controller);
            _managedShips[ship] = newState;

            // 3. Subscribe to the death event
            // Ensure we don't double-subscribe if Register is called multiple times (though the dictionary check prevents this path).
            health.OnDeathEvent -= OnShipDestroyed;
            health.OnDeathEvent += OnShipDestroyed;

            // 4. Add to active rosters if alive
            if (ship.IsAlive)
            {
                AddToActiveRosters(ship);
            }
            else
            {
                // If the ship is initialized dead, set its status accordingly.
                newState.Status = ShipStatus.Destroyed;
            }
        }

        public void HandleShipDisabled(StarshipIdentity ship)
        {
            if (ship == null || !_managedShips.ContainsKey(ship))
                return;

            ManagedShipState state = _managedShips[ship];

            // If the ship was active when disabled, we need to update its status and remove it from active rosters.
            if (state.Status == ShipStatus.Active)
            {
                // We treat unexpected disabling as temporary inactivation.
                state.Status = ShipStatus.Inactive;
                RemoveFromActiveRosters(ship);
            }
            // If the status was already Inactive or Destroyed (handled by OnShipDestroyed), the rosters are already correct.
        }

        public void DeactivateShipTemporarily(StarshipIdentity ship)
        {
            if (ship != null && _managedShips.TryGetValue(ship, out ManagedShipState state))
            {
                if (state.Status == ShipStatus.Active)
                {
                    state.Status = ShipStatus.Inactive;
                    RemoveFromActiveRosters(ship);
                    state.ShipGameObject.SetActive(false);
                }
            }
        }

        public void ReactivateShip(StarshipIdentity ship)
        {
            if (ship != null && _managedShips.TryGetValue(ship, out ManagedShipState state))
            {
                // Only reactivate if currently inactive.
                if (state.Status == ShipStatus.Inactive)
                {
                    // Clear physics state first to prevent erratic movement upon reactivation.
                    if (state.Controller != null)
                    {
                        state.Controller.ResetAndClearPhysicsState();
                    }

                    state.Status = ShipStatus.Active;
                    state.ShipGameObject.SetActive(true);

                    AddToActiveRosters(ship);
                }
            }
        }

        private void OnShipDestroyed(StarshipIdentity destroyedShip)
        {
            if (destroyedShip != null && _managedShips.TryGetValue(destroyedShip, out ManagedShipState state))
            {
                // 1. Update Status
                state.Status = ShipStatus.Destroyed;

                // 2. Remove from active simulation
                RemoveFromActiveRosters(destroyedShip);
                // Immediately deactivate the GameObject. This stops rendering and most script execution.
                state.ShipGameObject.SetActive(false);

                // 3. Check for Respawn
                if (destroyedShip.CanRespawn)
                {
                    state.RespawnTimer = RespawnTime;
                }
            }
        }

        private void HandleRespawnTimers()
        {
            // Iterate over all managed ships to check for pending respawns.
            foreach (var entry in _managedShips)
            {
                ManagedShipState state = entry.Value;

                if (state.Status == ShipStatus.Destroyed && state.RespawnTimer > 0)
                {
                    state.RespawnTimer -= Time.deltaTime;

                    if (state.RespawnTimer <= 0)
                    {
                        // Reset timer to prevent re-triggering
                        state.RespawnTimer = 0;
                        ExecuteRespawn(entry.Key, state);
                    }
                }
            }
        }

        private void ExecuteRespawn(StarshipIdentity shipToRespawn, ManagedShipState state)
        {
            // 1. Determine Location
            Transform respawnPoint = null;
            if (shipToRespawn.FactionID == Faction.TeamA)
            {
                respawnPoint = TeamARespawnPoint;
            }
            else if (shipToRespawn.FactionID == Faction.TeamB)
            {
                respawnPoint = TeamBRespawnPoint;
            }

            if (respawnPoint == null)
            {
                Debug.LogWarning($"No respawn point defined for Faction {shipToRespawn.FactionID}. Using FactionManager's position as fallback.", this);
                respawnPoint = transform; // Fallback
            }

            // 2. Calculate Position
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * RespawnRadius;
            randomOffset.y = 0f; // Force the offset to be only in the XZ plane
            Vector3 newPosition = respawnPoint.position + randomOffset;

            // 3. Set Transform (while inactive)
            state.ShipGameObject.transform.SetPositionAndRotation(newPosition, Quaternion.identity);

            // 4. Restore Ship Health
            if (state.HealthComp != null)
            {
                state.HealthComp.Initialize();
            }

            // 5. Reset Physics
            if (state.Controller != null)
            {
                // Ensure physics state is cleared before reactivation.
                state.Controller.ResetAndClearPhysicsState();
            }

            // 5.5: Reset AI Initialization State (Using Interface)
            IRespawnResettable[] resettables = state.ShipGameObject.GetComponents<IRespawnResettable>();
            foreach (var resettable in resettables)
            {
                resettable.PrepareForRespawn();
            }

            // 6. Reactivate
            state.ShipGameObject.SetActive(true);

            // 7. Update State
            state.Status = ShipStatus.Active;
            AddToActiveRosters(shipToRespawn);

            // 8. Camera Re-Targeting (if player)
            if (shipToRespawn.Role == RoleType.Player)
            {
                OnPlayerShipRespawned?.Invoke(state.ShipGameObject.transform);
            }
        }

        // --- Roster Management Helpers ---
        // (AddToActiveRosters, RemoveFromActiveRosters remain unchanged)

        // (Implementation details for unchanged methods omitted for brevity)
        private void AddToActiveRosters(StarshipIdentity ship)
        {
            Faction faction = ship.FactionID;

            if (!_allShipsRegistry.ContainsKey(faction))
                return; // Safety check

            if (!_allShipsRegistry[faction].Contains(ship))
            {
                _allShipsRegistry[faction].Add(ship);

                if (ship.IsLeader())
                {
                    _leadersRegistry[faction].Add(ship);
                }
                else
                {
                    _followersRegistry[faction].Add(ship);
                }
            }
            UpdateDebugInspectorLists();
        }

        private void RemoveFromActiveRosters(StarshipIdentity ship)
        {
            Faction faction = ship.FactionID;

            if (!_allShipsRegistry.ContainsKey(faction))
                return; // Safety check

            _allShipsRegistry[faction].Remove(ship);

            if (ship.IsLeader())
            {
                _leadersRegistry[faction].Remove(ship);
            }
            else
            {
                _followersRegistry[faction].Remove(ship);
            }

            if (ship == _cachedPlayer)
            {
                _cachedPlayer = null;
            }

            UpdateDebugInspectorLists();
        }

        // --- Intelligence Officer Queries (Public API for AI/Gameplay) ---
        // (GetHostiles, GetFriendlyLeaders, GetHomeBases, GetHostileFaction, GetTeamWaypoints, UpdateDebugInspectorLists, GetPlayerIdentity remain unchanged)

        // (Implementation details omitted for brevity)
        public List<StarshipIdentity> GetHostiles(Faction requesterFaction)
        {
            Faction hostileFaction = GetHostileFaction(requesterFaction);
            return _allShipsRegistry.TryGetValue(hostileFaction, out var roster) ? roster : EmptyRoster;
        }

        public List<StarshipIdentity> GetFriendlyLeaders(Faction requesterFaction)
        {
            return _leadersRegistry.TryGetValue(requesterFaction, out var leaders) ? leaders : EmptyRoster;
        }

        public List<Transform> GetHomeBases(Faction faction)
        {
            if (faction == Faction.TeamA)
                return TeamABases;
            if (faction == Faction.TeamB)
                return TeamBBases;
            return EmptyBases;
        }

        public Faction GetHostileFaction(Faction friendlyFaction)
        {
            return (friendlyFaction == Faction.TeamA) ? Faction.TeamB : Faction.TeamA;
        }

        public List<Transform> GetTeamWaypoints(Faction faction)
        {
            if (faction == Faction.TeamA)
                return TeamAWaypoints;
            if (faction == Faction.TeamB)
                return TeamBWaypoints;
            return EmptyWaypoints;
        }

        private void UpdateDebugInspectorLists()
        {
            // Ensure initialization has occurred
            if (_leadersRegistry == null || _followersRegistry == null)
                return;

            // Creates copies to avoid inspector interference with the actual registries.
            if (_leadersRegistry.ContainsKey(Faction.TeamA))
                _teamALeadersView = new List<StarshipIdentity>(_leadersRegistry[Faction.TeamA]);
            if (_followersRegistry.ContainsKey(Faction.TeamA))
                _teamAFollowersView = new List<StarshipIdentity>(_followersRegistry[Faction.TeamA]);

            if (_leadersRegistry.ContainsKey(Faction.TeamB))
                _teamBLeadersView = new List<StarshipIdentity>(_leadersRegistry[Faction.TeamB]);
            if (_followersRegistry.ContainsKey(Faction.TeamB))
                _teamBFollowersView = new List<StarshipIdentity>(_followersRegistry[Faction.TeamB]);
        }

        public StarshipIdentity GetPlayerIdentity()
        {
            return _cachedPlayer;
        }

        // NEW METHOD: Allow runtime updates to optimization settings via Inspector
        void OnValidate()
        {
            if (Application.isPlaying && Instance == this)
            {
                InitializeDistanceOptimization();

                // Handle enabling/disabling the system dynamically
                if (enableDistanceBasedActivation)
                {
                    StartDistanceChecks();
                }
                else
                {
                    StopDistanceChecks();
                }
            }
        }
    }
}