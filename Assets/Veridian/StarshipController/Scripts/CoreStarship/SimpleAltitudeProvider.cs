using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// A minimal, "no-layers" sensor provider.
    /// It only calculates altitude relative to a fixed 'SeaLevelY' and does not perform any
    /// physics-based obstacle detection, ensuring it works "out of the box" with no setup.
    /// </summary>
    public class SimpleAltitudeProvider : MonoBehaviour, ISensorProvider
    {
        public float AltitudeAboveGround { get; private set; }

#pragma warning disable CS0067
        /// <summary>
        /// This event will never be invoked by this provider.
        /// </summary>
        public event Action<Vector3> OnObstacleDetected;
#pragma warning restore CS0067

        private Transform _shipTransform;
        private float _seaLevelY;
        private bool _isScanning = false;

        public void Initialize(Transform shipTransform, Rigidbody shipRigidbody, ShipSensorySystemSettings settings)
        {
            _shipTransform = shipTransform;
            _seaLevelY = settings.SeaLevelY;

            // Calculate initial altitude
            if (_shipTransform != null)
            {
                AltitudeAboveGround = _shipTransform.position.y - _seaLevelY;
            }
            else
            {
                AltitudeAboveGround = 0f;
            }
        }

        public void StartPassiveScanning()
        {
            _isScanning = true;
        }

        public void StopForwardScanning()
        {
            // This provider does no forward scanning, so this method is empty.
        }

        public void StopAllScanning()
        {
            _isScanning = false;
        }

        /// <summary>
        /// This provider does not detect obstacles.
        /// </summary>
        /// <returns>An empty list. Always.</returns>
        public List<ScanResult> PerformActiveAvoidanceScan(Vector3 scanDirection)
        {
            // Return an empty list to signify no safe paths were found / no scan was performed.
            // The ShipSensorySystem will then use its default avoidance.
            return new List<ScanResult>();
        }

        /// <summary>
        /// Continuously updates the altitude based on the ship's Y-position.
        /// </summary>
        void Update()
        {
            if (!_isScanning || _shipTransform == null)
            {
                return;
            }

            // The only job of this provider: calculate altitude from the fixed sea level.
            AltitudeAboveGround = _shipTransform.position.y - _seaLevelY;
        }
    }
}