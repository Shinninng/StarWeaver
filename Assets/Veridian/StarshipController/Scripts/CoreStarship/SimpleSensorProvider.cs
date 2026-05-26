using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.Core
{

    public class SimpleSensorProvider : MonoBehaviour, ISensorProvider
    {
        public float AltitudeAboveGround { get; private set; }
        public event Action<Vector3> OnObstacleDetected;

        private Transform _shipTransform;
        private Rigidbody _shipRigidbody;
        private ShipSensorySystemSettings _settings;
        private bool _isInitialized = false;
        private bool _isScanning = false;
        private bool _isForwardScanning = false;

        // Frequency tracking variables
        private float _groundCheckTimer = 0f;
        private float _forwardCheckTimer = 0f;
        private int _groundCheckFrameCounter = 0;
        private int _forwardCheckFrameCounter = 0;

        #region ISensorProvider Implementation

        public void Initialize(Transform shipTransform, Rigidbody shipRigidbody, ShipSensorySystemSettings settings)
        {
            _shipTransform = shipTransform;
            _shipRigidbody = shipRigidbody;
            _settings = settings;
            AltitudeAboveGround = settings.MaxGroundCheckDistance;
            _isInitialized = true;
        }

        public void StartPassiveScanning()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("SimpleSensorProvider: Cannot start scanning before initialization.", this);
                return;
            }
            _isScanning = true;
            _isForwardScanning = true;

            // Initialize timers if they haven't been set, ensuring they start fresh.
            if (_groundCheckTimer <= 0)
            {
                _groundCheckTimer = GetTimeInterval(_settings.GroundCheckFrequency);
            }
            if (_forwardCheckTimer <= 0)
            {
                _forwardCheckTimer = GetTimeInterval(_settings.ForwardCheckFrequency);
            }
        }

        public void StopForwardScanning()
        {
            _isForwardScanning = false;
        }

        public void StopAllScanning()
        {
            _isScanning = false;
            _isForwardScanning = false;
        }

        public List<ScanResult> PerformActiveAvoidanceScan(Vector3 scanDirection)
        {
            if (!_isInitialized) return new List<ScanResult>();

            int rayCount = _settings.ConeRayCount;
            List<ScanResult> scanResults = new(rayCount);

            if (rayCount < 1) return scanResults;

            // Handle the edge case of a single ray scan
            if (rayCount == 1)
            {
                bool didHit = Physics.SphereCast(_shipTransform.position, _settings.CheckRadius, scanDirection, out RaycastHit hit, _settings.ForwardCheckDistance, _settings.ObstacleLayerMask, QueryTriggerInteraction.Ignore);
                scanResults.Add(new ScanResult
                {
                    DidHit = didHit,
                    Distance = didHit ? hit.distance : _settings.ForwardCheckDistance,
                    Direction = scanDirection
                });
                return scanResults;
            }

            // Perform the multi-ray scan synchronously
            for (int i = 0; i < rayCount; i++)
            {
                float t = (float)i / (rayCount - 1);
                Vector3 direction = CalculateConeDirection(t, scanDirection);

                bool didHit = Physics.SphereCast(_shipTransform.position, _settings.CheckRadius, direction, out RaycastHit hit, _settings.ForwardCheckDistance, _settings.ObstacleLayerMask, QueryTriggerInteraction.Ignore);

                scanResults.Add(new ScanResult
                {
                    DidHit = didHit,
                    Distance = didHit ? hit.distance : _settings.ForwardCheckDistance,
                    Direction = direction
                });
            }

            return scanResults;
        }

        #endregion

        #region Unity Lifecycle and Processing

        void Update()
        {
            if (!_isScanning || !_isInitialized) return;

            ProcessGroundCheck();
            ProcessForwardCheck();
        }

        private void ProcessGroundCheck()
        {
            if (IsQueryDue(_settings.GroundCheckFrequency, ref _groundCheckTimer, ref _groundCheckFrameCounter))
            {
                PerformGroundCheck();
            }
        }

        private void ProcessForwardCheck()
        {
            if (!_isForwardScanning) return;

            if (IsQueryDue(_settings.ForwardCheckFrequency, ref _forwardCheckTimer, ref _forwardCheckFrameCounter))
            {
                PerformForwardCheck();
            }
        }

        #endregion

        #region Physics Operations

        private void PerformGroundCheck()
        {
            if (Physics.Raycast(_shipTransform.position, Vector3.down, out RaycastHit hit, _settings.MaxGroundCheckDistance, _settings.GroundLayerMask, QueryTriggerInteraction.Ignore))
            {
                AltitudeAboveGround = hit.distance;
            }
            else
            {
                AltitudeAboveGround = _settings.MaxGroundCheckDistance;
            }
        }

        private void PerformForwardCheck()
        {
            Vector3 direction = GetVelocityDirection();
            if (Physics.SphereCast(_shipTransform.position, _settings.CheckRadius, direction, out RaycastHit hit, _settings.ForwardCheckDistance, _settings.ObstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                // Obstacle detected! Notify the ShipSensorySystem.
                OnObstacleDetected?.Invoke(direction);
            }
        }

        #endregion

        #region Helper Methods

        private Vector3 GetVelocityDirection()
        {
            // Use linearVelocity for Rigidbody in Unity 6 (2025.1+) compatibility.
            if (_shipRigidbody != null && _shipRigidbody.linearVelocity.magnitude > 1.0f)
            {
                return _shipRigidbody.linearVelocity.normalized;
            }
            return _shipTransform != null ? _shipTransform.forward : Vector3.forward;
        }

        private Vector3 CalculateConeDirection(float t, Vector3 initialDirection)
        {
            // Logic preserved from the original ShipSensorySystem implementation
            float horizontalAngle = Mathf.Lerp(-_settings.ConeAngle, _settings.ConeAngle, t);
            Quaternion horizontalRotation = Quaternion.AngleAxis(horizontalAngle, _shipTransform.up);

            float verticalAngle = Mathf.Lerp(-_settings.ConeAngle, _settings.ConeAngle, t);
            // Vertical scan (pitch) rotates around the right axis.
            Quaternion verticalRotation = Quaternion.AngleAxis(verticalAngle, _shipTransform.right);

            return verticalRotation * horizontalRotation * initialDirection;
        }

        // Determines if a query should run this frame based on its frequency setting.
        private bool IsQueryDue(QueryFrequency frequency, ref float timer, ref int frameCounter)
        {
            switch (frequency)
            {
                // Frame-based frequencies
                case QueryFrequency.EveryFrame:
                    return true;
                case QueryFrequency.Every2Frames:
                case QueryFrequency.Every3Frames:
                    int interval = (frequency == QueryFrequency.Every2Frames) ? 2 : 3;
                    frameCounter++;
                    if (frameCounter >= interval)
                    {
                        frameCounter = 0;
                        return true;
                    }
                    return false;

                // Time-based frequencies
                case QueryFrequency.Every_0_1_Seconds:
                case QueryFrequency.Every_0_3_Seconds:
                case QueryFrequency.Every_0_6_Seconds:
                case QueryFrequency.Every_1_Second:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        // Reset timer, accounting for potential overshoot to maintain average frequency
                        timer = GetTimeInterval(frequency) + timer;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private float GetTimeInterval(QueryFrequency frequency) =>
    frequency switch
    {
        QueryFrequency.Every_0_1_Seconds => 0.1f,
        QueryFrequency.Every_0_3_Seconds => 0.3f,
        QueryFrequency.Every_0_6_Seconds => 0.6f,
        QueryFrequency.Every_1_Second => 1.0f,
        _ => 0f // The 'default' case becomes '_'
    };

        #endregion
    }
}