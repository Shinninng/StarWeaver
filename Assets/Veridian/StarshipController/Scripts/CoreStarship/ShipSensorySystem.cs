using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Manages the sensory input for the ship, primarily focusing on environmental awareness (ground altitude) and obstacle detection/avoidance.
    /// It utilizes a swappable ISensorProvider to perform the actual physics queries.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipSensorySystem : MonoBehaviour
    {
        // --- Configuration ---
        [Header("Profile Configuration")]
        [Tooltip("The ScriptableObject containing the sensory configuration settings (ranges, angles, timings).")]
        public SensorySettingsSO settingsSO;
        [Tooltip("If true, settings will be loaded from the assigned Settings SO. If false, the values set directly on this component will be used.")]
        public bool useProfile = true;
        [Header("Simple Altitude Mode")]
        [Tooltip("The world-space Y-coordinate of your 'sea level' or 'ground floor'. Used by the 'SimpleAltitudeOnly' provider.")]
        public float seaLevelY = 0f;
        // --- Provider Configuration ---
        /// <summary>
        /// Defines the implementation used for sensor queries.
        /// </summary>
        public enum SensorProviderType
        {
            /// <summary>
            /// No physics. Only calculates altitude from 'seaLevelY'. Does NOT detect obstacles.
            /// </summary>
            SimpleAltitudeOnly,

            /// <summary>
            /// Uses direct Unity Physics calls (e.g., Physics.Raycast). Lightweight and suitable for low entity counts.
            /// </summary>
            Simple,

            /// <summary>
            /// (Placeholder) Uses an advanced system potentially leveraging batched jobs or optimized spatial partitioning.
            /// </summary>
            Advanced
        }

        [Header("Sensor Provider")]
        [Tooltip("Select the sensor implementation. Simple uses direct physics calls. Advanced is a placeholder for optimized, batched systems.")]
        public SensorProviderType providerType = SensorProviderType.SimpleAltitudeOnly;

        private ISensorProvider _sensorProvider;
        private SensorProviderType _cachedProviderType;

        [Header("Query Frequencies (Performance Tuning)")]
        [Tooltip("How frequently the system checks the altitude above the ground. Lower frequency improves performance but reduces responsiveness.")]
        public QueryFrequency groundCheckFrequency = QueryFrequency.Every_0_3_Seconds;
        [Tooltip("How frequently the system probes forward for obstacles when in the 'Searching' state. Higher frequency improves reaction time but increases cost.")]
        public QueryFrequency forwardCheckFrequency = QueryFrequency.Every3Frames;

        [Header("--- Runtime Sensory Parameters ---")]
        [Header("Collision & Sensory Layers")]
        [Tooltip("The LayerMask used to detect the ground or terrain for altitude readings.")]
        public LayerMask groundLayerMask;
        [Tooltip("The LayerMask used to detect obstacles (buildings, asteroids, other ships) for avoidance.")]
        public LayerMask obstacleLayerMask;

        // Fields initialized with default values...
        [Header("Settings (Runtime Values)")]
        [Tooltip("The maximum distance (in meters) for ground checks.")]
        public float maxGroundCheckDistance = 5000f;
        [Tooltip("The distance (in meters) for forward obstacle checks.")]
        public float forwardCheckDistance = 300f;
        [Tooltip("The radius (in meters) of the sphere cast used for checks.")]
        public float checkRadius = 10f;
        [Tooltip("The angle (in degrees) of the avoidance scan cone.")]
        public float coneAngle = 30f;
        [Tooltip("The number of rays used in the avoidance scan cone.")]
        public int coneRayCount = 8;
        [Tooltip("Duration (in seconds) the system stays in the 'Avoiding' state.")]
        public float avoidanceDuration = 2.0f;
        [Tooltip("Duration (in seconds) the system stays in 'Cooldown' after avoiding.")]
        public float cooldownDuration = 1.0f;
        [Tooltip("Bonus score applied to upward avoidance paths.")]
        public float upwardBiasBonus = 150f;
        [Tooltip("Bonus score applied to avoidance paths aligning with current turning inertia.")]
        public float turningInertiaBiasBonus = 200f;

        // Public API
        /// <summary>
        /// Enum indicating the general direction of the calculated evasion maneuver.
        /// </summary>
        public enum EvasionDirection { None, Up, Down, Left, Right }
        /// <summary>
        /// Indicates if the system is currently in the 'Avoiding' state due to a detected obstacle.
        /// </summary>
        public bool IsObstacleAhead => currentState == SensoryState.Avoiding;
        /// <summary>
        /// The calculated world-space vector representing the optimal avoidance path.
        /// </summary>
        public Vector3 ObstacleAvoidanceVector { get; private set; }
        /// <summary>
        /// A hint indicating the primary direction of the avoidance maneuver.
        /// </summary>
        public EvasionDirection AvoidanceDirectionHint { get; private set; }

        /// <summary>
        /// The current altitude above the ground (in meters), as reported by the active sensor provider.
        /// </summary>
        // Altitude is now retrieved directly from the provider. Provides a fallback if provider is not yet initialized.
        public float AltitudeAboveGround => _sensorProvider != null ? _sensorProvider.AltitudeAboveGround : maxGroundCheckDistance;

        /// <summary>
        /// Invoked when the system detects an immediate obstacle and transitions into the Avoiding state.
        /// </summary>
        public event Action OnProximityAlert;
        private enum SensoryState { Searching, Avoiding, Cooldown }
        private SensoryState currentState = SensoryState.Searching;

        private Rigidbody shipRigidbody;
        private float stateTimer;

        void Awake()
        {
            shipRigidbody = GetComponent<Rigidbody>();
            LoadSettings();
        }

        void Start()
        {
            // Initialize the provider in Start to ensure managers are ready.
            InitializeProvider();
            // Cache the initial provider type.
            _cachedProviderType = providerType;
        }

        void OnEnable()
        {
            // Manage scanning state when the component is enabled/re-enabled.
            if (_sensorProvider != null)
            {
                ActivateScanningBasedOnState();
            }
        }

        void OnDisable()
        {
            // Stop scanning when the component is disabled.
            _sensorProvider?.StopAllScanning();
        }

        #region Provider Management

        private void InitializeProvider()
        {
            // Clean up any existing provider component before adding a new one
            CleanupExistingProvider();

            // The provider is currently set to use the SimpleSensorProvider by default.
            // To switch to a different provider (like the AdvancedSensorProvider), you would:
            // 1. Ensure the script file (e.g., AdvancedSensorProvider.cs) is in your project.
            // 2. Uncomment the corresponding "else if" block below.
            // 3. Set the 'providerType' variable in the Inspector to your desired provider.


            if (providerType == SensorProviderType.SimpleAltitudeOnly)
            {
                _sensorProvider = gameObject.AddComponent<SimpleAltitudeProvider>();
            }


            if (providerType == SensorProviderType.Simple)
            {
                _sensorProvider = gameObject.AddComponent<SimpleSensorProvider>();
            }
            // else if (providerType == SensorProviderType.Advanced)
            // {
            //    _sensorProvider = gameObject.AddComponent<AdvancedSensorProvider>();
            // }

            if (_sensorProvider == null)
            {
                Debug.LogError("ShipSensorySystem: Failed to initialize ISensorProvider. Ensure the selected provider type is valid and available.", this);
                return;
            }

            // Initialize the provider with current settings
            _sensorProvider.Initialize(transform, shipRigidbody, GetCurrentSettings());
            _sensorProvider.OnObstacleDetected += HandleObstacleDetected;

            // Ensure the scanning state matches the current sensory state if the object is active.
            if (isActiveAndEnabled)
            {
                ActivateScanningBasedOnState();
            }
        }

        private void CleanupExistingProvider()
        {
            if (_sensorProvider != null)
            {
                _sensorProvider.OnObstacleDetected -= HandleObstacleDetected;
                _sensorProvider.StopAllScanning();

                // If the provider is a MonoBehaviour attached to this GameObject, destroy the component.
                if (_sensorProvider is MonoBehaviour providerMB && providerMB != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(providerMB);
                    }
                    else
                    {
                        // Handle cleanup during OnValidate in the editor safely.
#if UNITY_EDITOR
                        // Use delayCall to safely destroy immediate when called from OnValidate.
                        UnityEditor.EditorApplication.delayCall += () =>
                        {
                            if (providerMB != null) DestroyImmediate(providerMB);
                        };
#else
                        // Should not happen in a build if Application.isPlaying is false, but handle defensively.
                        Destroy(providerMB);
#endif
                    }
                }
                _sensorProvider = null;
            }
        }

        private void ActivateScanningBasedOnState()
        {
            if (_sensorProvider == null) return;

            // StartPassiveScanning ensures ground checks are active.
            _sensorProvider.StartPassiveScanning();

            // If we are avoiding or cooling down, ensure forward scanning is explicitly stopped.
            if (currentState != SensoryState.Searching)
            {
                _sensorProvider.StopForwardScanning();
            }
        }

        private ShipSensorySystemSettings GetCurrentSettings()
        {
            return new ShipSensorySystemSettings
            {
                GroundLayerMask = groundLayerMask,
                ObstacleLayerMask = obstacleLayerMask,
                MaxGroundCheckDistance = maxGroundCheckDistance,
                ForwardCheckDistance = forwardCheckDistance,
                CheckRadius = checkRadius,
                ConeAngle = coneAngle,
                ConeRayCount = coneRayCount,
                GroundCheckFrequency = groundCheckFrequency,
                ForwardCheckFrequency = forwardCheckFrequency,
                SeaLevelY = this.seaLevelY
            };
        }

        #endregion

        #region Settings Management

        /// <summary>
        /// Loads the settings from the assigned SensorySettingsSO if 'useProfile' is enabled.
        /// </summary>
        public void LoadSettings()
        {
            if (useProfile && settingsSO != null)
            {
                LoadSettingsData(settingsSO);
            }
        }

        private void LoadSettingsData(SensorySettingsSO data)
        {
            maxGroundCheckDistance = data.maxGroundCheckDistance;
            forwardCheckDistance = data.forwardCheckDistance;
            checkRadius = data.checkRadius;
            coneAngle = data.coneAngle;
            coneRayCount = data.coneRayCount;
            avoidanceDuration = data.avoidanceDuration;
            cooldownDuration = data.cooldownDuration;
            upwardBiasBonus = data.upwardBiasBonus;
            turningInertiaBiasBonus = data.turningInertiaBiasBonus;
        }

        void OnValidate()
        {
            // This method should now ONLY be used to update the inspector view
            // from the ScriptableObject when not in play mode.
            if (useProfile && settingsSO != null)
            {
                LoadSettingsData(settingsSO);
            }

            // The problematic runtime re-initialization has been removed from here.
        }

        #endregion

        void Update()
        {
            // Check if the provider type is changed in the inspector during play mode.
            if (Application.isPlaying && providerType != _cachedProviderType)
            {
                // Re-initialize the provider and update the cached type.
                InitializeProvider();
                _cachedProviderType = providerType;
            }

            // State machine logic for timing avoidance and cooldown periods.
            switch (currentState)
            {
                case SensoryState.Avoiding:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        TransitionToCooldown();
                    }
                    break;

                case SensoryState.Cooldown:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        TransitionToSearching();
                    }
                    break;
            }
        }

        #region Sensory Logic and Callbacks

        // Callback invoked by the ISensorProvider when an obstacle is detected during passive scanning.
        private void HandleObstacleDetected(Vector3 detectionDirection)
        {
            // Safety check: Ignore if we are no longer searching.
            if (currentState != SensoryState.Searching) return;

            // Initiate the active, synchronous scan using the provider.
            InitiateAvoidanceScan(detectionDirection);
        }

        // Delegates the active scan to the provider.
        private void InitiateAvoidanceScan(Vector3 initialDirection)
        {
            if (_sensorProvider == null) return;

            // 1. Perform the scan (synchronously)
            List<ScanResult> scanResults = _sensorProvider.PerformActiveAvoidanceScan(initialDirection);

            // 2. Decide the best path and transition state.
            if (scanResults != null && scanResults.Count > 0)
            {
                ChooseBestAvoidancePath(scanResults);
            }
            else
            {
                // Handle case where scan failed or returned no results
                Debug.LogWarning("ShipSensorySystem: Active avoidance scan returned no results.", this);
                // Fallback behavior: attempt a default avoidance maneuver.
                SetDefaultAvoidance();
            }
            TransitionToAvoiding();
        }

        #endregion

        #region State Transitions

        private void TransitionToAvoiding()
        {
            if (currentState == SensoryState.Avoiding) return;

            currentState = SensoryState.Avoiding;
            stateTimer = avoidanceDuration;

            // Tell the provider to stop the forward check while avoiding.
            _sensorProvider?.StopForwardScanning();

            OnProximityAlert?.Invoke();
        }

        private void TransitionToCooldown()
        {
            currentState = SensoryState.Cooldown;
            stateTimer = cooldownDuration;
            AvoidanceDirectionHint = EvasionDirection.None;
        }

        private void TransitionToSearching()
        {
            currentState = SensoryState.Searching;
            // Tell the provider to resume full passive scanning (including forward checks).
            _sensorProvider?.StartPassiveScanning();
        }

        #endregion

        #region Avoidance Decision Logic

        private void ChooseBestAvoidancePath(List<ScanResult> results)
        {
            float bestScore = -1f;
            ScanResult bestResult = new();

            // Filter results that are safe (no hit or distance greater than immediate collision range)
            foreach (var result in results.Where(r => !r.DidHit || r.Distance > checkRadius * 2))
            {
                float score = result.Distance;

                // Upward bias
                float upwardDot = Vector3.Dot(result.Direction, Vector3.up);
                if (upwardDot > 0.3f)
                {
                    score += upwardBiasBonus * upwardDot;
                }

                // Turning inertia bias
                // Using angularVelocity for Unity 6 (2025.1+) compatibility.
                if (shipRigidbody.angularVelocity.sqrMagnitude > 0.1f)
                {
                    Vector3 angularVel = shipRigidbody.angularVelocity.normalized;
                    float turnDot = Vector3.Dot(result.Direction, angularVel);
                    if (turnDot > 0)
                    {
                        score += turningInertiaBiasBonus * turnDot;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestResult = result;
                }
            }

            if (bestScore > -1f)
            {
                ObstacleAvoidanceVector = bestResult.Direction;
                SetAvoidanceHint(ObstacleAvoidanceVector);
            }
            else
            {
                // No safe path found, use default reaction.
                SetDefaultAvoidance();
            }
        }

        private void SetDefaultAvoidance()
        {
            // Default reaction: attempt to move opposite the current velocity.
            ObstacleAvoidanceVector = -GetVelocityDirectionFallback();
            AvoidanceDirectionHint = EvasionDirection.None;
        }


        // Helper to get the current velocity direction, used as a fallback in avoidance logic.
        private Vector3 GetVelocityDirectionFallback()
        {
            // Use linearVelocity for Rigidbody in Unity 6 (2025.1+) compatibility.
            if (shipRigidbody.linearVelocity.magnitude > 1.0f)
            {
                return shipRigidbody.linearVelocity.normalized;
            }
            return transform.forward;
        }

        private void SetAvoidanceHint(Vector3 direction)
        {
            Vector3 localDir = transform.InverseTransformDirection(direction);

            // Determine the dominant axis for the hint.
            if (Mathf.Abs(localDir.y) > Mathf.Abs(localDir.x) && Mathf.Abs(localDir.y) > Mathf.Abs(localDir.z))
            {
                AvoidanceDirectionHint = localDir.y > 0 ? EvasionDirection.Up : EvasionDirection.Down;
            }
            // Assuming +Z as forward, the local X-axis is Left/Right.
            else if (Mathf.Abs(localDir.x) > Mathf.Abs(localDir.y) && Mathf.Abs(localDir.x) > Mathf.Abs(localDir.z))
            {
                AvoidanceDirectionHint = localDir.x > 0 ? EvasionDirection.Right : EvasionDirection.Left;
            }
            else
            {
                AvoidanceDirectionHint = EvasionDirection.None;
            }
        }
        #endregion
    }
}