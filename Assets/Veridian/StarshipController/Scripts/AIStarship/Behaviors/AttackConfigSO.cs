using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// AI Behavior: Attack. Manages combat maneuvers against a designated target.
    /// Implements a state machine (Engaging, Dogfighting, Evading) to handle different tactical situations.
    /// Relies on the AiBrain for target selection (AttackTarget) and the AttackConfigSO for parameters.
    /// </summary>
    public class BehaviorAttack : SimpleAiBehaviorBase
    {
        private readonly AttackConfigSO _config;
        private Transform _currentTarget;

        // Maneuver Management
        private CombatState _currentState;
        private CombatManeuver _activeManeuver;
        private Dictionary<CombatState, CombatManeuver> _maneuvers;

        private float _evasionCheckTimer;

        /// <summary>
        /// Initializes the BehaviorAttack with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration parameters for the attack behavior.</param>
        public BehaviorAttack(AttackConfigSO config)
        {
            _config = config;
        }

        public override string GetName() => $"Attack ({_currentState})";

        public override void Initialize(SimpleAiPilot pilot)
        {
            base.Initialize(pilot);

            // Initialize Maneuver instances
            _maneuvers = new Dictionary<CombatState, CombatManeuver>
           {
               { CombatState.Engaging, new ManeuverEngage() },
               { CombatState.Dogfighting, new ManeuverDogfight() },
               { CombatState.Evading, new ManeuverEvade() }
           };

            // Determine initial target (read from Brain)
            _currentTarget = pilot.Brain != null ? pilot.Brain.AttackTarget : null;

            // Initialize all maneuvers with the current context
            InitializeManeuvers(pilot);

            // Determine initial combat state
            UpdateCombatState(pilot);
        }

        /// <summary>
        /// Initializes all maneuver instances with the current context (pilot, target, config).
        /// </summary>
        private void InitializeManeuvers(SimpleAiPilot pilot)
        {
            if (_maneuvers == null) return;

            foreach (var maneuver in _maneuvers.Values)
            {
                // Pass the required context to the maneuver.
                // It reads parameters directly from the centralized _config.
                maneuver.Initialize(pilot, _currentTarget, _config, _config.SlowDownRadius);
            }
        }

        public override NavigationGoal UpdateGoal(SimpleAiPilot pilot)
        {
            // 1. Update Target reference (Read exclusively from Brain)
            Transform newTarget = pilot.Brain != null ? pilot.Brain.AttackTarget : null;

            if (newTarget == null)
            {
                _currentTarget = null;

                // If the target is lost, stop the active maneuver and return an Idle goal.
                if (_activeManeuver != null)
                {
                    _activeManeuver.OnExit();
                    _activeManeuver = null;
                }

                return NavigationGoal.Idle(pilot.Transform.position);
            }

            // If the target changed, update the maneuver context
            if (newTarget != _currentTarget)
            {
                _currentTarget = newTarget;
                // Re-initialization ensures maneuvers update their internal state for the new target.
                InitializeManeuvers(pilot);
            }


            // 2. Update State and Maneuver Selection
            UpdateCombatState(pilot);

            // 3. Execute Active Maneuver
            if (_activeManeuver != null)
            {
                return _activeManeuver.Execute();
            }

            // Fallback if no maneuver is active.
            return NavigationGoal.Idle(pilot.Transform.position);
        }

        /// <summary>
        /// Updates the combat state machine based on distance to the target and evasion checks.
        /// </summary>
        private void UpdateCombatState(SimpleAiPilot pilot)
        {
            if (_currentTarget == null) return;

            // Read parameters from the configuration.
            float distance = Vector3.Distance(pilot.Transform.position, _currentTarget.position);
            float preferredRange = _config.PreferredEngagementRange;
            float tolerance = _config.EngagementRangeTolerance;

            CombatState newState = _currentState;

            // State transition logic
            switch (_currentState)
            {
                case CombatState.Evading:
                    // Evasion runs until the maneuver reports completion.
                    if (_activeManeuver != null)
                    {
                        // ManeuverEvade signals completion by returning a goal with near-zero speed.
                        NavigationGoal currentGoal = _activeManeuver.Execute();
                        if (currentGoal.DesiredSpeed.HasValue && currentGoal.DesiredSpeed.Value < 0.1f)
                        {
                            // After evasion, decide next move based on distance
                            newState = (distance > preferredRange + tolerance) ? CombatState.Engaging : CombatState.Dogfighting;
                        }
                    }
                    else
                    {
                        // Fallback if maneuver failed to initialize.
                        newState = CombatState.Engaging;
                    }
                    break;

                case CombatState.Engaging:
                    // Transition to Dogfighting if within the engagement range buffer.
                    if (distance <= preferredRange + tolerance)
                    {
                        newState = CombatState.Dogfighting;
                    }
                    CheckForEvasion(pilot, ref newState);
                    break;

                case CombatState.Dogfighting:
                    // Transition back to Engaging if the target escapes the engagement range buffer.
                    if (distance > preferredRange + tolerance)
                    {
                        newState = CombatState.Engaging;
                    }
                    CheckForEvasion(pilot, ref newState);
                    break;

                default:
                    // Handle initial state or fallback
                    if (_currentTarget != null)
                    {
                        newState = (distance > preferredRange + tolerance) ? CombatState.Engaging : CombatState.Dogfighting;
                    }
                    break;
            }

            TransitionToState(newState);
        }

        /// <summary>
        /// Periodically checks if an evasive maneuver should be initiated based on the Evasiveness parameter and danger state.
        /// </summary>
        private void CheckForEvasion(SimpleAiPilot pilot, ref CombatState currentState)
        {
            _evasionCheckTimer -= Time.deltaTime;
            if (_evasionCheckTimer <= 0)
            {
                _evasionCheckTimer = 1.0f; // Check approximately every second.

                bool isInDanger = (pilot.Brain != null) && pilot.Brain.IsInDanger;

                // Calculate evasion chance. Increase probability (by 50%) if in danger.
                float evasionChance = _config.Evasiveness * (isInDanger ? 1.5f : 1.0f);

                if (UnityEngine.Random.value < evasionChance)
                {
                    currentState = CombatState.Evading;
                }
            }
        }

        /// <summary>
        /// Handles the transition between combat states and activates the corresponding maneuver.
        /// </summary>
        private void TransitionToState(CombatState newState)
        {
            if (newState == _currentState && _activeManeuver != null) return;

            _activeManeuver?.OnExit();

            _currentState = newState;

            if (_maneuvers != null && _maneuvers.TryGetValue(_currentState, out CombatManeuver newManeuver))
            {
                _activeManeuver = newManeuver;
                _activeManeuver.OnEnter();
            }
            else
            {
                _activeManeuver = null;
            }
        }

        public override Transform GetCurrentTargetObject() => _currentTarget;
    }


    /// <summary>
    /// Configuration ScriptableObject for the Attack behavior.
    /// Consolidates combat maneuvering parameters and weapon control parameters specific to the Attack behavior execution.
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Attack", menuName = "Starship AI/Behavior Config/Attack")]
    public class AttackConfigSO : BehaviorConfigSO
    {
        // Note: The parameters below mirror those found in CombatPersonalitySO.
        // When BehaviorAttack is active, these values define the combat style.
        // If the AiWeaponController is also active, it should ideally source its parameters from this configuration during the attack behavior,
        // or this configuration should match the CombatPersonalitySO assigned to the AiWeaponController.
        // Based on the provided architecture, these fields are used directly by BehaviorAttack and its maneuvers.

        [Header("Accuracy and Targeting (Used by Weapon Systems)")]
        [Tooltip("The maximum angle (degrees) off the calculated aim point the AI is allowed to fire. Defines the required precision for a firing solution.")]
        [Range(1f, 45f)]
        public float FiringConeAngle = 5.0f;

        [Tooltip("Scale of simulated aiming error (meters of offset per 100m distance). Higher values mean less accurate fire.")]
        [Range(0f, 10f)]
        public float ErrorScale = 2.0f;

        [Tooltip("Multiplier applied to the weapon's base max range. >1.0 means engaging from further away.")]
        [Range(0.5f, 1.5f)]
        public float RangeMultiplier = 1.0f;

        [Header("Primary Weapon (Burst Control)")]
        [Tooltip("The minimum duration (in seconds) the AI will continuously fire primary weapons once a burst starts.")]
        public float MinBurstDuration = 0.5f;
        [Tooltip("The maximum duration (in seconds) the AI will continuously fire primary weapons during a single burst.")]
        public float MaxBurstDuration = 2.0f;

        [Tooltip("The minimum time (in seconds) the AI will pause firing between bursts (cooldown).")]
        public float MinBurstCooldown = 0.2f;
        [Tooltip("The maximum time (in seconds) the AI will pause firing between bursts (cooldown).")]
        public float MaxBurstCooldown = 1.5f;

        [Header("Secondary Weapon Control")]
        [Tooltip("The probability (0.0-1.0) per second that the AI will fire a secondary weapon when available and conditions are met.")]
        [Range(0f, 1f)]
        public float SecondaryFireRate = .2f;

        [Tooltip("The minimum time (in seconds) the AI must be continuously engaged with a target before it considers using secondary weapons.")]
        public float SecondaryWarmupDelay = 3.0f;

        [Header("Maneuvering Style (Used by BehaviorAttack)")]
        [Tooltip("How frequently the AI attempts evasive maneuvers (0.0-1.0). Higher values lead to more frequent evasion checks, especially when under fire (IsInDanger).")]
        [Range(0f, 1f)]
        public float Evasiveness = 0.3f;

        [Tooltip("The ideal combat distance (in meters) the AI will try to maintain during the 'Dogfighting' state.")]
        public float PreferredEngagementRange = 300f;

        [Tooltip("The tolerance (in meters) around the PreferredEngagementRange. The AI switches maneuvers when outside this buffer zone.")]
        public float EngagementRangeTolerance = 50f;

        public override ISimpleAiBehavior CreateBehavior(AiBrain brain)
        {
            // Pass this configuration instance to the BehaviorAttack constructor.
            return new BehaviorAttack(this);
        }
    }
}