using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

// using Starship.Flight; // REMOVED

namespace Veridian.Starship.AI
{
    // NEW: Helper struct for mapping AiMode to BehaviorConfigSO in the Inspector.
    [System.Serializable]
    public struct BehaviorMapping
    {
        public AiMode Mode;
        public BehaviorConfigSO Config;
    }

    /// <summary>
    /// The Strategic Commander. Makes high-level decisions and executes behaviors via the AiPilot.
    /// </summary>

    [RequireComponent(typeof(StarshipIdentity))]
    public class AiBrain : MonoBehaviour, IRespawnResettable // Implemented IRespawnResettable
    {
        [Header("Configuration")]
        [Tooltip("The prioritized list of actions defining the AI's personality. Evaluated top-down.")]
        public List<AiActionSO> Actions = new List<AiActionSO>();

        // NEW FIELD: Replaces configuration fields from the old controller.
        [Tooltip("Defines the configuration asset (parameters and factory) for each AI Behavior Mode.")]
        public List<BehaviorMapping> BehaviorConfigurations = new List<BehaviorMapping>();

        [Tooltip("How often (in seconds) the brain re-evaluates the situation.")]
        public float ThinkCycleInterval = 2.0f;

        [Header("Dynamic State (Debug Info)")]
        [SerializeField]
        private AiActionSO _currentAction; // Preserved SerializeField

        [SerializeField]
        private bool _isInDanger;

        [SerializeField]
        private AiMode _activeBehaviorMode = AiMode.Idle; // Debug field

        [SerializeField]
        private Transform _debugMoveTarget;

        [SerializeField]
        private Transform _debugAttackTarget;

        // Public Accessors for Targets (Preserved public access)
        public Transform MoveTarget { get; private set; }
        public Transform AttackTarget { get; private set; }

        // Public Accessors for State and Components
        public bool IsInDanger => _isInDanger;
        public StarshipIdentity Identity { get; private set; }
        public IHealthProvider HealthProvider { get; private set; }

        // Expose SensorySystem for conditions and behaviors.
        public ShipSensorySystem SensorySystem { get; private set; }

        // Command System
        public enum AiCommand
        {
            None,
            RequestTakeoff,
            RequestLanding
        }

        public AiCommand PendingCommand { get; private set; } = AiCommand.None;

        // Internal References
        // private SimpleAiController _simpleAiController; // REMOVED
        private SimpleAiPilot _simpleAiPilot;
        private float _thinkTimer;

        // NEW: Runtime dictionary for fast lookup of configurations.
        private Dictionary<AiMode, BehaviorConfigSO> _configMap = new Dictionary<AiMode, BehaviorConfigSO>();

        // State Management for Actions (Timers)
        private class ActionState
        {
            public float ActiveTimeRemaining;
            public float CooldownTimeRemaining;
        }

        private Dictionary<AiActionSO, ActionState> _actionStates = new Dictionary<AiActionSO, ActionState>();

        // NEW: Initialization State Flag
        private bool _hasBeenInitialized = false;

        #region Initialization and Reset

        void Awake()
        {
            _simpleAiPilot = GetComponent<SimpleAiPilot>();
            Identity = GetComponent<StarshipIdentity>();
            HealthProvider = GetComponent<IHealthProvider>();
            SensorySystem = GetComponent<ShipSensorySystem>();

            if (_simpleAiPilot != null)
            {
                // Establish the link so behaviors can access the brain's targets.
                _simpleAiPilot.SetBrain(this);
            }

            InitializeActionStates();
            InitializeConfigMap(); // NEW
        }

        // NEW METHOD
        private void InitializeConfigMap()
        {
            // (Unchanged)
            _configMap.Clear();
            foreach (var mapping in BehaviorConfigurations)
            {
                if (mapping.Config != null)
                {
                    if (!_configMap.ContainsKey(mapping.Mode))
                    {
                        _configMap.Add(mapping.Mode, mapping.Config);
                    }
                    else
                    {
                        Debug.LogWarning($"Duplicate configuration found for AiMode {mapping.Mode} on {gameObject.name}. Ignoring subsequent entries.", this);
                    }
                }
            }
        }

        // MODIFIED METHOD
        void OnEnable()
        {
            // Check the initialization flag.
            // If this is the first time (Awake/Start) or a respawn (where PrepareForRespawn set it to false), perform a full reset.
            if (!_hasBeenInitialized)
            {
                ResetBrainState();
            }
            // If this is a distance-based reactivation, _hasBeenInitialized is true, so we skip the reset, preserving the tactical state (timers, actions, targets).
        }

        // MODIFIED METHOD
        void Start()
        {
            // Mark as initialized after the first full setup (which happens via OnEnable->ResetBrainState).
            _hasBeenInitialized = true;
        }

        // NEW METHOD (IRespawnResettable implementation)
        public void PrepareForRespawn()
        {
            // Reset the flag so the next OnEnable triggers a full ResetBrainState.
            _hasBeenInitialized = false;
        }

        private void ResetBrainState()
        {
            // (Unchanged)
            InitializeActionStates();

            // Clear current action and ensure exit logic is handled
            TransitionToIdle();

            // Clear Targets (TransitionToIdle also does this)
            MoveTarget = null;
            AttackTarget = null;

            // Reset Environmental State
            _isInDanger = false;

            // Reset Action Timers
            foreach (var state in _actionStates.Values)
            {
                state.ActiveTimeRemaining = 0;
                state.CooldownTimeRemaining = 0;
            }

            // Clear Commands
            ClearCommand();

            // Initialize the think timer with a random offset.
            _thinkTimer = UnityEngine.Random.Range(0.1f, ThinkCycleInterval);
        }

        private void InitializeActionStates()
        {
            // (Unchanged)
            foreach (var action in Actions)
            {
                if (action != null && !_actionStates.ContainsKey(action))
                {
                    _actionStates[action] = new ActionState();
                }
            }
        }

        #endregion

        // (The rest of the AiBrain class remains unchanged, implementation omitted for brevity)
        #region Command API
        public void IssueCommand(AiCommand command)
        {
            PendingCommand = command;
            _thinkTimer = 0f;
        }

        public void ClearCommand()
        {
            PendingCommand = AiCommand.None;
        }
        #endregion

        #region Update Loop and Think Cycle

        void Update()
        {
            // Do not run the AI if the ship is dead.
            if (Identity == null || !Identity.IsAlive)
            {
                if (_currentAction != null)
                {
                    TransitionToIdle();
                }
                return;
            }

            UpdateTimers(Time.deltaTime);

            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer <= 0f)
            {
                Think();
                _thinkTimer = ThinkCycleInterval;
            }
        }

        private void UpdateTimers(float deltaTime)
        {
            // Update cooldowns
            foreach (var state in _actionStates.Values)
            {
                if (state.CooldownTimeRemaining > 0)
                {
                    state.CooldownTimeRemaining -= deltaTime;
                }
            }

            // Update active timer for the current action
            if (_currentAction != null && _actionStates.TryGetValue(_currentAction, out ActionState currentState))
            {
                if (currentState.ActiveTimeRemaining > 0)
                {
                    currentState.ActiveTimeRemaining -= deltaTime;
                }
            }
        }

        /// <summary>
        /// The main decision-making loop.
        /// </summary>
        private void Think()
        {
            // 1. Sense
            SenseEnvironment();

            // 2. Decide (The Two-Phase Check)
            AiActionSO preliminaryWinner = null;
            if (_currentAction != null)
            {
                if (ShouldRemainInCurrentAction())
                {
                    preliminaryWinner = _currentAction;
                }
            }
            AiActionSO finalWinner = FindBestActionToTransitionTo(preliminaryWinner);

            // 3. Act
            if (finalWinner != _currentAction)
            {
                // If we are changing actions, transition fully.
                // TransitionToNewAction handles the strict target clearing.
                TransitionToNewAction(finalWinner);
            }
            // If we are remaining in the SAME action, we still need to update our target.
            else if (_currentAction != null)
            {
                // When remaining in the same action, targets persist across think cycles.
                // The action's ExecuteTargeting logic handles whether to keep the target or re-acquire.
                _currentAction.ExecuteTargeting(this);
            }
        }

        #endregion

        #region Sensing

        private void SenseEnvironment()
        {
            if (Identity == null)
                return;

            // Query the SAS for the danger state (Assumes SituationalAwarenessSystem exists statically).
            QueryRequest dangerRequest = new QueryRequest(Identity, QueryType.CheckDanger, _isInDanger);
            QueryResponse dangerResponse = SituationalAwarenessSystem.ProcessQuery(dangerRequest);
            _isInDanger = dangerResponse.Status;
        }

        #endregion

        #region Decision Logic
        // Decision logic remains unchanged from the provided context.

        private bool ShouldRemainInCurrentAction()
        {
            if (_currentAction == null || !_actionStates.TryGetValue(_currentAction, out ActionState state))
            {
                return false;
            }

            // Check Active Duration Timer
            if (state.ActiveTimeRemaining > 0)
            {
                return true;
            }

            // If timer expired or wasn't set, check conditions and probability.
            if (_currentAction.AreConditionsMet(this))
            {
                // Check Stay Probability
                if (UnityEngine.Random.value <= _currentAction.StayProbability)
                {
                    return true;
                }
            }

            return false;
        }

        private AiActionSO FindBestActionToTransitionTo(AiActionSO preliminaryWinner)
        {
            int searchLimit = Actions.Count;

            if (preliminaryWinner != null)
            {
                int winnerIndex = Actions.IndexOf(preliminaryWinner);
                if (winnerIndex != -1)
                {
                    searchLimit = winnerIndex;
                }
            }

            for (int i = 0; i < searchLimit; i++)
            {
                AiActionSO action = Actions[i];
                if (action == null)
                    continue;

                if (ShouldTransitionToAction(action))
                {
                    return action;
                }
            }

            return preliminaryWinner;
        }

        private bool ShouldTransitionToAction(AiActionSO action)
        {
            if (!_actionStates.TryGetValue(action, out ActionState state))
            {
                state = new ActionState();
                _actionStates[action] = state;
            }

            // Check Cooldown
            if (state.CooldownTimeRemaining > 0)
            {
                return false;
            }

            // Check Conditions
            if (action.AreConditionsMet(this))
            {
                // Check Enter Probability
                if (UnityEngine.Random.value <= action.EnterProbability)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Action Execution

        // REFACTORED METHOD
        private void TransitionToNewAction(AiActionSO newAction)
        {
            // --- REQUIREMENT IMPLEMENTED: Unconditional Target Clearing ---
            // As the first step in the TransitionToNewAction method—before any other logic is run.
            SetMoveTarget(null);
            SetAttackTarget(null);

            if (newAction == null)
            {
                // We pass true to indicate targets are already cleared by this method.
                TransitionToIdle(targetsAlreadyCleared: true);
                return;
            }

            // Handle exiting the previous action
            HandleExitCurrentAction();

            _currentAction = newAction;

            // Handle entering the new action (Timers)
            if (_actionStates.TryGetValue(_currentAction, out ActionState newState))
            {
                // Start active duration timer if applicable
                if (_currentAction.ActiveDuration > 0)
                {
                    newState.ActiveTimeRemaining = _currentAction.ActiveDuration;
                }
            }

            // --- The Command Chain Integration Point (Refactored) ---

            // 1. Targeting (Action defines the target acquisition logic)
            // The slate is clean; the action is 100% responsible for setting targets.
            _currentAction.ExecuteTargeting(this);

            // 2. Determine Behavior Mode
            AiMode behaviorMode = _currentAction.BehaviorToActivate;

            // 3. Instantiate and Switch Behavior (The Factory Pattern Integration)
            ActivateBehavior(behaviorMode);
        }

        // REFACTORED METHOD
        private void TransitionToIdle(bool targetsAlreadyCleared = false)
        {
            // Clear targets unless they were already cleared by the caller (TransitionToNewAction).
            if (!targetsAlreadyCleared)
            {
                SetMoveTarget(null);
                SetAttackTarget(null);
            }

            HandleExitCurrentAction();
            _currentAction = null;

            ActivateBehavior(AiMode.Idle);
        }

        // NEW METHOD: Centralized behavior activation logic using the Factory Pattern.
        private void ActivateBehavior(AiMode mode)
        {
            _activeBehaviorMode = mode; // Update debug field

            if (_simpleAiPilot == null)
                return;

            ISimpleAiBehavior behaviorInstance = null;

            // Look up the configuration associated with the requested mode.
            if (_configMap.TryGetValue(mode, out BehaviorConfigSO config))
            {
                if (config != null)
                {
                    // Use the factory method defined in the ScriptableObject to create the behavior instance.
                    // We pass 'this' (the AiBrain reference).
                    behaviorInstance = config.CreateBehavior(this);
                }
            }

            if (behaviorInstance != null)
            {
                // Command the Pilot to use the new behavior instance.
                _simpleAiPilot.SetBehavior(behaviorInstance);
            }
            else
            {
                // Handle failure case gracefully.
                if (mode != AiMode.Idle)
                {
                    Debug.LogWarning($"Failed to create or find configuration for AiMode.{mode} on {gameObject.name}. Check Behavior Configurations mapping. Defaulting to Idle.", this);
                    ActivateBehavior(AiMode.Idle); // Recursive call to ensure Idle is set
                }
                else
                {
                    // If even Idle fails (e.g., missing IdleConfigSO or its factory fails), we must prevent infinite recursion.
                    // As a last resort, we manually create BehaviorIdle if the ConfigSO failed.
                    Debug.LogWarning($"Failed to activate AiMode.Idle via ConfigSO on {gameObject.name}. Manually instantiating BehaviorIdle.", this);
                    _simpleAiPilot.SetBehavior(new BehaviorIdle());
                }
            }
        }

        private void HandleExitCurrentAction()
        {
            if (_currentAction != null && _actionStates.TryGetValue(_currentAction, out ActionState previousState))
            {
                // Start cooldown if applicable
                if (_currentAction.CooldownDuration > 0)
                {
                    previousState.CooldownTimeRemaining = _currentAction.CooldownDuration;
                }
                // Reset active timer (important if it was interrupted)
                previousState.ActiveTimeRemaining = 0;
            }
        }

        #endregion

        #region Public API (Targeting)

        public void SetMoveTarget(Transform target)
        {
            MoveTarget = target;
            _debugMoveTarget = target; // ADD THIS LINE
        }

        public void SetAttackTarget(Transform target)
        {
            AttackTarget = target;
            _debugAttackTarget = target; // ADD THIS LINE
        }

        #endregion
    }
}