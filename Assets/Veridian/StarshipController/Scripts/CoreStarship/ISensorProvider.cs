using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Defines the frequency options for sensor queries, balancing performance and responsiveness.
    /// </summary>
    public enum QueryFrequency
    {
        // High Frequency (Frame-based)
        [Tooltip("Query runs every frame. Most responsive, highest performance cost.")]
        EveryFrame = 0,
        [Tooltip("Query runs every 2 frames.")]
        Every2Frames = 1,
        [Tooltip("Query runs every 3 frames.")]
        Every3Frames = 2,
        // Low Frequency (Time-based, Time-sliced)
        [Tooltip("Query runs approximately every 0.1 seconds.")]
        Every_0_1_Seconds = 10,
        [Tooltip("Query runs approximately every 0.3 seconds.")]
        Every_0_3_Seconds = 11,
        [Tooltip("Query runs approximately every 0.6 seconds.")]
        Every_0_6_Seconds = 12,
        [Tooltip("Query runs approximately every 1 second. Least responsive, lowest performance cost.")]
        Every_1_Second = 13
    }

    /// <summary>
    /// Defines the contract for a sensory provider implementation used by the ShipSensorySystem.
    /// Allows for swappable implementations (e.g., simple direct physics vs. complex batched queries).
    /// </summary>
    public interface ISensorProvider
    {
        /// <summary>
        /// Gets the most recently measured altitude above the ground.
        /// </summary>
        float AltitudeAboveGround { get; }

        /// <summary>
        /// Invoked when the passive forward scan detects an imminent obstacle.
        /// The Vector3 argument is the direction of the detected obstacle (usually the ship's velocity direction).
        /// </summary>
        event Action<Vector3> OnObstacleDetected;

        /// <summary>
        /// Initializes the sensor provider with the necessary context and settings.
        /// </summary>
        /// <param name="shipTransform">The transform of the ship.</param>
        /// <param name="shipRigidbody">The rigidbody of the ship.</param>
        /// <param name="settings">The sensory settings to use.</param>
        void Initialize(Transform shipTransform, Rigidbody shipRigidbody, ShipSensorySystemSettings settings);

        /// <summary>
        /// Starts the passive scanning processes (ground check and forward obstacle check).
        /// If forward scanning was previously stopped, this restarts it.
        /// </summary>
        void StartPassiveScanning();

        /// <summary>
        /// Stops the passive forward obstacle check. Used when the ship enters an avoidance state.
        /// Ground checks should continue running unless StopAllScanning is called.
        /// </summary>
        void StopForwardScanning();

        /// <summary>
        /// Stops all passive scanning processes.
        /// </summary>
        void StopAllScanning();

        /// <summary>
        /// Performs an immediate, synchronous scan in a cone to find an avoidance route.
        /// </summary>
        /// <param name="scanDirection">The central direction of the scan.</param>
        /// <returns>A list of scan results detailing hits and distances in various directions.</returns>
        List<ScanResult> PerformActiveAvoidanceScan(Vector3 scanDirection);
    }

    /// <summary>
    /// Helper struct to hold configuration data for the sensor provider, decoupled from ScriptableObjects.
    /// </summary>
    public struct ShipSensorySystemSettings
    {
        // --- Physics Provider Settings ---
        public LayerMask GroundLayerMask;
        public LayerMask ObstacleLayerMask;
        public float MaxGroundCheckDistance;
        public float ForwardCheckDistance;
        public float CheckRadius;
        public float ConeAngle;
        public int ConeRayCount;
        public QueryFrequency GroundCheckFrequency;
        public QueryFrequency ForwardCheckFrequency;

        // --- Simple Altitude Provider Setting ---

        /// <summary>
        /// The world-space Y-coordinate of the sea level. Used by SimpleAltitudeProvider.
        /// </summary>
        public float SeaLevelY;
    }

    /// <summary>
    /// Helper struct to return scan results, decoupled from RaycastHit.
    /// </summary>
    public struct ScanResult
    {
        public bool DidHit;
        public float Distance;
        public Vector3 Direction;
    }
}