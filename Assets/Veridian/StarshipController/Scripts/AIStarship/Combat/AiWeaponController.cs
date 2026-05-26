using UnityEngine;
using Veridian.Starship.Core;
using Veridian.Starship.Weapons;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// Manages the aiming and firing logic for AI pilots. This component runs independently of the flight maneuvers managed by SimpleAiPilot.
    /// It determines the precise aim point, evaluates firing solutions, and controls weapon bursts based on the CombatPersonalitySO.
    /// </summary>
    [RequireComponent(typeof(ShipWeaponController))]
    [RequireComponent(typeof(AiBrain))]
    public class AiWeaponController : MonoBehaviour, IRespawnResettable // Implemented IRespawnResettable
    {
        [Header("Configuration")]
        [Tooltip("Defines the AI's combat style, including accuracy, aggression, burst timing, and preferred engagement ranges.")]
        public CombatPersonalitySO CombatPersonality;

        // Fields used only for displaying information in the Inspector.
        [Header("Debug Info (Read-Only)")]
        [SerializeField, Tooltip("The current target being tracked by the weapon system.")]
        private Transform _currentTargetForInspector;

        [SerializeField, Tooltip("The calculated world-space position the AI is currently aiming at (including interception prediction and error).")]
        private Vector3 _aimPositionForInspector;

        [SerializeField, Tooltip("Is the target currently within the defined firing cone and range?")]
        private bool _hasFiringSolutionForInspector;

        [SerializeField, Tooltip("The duration (seconds) the AI has been continuously engaged with the current target.")]
        private float _engagementTimerForInspector;


        [Tooltip("If set, this transform's 'forward' will be used for firing cone checks instead of the root transform. Essential for turrets.")]
        public Transform AimingTransformOverride;
        // Public accessors for the SimpleAiPilot to read (updated every frame)
        /// <summary>
        /// The calculated world-space position the AI is aiming at.
        /// </summary>
        public Vector3 AimPosition { get; private set; }

        /// <summary>
        /// Should the primary weapons fire this frame?
        /// </summary>
        public bool FirePrimary { get; private set; }

        /// <summary>
        /// Should the secondary weapons fire this frame?
        /// </summary>
        public bool FireSecondary { get; private set; }

        // Internal references
        private ShipWeaponController _weapons;
        private AiBrain _brain;
        private Transform _shipTransform;
        private Rigidbody _shipRigidbody;

        // State (real-time)
        private float _burstTimer;
        private bool _isBursting;
        private float _currentEngagementTimer;

        // NEW: Initialization State Flag
        private bool _hasBeenInitialized = false;

        void Awake()
        {
            _weapons = GetComponent<ShipWeaponController>();
            _brain = GetComponent<AiBrain>();
            _shipTransform = transform;
            // Assuming Unity 2025.1 (Unity 6) compatibility
            _shipRigidbody = GetComponent<Rigidbody>();

            if (CombatPersonality == null)
            {
                Debug.LogWarning($"AiWeaponController on {gameObject.name} is missing a CombatPersonalitySO. Disabling.", this);
                enabled = false;
            }
        }

        // MODIFIED METHOD
        void OnEnable()
        {
            // Check the initialization flag.
            // If this is the first time or a respawn, perform a full reset.
            if (!_hasBeenInitialized)
            {
                ResetCombatState();
            }
            // If this is a distance-based reactivation, skip the reset, preserving engagement timers and burst state.
        }

        // NEW METHOD
        void Start()
        {
            // Mark as initialized after the first full setup.
            _hasBeenInitialized = true;
        }

        // NEW METHOD (IRespawnResettable implementation)
        public void PrepareForRespawn()
        {
            // Reset the flag so the next OnEnable triggers a full ResetCombatState.
            _hasBeenInitialized = false;
        }

        /// <summary>
        /// Resets the internal combat engagement state, including timers, firing flags, and aim position.
        /// </summary>
        private void ResetCombatState()
        {
            // (Unchanged)
            // Clear engagement timers
            _currentEngagementTimer = 0f;

            // Clear burst firing state
            _isBursting = false;
            _burstTimer = 0f;

            // Ensure outputs are reset
            FirePrimary = false;
            FireSecondary = false;

            // Reset aim position to a safe default (forward)
            // Ensure _shipTransform is initialized before use.
            if (_shipTransform == null)
            {
                _shipTransform = transform;
            }

            // Check if _shipTransform is still valid before accessing position/forward (e.g. if destroyed/disabled during initialization)
            if (_shipTransform != null)
            {
                AimPosition = _shipTransform.position + _shipTransform.forward * 1000f;
            }

            // Ensure the underlying weapon controller clears any locked target (redundant safety, as ShipWeaponController also resets on enable)
            if (_weapons != null)
            {
                _weapons.ClearTarget();
            }
        }

        // (The rest of the AiWeaponController class remains unchanged, implementation omitted for brevity)
        void Update()
        {
            // Ensure CombatPersonality is available before running logic
            if (CombatPersonality == null)
                return;

            // --- REAL-TIME LOGIC (Runs Every Frame) ---

            // 1. Reset outputs and identify target
            FirePrimary = false;
            FireSecondary = false;
            Transform currentTarget = DetermineTarget();

            // 2. Handle target state
            if (currentTarget == null)
            {
                HandleTargetLoss();
            }
            else
            {
                HandleTargetEngagement(currentTarget);
            }

            // Update Inspector debug fields (runs every frame for immediate feedback)
            UpdateDebugInspectorFields(currentTarget);
        }

        /// <summary>
        /// Handles the state when no valid target is available.
        /// </summary>
        private void HandleTargetLoss()
        {
            _weapons.ClearTarget();
            // Default aim position when idle.
            AimPosition = _shipTransform.position + _shipTransform.forward * 1000f;
            // Reset engagement timer when target is lost
            _currentEngagementTimer = 0f;
            // Ensure bursting stops if the target is lost
            if (_isBursting)
            {
                EndBurst();
            }
        }

        /// <summary>
        /// Handles the logic when engaged with a valid target.
        /// </summary>
        private void HandleTargetEngagement(Transform currentTarget)
        {
            _currentEngagementTimer += Time.deltaTime;
            _weapons.SetTarget(currentTarget.gameObject);

            // 3. Calculate aim and evaluate firing solution
            AimPosition = CalculateAimPoint(currentTarget);
            bool hasFiringSolution = EvaluateFiringSolution(AimPosition, currentTarget);

            // 4. Execute Firing Logic
            HandlePrimaryFire(hasFiringSolution);
            HandleSecondaryFire(hasFiringSolution);
        }

        /// <summary>
        /// Updates the serialized fields used for debugging in the Inspector.
        /// </summary>
        private void UpdateDebugInspectorFields(Transform currentTarget)
        {
            _currentTargetForInspector = currentTarget;
            _aimPositionForInspector = AimPosition;
            // Re-evaluate firing solution for debug display to ensure it reflects the current frame's aim position.
            _hasFiringSolutionForInspector = (currentTarget != null) && EvaluateFiringSolution(AimPosition, currentTarget);
            _engagementTimerForInspector = _currentEngagementTimer;
        }

        /// <summary>
        /// Determines the current valid attack target by reading from the AiBrain and performing validation checks.
        /// </summary>
        private Transform DetermineTarget()
        {
            // Check if the brain reference is valid before accessing it.
            if (_brain == null)
                return null;

            Transform target = _brain.AttackTarget;

            // Check if the target reference is null or inactive.
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return null;
            }

            // Check if the target is alive (if it has an identity).
            var identity = target.GetComponent<StarshipIdentity>();
            if (identity != null && !identity.IsAlive)
            {
                return null;
            }

            return target;
        }

        /// <summary>
        /// Calculates the precise interception point for the target, factoring in projectile speed and target velocity, then applies aiming error.
        /// </summary>
        private Vector3 CalculateAimPoint(Transform target)
        {
            // This calculation is complex and must run every frame for accuracy.
            if (_weapons.primaryWeaponStats == null)
                return target.position;

            float projectileSpeed = _weapons.primaryWeaponStats.projectileSpeed;
            Vector3 targetVelocity = Vector3.zero;

            // Get target velocity if it has a Rigidbody.
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null && !targetRb.isKinematic)
            {
                // Assuming Unity 6 compatibility
                targetVelocity = targetRb.linearVelocity;
            }

            // Get shooter velocity.
            Vector3 shooterVelocity = (_shipRigidbody != null && !_shipRigidbody.isKinematic) ? _shipRigidbody.linearVelocity : Vector3.zero;

            // Calculate the first-order interception point using the utility class.
            Vector3 interceptionPoint = InterceptionCalculator.CalculateInterceptionPoint(_shipTransform.position, shooterVelocity, target.position, targetVelocity, projectileSpeed);

            // Apply simulated aiming error based on the combat personality.
            return ApplyAimError(interceptionPoint, target);
        }

        /// <summary>
        /// Manages the primary weapon firing logic, implementing burst fire patterns.
        /// </summary>
        /// <param name="hasFiringSolution">Is the target currently viable for firing?</param>
        private void HandlePrimaryFire(bool hasFiringSolution)
        {
            // If the firing solution is lost, stop the current burst.
            if (!hasFiringSolution)
            {
                if (_isBursting)
                {
                    EndBurst();
                }
                // Continue cooldown countdown even without a solution.
                if (_burstTimer > 0 && !_isBursting)
                {
                    _burstTimer -= Time.deltaTime;
                }
                return;
            }

            // If currently bursting
            if (_isBursting)
            {
                _burstTimer -= Time.deltaTime;
                if (_burstTimer <= 0)
                {
                    // Burst duration ended, start cooldown.
                    EndBurst();
                }
                else
                {
                    // Continue firing during the burst.
                    FirePrimary = true;
                }
            }
            // If not currently bursting (Cooldown phase)
            else
            {
                _burstTimer -= Time.deltaTime;
                if (_burstTimer <= 0)
                {
                    // Cooldown ended, start a new burst.
                    StartBurst();
                    FirePrimary = true;
                }
            }
        }

        private void StartBurst()
        {
            _isBursting = true;
            _burstTimer = UnityEngine.Random.Range(CombatPersonality.MinBurstDuration, CombatPersonality.MaxBurstDuration);
        }

        private void EndBurst()
        {
            _isBursting = false;
            _burstTimer = UnityEngine.Random.Range(CombatPersonality.MinBurstCooldown, CombatPersonality.MaxBurstCooldown);
        }

        /// <summary>
        /// Manages the secondary weapon firing logic based on probability, engagement time, and weapon type (guided vs. unguided).
        /// </summary>
        /// <param name="hasFiringSolution">Is the target currently viable for firing (relevant for unguided weapons)?</param>
        private void HandleSecondaryFire(bool hasFiringSolution)
        {
            if (_weapons.secondaryWeaponStats == null)
                return;

            // Check ammunition constraints.
            if (_weapons.CurrentSecondaryAmmo <= 0 && _weapons.secondaryWeaponStats.maxAmmo > 0)
                return;

            // Check engagement warmup delay.
            if (_currentEngagementTimer < CombatPersonality.SecondaryWarmupDelay)
                return;

            // Determine if firing is viable. Guided weapons rely on the WeaponController's internal lock checks (handled when FireSecondary is called by the pilot),
            // while unguided weapons rely on the provided firing solution here.
            bool canFire = _weapons.secondaryWeaponStats.isGuided || hasFiringSolution;

            if (canFire)
            {
                // Fire based on probability (SecondaryFireRate defines probability per second).
                if (UnityEngine.Random.value < CombatPersonality.SecondaryFireRate * Time.deltaTime)
                {
                    FireSecondary = true;
                }
            }
        }

        #region Helper Methods
        /// <summary>
        /// Applies a randomized offset to the precise aim point to simulate pilot error, based on distance and personality settings.
        /// </summary>
        private Vector3 ApplyAimError(Vector3 preciseAimPoint, Transform target)
        {
            float distance = Vector3.Distance(_shipTransform.position, preciseAimPoint);
            // Calculate error radius: ErrorScale meters of offset per 100m distance.
            float errorRadius = (distance / 100f) * CombatPersonality.ErrorScale;

            // Clamp the maximum error radius to prevent excessive misses at extreme ranges.
            float maxError = 50f; // Default max error

            // Use the target's collider bounds to determine a reasonable max error radius.
            if (target.TryGetComponent<Collider>(out var targetCollider))
            {
                // Max error is 1.5 times the target's extents (size).
                maxError = targetCollider.bounds.extents.magnitude * 1.5f;
            }
            errorRadius = Mathf.Min(errorRadius, maxError);

            if (errorRadius < 0.1f)
            {
                return preciseAimPoint;
            }

            // Apply the random offset within the calculated error radius.
            Vector3 offset = UnityEngine.Random.insideUnitSphere * errorRadius;
            return preciseAimPoint + offset;
        }

        /// <summary>
        /// Evaluates if the current aim position provides a valid firing solution based on range and firing cone constraints.
        /// </summary>
        private bool EvaluateFiringSolution(Vector3 aimPosition, Transform target)
        {
            if (_weapons.primaryWeaponStats == null)
                return false;

            Vector3 directionToAim = (aimPosition - _shipTransform.position).normalized;
            // Distance check uses the target's actual position, not the aim position.
            float distance = Vector3.Distance(_shipTransform.position, target.position);

            // Check Range
            if (distance > _weapons.primaryWeaponStats.maxRange * CombatPersonality.RangeMultiplier)
            {
                return false;
            }

            // Check Firing Cone Angle against the ship's forward vector.
            Transform aimReference = (AimingTransformOverride != null) ? AimingTransformOverride : _shipTransform;
            float angle = Vector3.Angle(aimReference.forward, directionToAim);
            if (angle > CombatPersonality.FiringConeAngle)
            {
                return false;
            }

            return true;
        }
        #endregion
    }
}