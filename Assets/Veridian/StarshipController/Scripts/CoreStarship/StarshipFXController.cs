using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Weapons;

/// <summary>
/// Optional component to handle VFX and SFX for a starship.
/// Focuses exclusively on continuous movement, engine, and weapon effects.
/// </summary>
namespace Veridian.Starship.Core
{
    public class StarshipFXController : MonoBehaviour
    {
        [Header("--- Audio Sources ---")]
        [Tooltip("Handles primary continuous engine sounds (main loop).")]
        public AudioSource engineAudioSource;
        [Tooltip("Handles boost overlay sounds.")]
        public AudioSource boostAudioSource;
        [Tooltip("Handles continuous movement sounds (wind, atmospheric effects).")]
        public AudioSource movementAudioSource;
        [Tooltip("Handles primary one-shot sounds (weapons, impacts, major alerts, G-force stress).")]
        public AudioSource oneShotAudioSource;
        [Tooltip("Handles reverse thruster sounds.")]
        public AudioSource reverseThrusterAudioSource;
        [Tooltip("Handles vertical thruster sounds.")]
        public AudioSource verticalThrusterAudioSource;

        // --- VFX References ---
        [Header("--- Engine VFX ---")]
        [Tooltip("Particle systems for the main forward thrusters.")]
        public List<ParticleSystem> mainThrustersVFX;
        [Tooltip("Particle systems for the reverse (braking) thrusters.")]
        public List<ParticleSystem> reverseThrustersVFX;
        [Tooltip("Particle systems for the vertical (VTOL) thrusters.")]
        public List<ParticleSystem> verticalThrustersVFX;
        [Tooltip("Optional: Specific VFX that only activate during boost (e.g., shock diamonds, brighter core).")]
        public List<ParticleSystem> boostSpecificVFX;

        [Header("--- Movement VFX ---")]
        [Tooltip("World-space particle system for speed lines.")]
        public ParticleSystem speedLinesVFX;
        public float speedLinesActivationThreshold = 150f;

        // REMOVED: [Header("--- Damage VFX (One-Shot) ---")] and associated fields.

        [Header("--- Weapon VFX (Muzzle Flash Prefabs) ---")]
        [Tooltip("VFX Prefab instantiated at the fire point for primary weapons.")]
        public ParticleSystem primaryMuzzleFlashPrefab;
        [Tooltip("VFX Prefab instantiated at the fire point for secondary weapons.")]
        public ParticleSystem secondaryMuzzleFlashPrefab;

        // --- SFX References ---
        [Header("--- Engine SFX ---")]
        public AudioClip mainEngineLoopClip;
        public AudioClip boostOverlayLoopClip;
        public AudioClip reverseThrusterLoopClip;
        public AudioClip verticalThrusterLoopClip;

        [Header("--- Movement SFX ---")]
        public AudioClip windLoopClip;
        [Tooltip("Intermittent sounds played when pulling high Gs.")]
        public List<AudioClip> gForceStressClips;
        public float minGForceForStressSound = 7.0f;
        public float gForceSoundCooldown = 2.5f;

        // REMOVED: [Header("--- Damage SFX ---")] and associated fields.

        [Header("--- Weapon SFX ---")]
        public AudioClip primaryFireClip;
        public AudioClip secondaryFireClip;
        public AudioClip bombDropClip;

        [Header("--- System SFX ---")]
        public AudioClip proximityAlertClip;

        // Internal References
        private AtmosphericStarshipController shipController;
        private HealthComponent healthComponent; // Retained reference to poll IsAlive status.
        private ShipWeaponController weaponController;
        private ShipSensorySystem sensorySystem;

        // Internal State
        private float lastGForceSoundTime = -10f;
        // Cache initial emission rates to modulate them based on thrust
        private Dictionary<ParticleSystem, float> defaultEmissionRates = new();
        private bool isAlive = true;
        private bool wasAliveLastFrame = true; // Used to detect the moment of death

        void Awake()
        {
            // Find required components by searching the parent hierarchy
            shipController = GetComponentInParent<AtmosphericStarshipController>();
            healthComponent = GetComponentInParent<HealthComponent>();
            weaponController = GetComponentInParent<ShipWeaponController>();
            sensorySystem = GetComponentInParent<ShipSensorySystem>();

            // This check is now even more important to ensure the child is parented correctly.
            if (shipController == null)
            {
                Debug.LogWarning("StarshipFXController could not find an AtmosphericStarshipController on its parent. Disabling FX.", this);
                this.enabled = false;
                return;
            }

            CacheDefaultEmissionRates();
            InitializeAudioSources();
        }
        void OnEnable()
        {
            // Reset internal state for when the ship is re-enabled (respawned).
            ResetFXState();

            // Re-subscribe to events (ensure this is done after state reset if needed).
            SubscribeToEvents();

            // Optionally, re-initialize audio sources if they need specific setup beyond playing.
            InitializeAudioSources(); // Call this to ensure sources are playing (but start silent).
        }
        void OnDisable()
        {
            // Unsubscribe from events first.
            UnsubscribeFromEvents();

            // Stop all continuous VFX and SFX immediately.
            StopAllContinuousFX();

            // Optional: Stop one-shot source if it might be playing something long.
            if (oneShotAudioSource != null) oneShotAudioSource.Stop();
        }

        #region Initialization and Setup

        private void InitializeAudioSources()
        {
            SetupLoopingAudioSource(engineAudioSource, mainEngineLoopClip);
            SetupLoopingAudioSource(boostAudioSource, boostOverlayLoopClip);
            SetupLoopingAudioSource(movementAudioSource, windLoopClip);
            SetupLoopingAudioSource(reverseThrusterAudioSource, reverseThrusterLoopClip);
            SetupLoopingAudioSource(verticalThrusterAudioSource, verticalThrusterLoopClip);
        }

        private void SetupLoopingAudioSource(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.clip = clip;
                source.loop = true;
                source.volume = 0f; // Start silent
                source.Play();
            }
        }

        // Store the initial 'Rate over Time' setting from the Inspector.
        private void CacheDefaultEmissionRates()
        {
            CacheRates(mainThrustersVFX);
            CacheRates(reverseThrustersVFX);
            CacheRates(verticalThrustersVFX);
            CacheRates(boostSpecificVFX);
            if (speedLinesVFX != null)
            {
                var emission = speedLinesVFX.emission;
                defaultEmissionRates[speedLinesVFX] = emission.rateOverTime.constant;
            }
        }

        private void CacheRates(List<ParticleSystem> systems)
        {
            if (systems == null) return;
            foreach (var ps in systems)
            {
                if (ps != null && !defaultEmissionRates.ContainsKey(ps))
                {
                    var emission = ps.emission;
                    defaultEmissionRates[ps] = emission.rateOverTime.constant;
                }
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            // REMOVED: HealthComponent subscriptions (OnImpact, OnDeathEvent, etc.)

            if (weaponController != null)
            {
                weaponController.OnPrimaryFired += HandlePrimaryFired;
                weaponController.OnSecondaryFired += HandleSecondaryFired;
                weaponController.OnBombDropped += HandleBombDropped;
            }

            if (sensorySystem != null)
            {
                sensorySystem.OnProximityAlert += HandleProximityAlert;
            }

            if (shipController != null)
            {
                shipController.OnGForceUpdate += HandleGForceUpdate;
            }
        }

        private void UnsubscribeFromEvents()
        {
            // REMOVED: HealthComponent unsubscriptions

            if (weaponController != null)
            {
                weaponController.OnPrimaryFired -= HandlePrimaryFired;
                weaponController.OnSecondaryFired -= HandleSecondaryFired;
                weaponController.OnBombDropped -= HandleBombDropped;
            }

            if (sensorySystem != null)
            {
                sensorySystem.OnProximityAlert -= HandleProximityAlert;
            }

            if (shipController != null)
            {
                shipController.OnGForceUpdate -= HandleGForceUpdate;
            }
        }

        #endregion

        void Update()
        {
            // Poll HealthComponent for status change, as events are removed.
            if (healthComponent != null)
            {
                isAlive = healthComponent.IsAlive;
            }

            // Detect the transition from alive to dead
            if (wasAliveLastFrame && !isAlive)
            {
                // Stop continuous effects immediately upon death detection.
                StopAllContinuousFX();
            }
            wasAliveLastFrame = isAlive;

            // If the ship is destroyed or inactive, stop updating FX.
            if (!isAlive || !shipController.isActiveAndEnabled)
            {
                return;
            }

            // Read continuous data from the controller
            float forwardThrust = shipController.CurrentThrustLevel;
            float verticalThrust = shipController.CurrentVerticalThrustLevel;
            float speed = shipController.CurrentSpeed;
            bool isBoosting = shipController.IsTryingToBoost;

            UpdateEngineFX(forwardThrust, verticalThrust, isBoosting);
            UpdateMovementFX(speed);
        }

        #region Continuous Effects (Engine, Boost, Movement)

        private void UpdateEngineFX(float forwardThrust, float verticalThrust, bool isBoosting)
        {
            // Separate forward and reverse thrust levels
            float mainThrustLevel = Mathf.Clamp01(forwardThrust);
            float reverseThrustLevel = Mathf.Clamp01(-forwardThrust);
            float verticalThrustLevel = Mathf.Abs(verticalThrust);

            // VFX Modulation
            SetThrusterVFXState(mainThrustersVFX, mainThrustLevel);
            SetThrusterVFXState(reverseThrustersVFX, reverseThrustLevel);
            SetThrusterVFXState(verticalThrustersVFX, verticalThrustLevel);

            // Boost VFX Toggle
            SetBoostVFXActive(isBoosting);

            // SFX
            HandleEngineAudio(mainThrustLevel, isBoosting);
            HandleBoostAudio(isBoosting);
            HandleReverseThrusterAudio(reverseThrustLevel);
            HandleVerticalThrusterAudio(verticalThrustLevel);
        }

        // Modulates the particle emission rate based on the thrust level.
        private void SetThrusterVFXState(List<ParticleSystem> systems, float thrustLevel)
        {
            if (systems == null) return;

            foreach (var ps in systems)
            {
                if (ps == null) continue;

                var emission = ps.emission;
                if (thrustLevel > 0.01f)
                {
                    if (!ps.isPlaying)
                    {
                        ps.Play();
                    }
                    // Modulate emission rate based on cached default rate
                    if (defaultEmissionRates.TryGetValue(ps, out float defaultRate))
                    {
                        // We use the thrustLevel (0-1) to scale the emission rate.
                        emission.rateOverTime = defaultRate * thrustLevel;
                    }
                }
                else
                {
                    // Set rate to 0 when thrust is off.
                    if (ps.isEmitting)
                    {
                        emission.rateOverTime = 0;
                    }
                }
            }
        }

        // Toggles specific boost VFX on or off.
        private void SetBoostVFXActive(bool isActive)
        {
            if (boostSpecificVFX == null) return;
            foreach (var ps in boostSpecificVFX)
            {
                if (ps == null) continue;

                var emission = ps.emission;

                if (isActive)
                {
                    if (!ps.isPlaying)
                    {
                        ps.Play();
                    }
                    // Ensure emission rate is set to default when active (as these are overlays)
                    if (defaultEmissionRates.TryGetValue(ps, out float defaultRate))
                    {
                        emission.rateOverTime = defaultRate;
                    }
                }
                else if (ps.isEmitting)
                {
                    // Stop emitting new particles
                    emission.rateOverTime = 0;
                }
            }
        }

        private void HandleEngineAudio(float mainThrustLevel, bool isBoosting)
        {
            if (engineAudioSource == null) return;

            // Calculate target volume and pitch
            float targetVolume = mainThrustLevel;
            float targetPitch = 0.8f + mainThrustLevel * 0.4f; // Pitch varies with thrust

            // Adjust volume/pitch if boosting for extra intensity
            if (isBoosting)
            {
                targetVolume = Mathf.Max(targetVolume, 0.8f); // Ensure boost has high volume
                targetPitch += 0.2f;
            }

            // Smoothly adjust volume and pitch
            engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVolume, Time.deltaTime * 5f);
            engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.deltaTime * 5f);
        }

        private void HandleBoostAudio(bool isBoosting)
        {
            if (boostAudioSource == null) return;

            float targetVolume = isBoosting ? 1.0f : 0.0f;
            // Smoothly ramp the boost audio in and out
            boostAudioSource.volume = Mathf.Lerp(boostAudioSource.volume, targetVolume, Time.deltaTime * 8f);
        }

        private void HandleReverseThrusterAudio(float reverseThrustLevel)
        {
            if (reverseThrusterAudioSource == null) return;

            // Volume is directly proportional to the reverse thrust level
            float targetVolume = reverseThrustLevel;
            // Pitch increases slightly with more thrust
            float targetPitch = 0.8f + reverseThrustLevel * 0.4f;

            // Smoothly adjust volume and pitch
            reverseThrusterAudioSource.volume = Mathf.Lerp(reverseThrusterAudioSource.volume, targetVolume, Time.deltaTime * 5f);
            reverseThrusterAudioSource.pitch = Mathf.Lerp(reverseThrusterAudioSource.pitch, targetPitch, Time.deltaTime * 5f);
        }

        private void HandleVerticalThrusterAudio(float verticalThrustLevel)
        {
            if (verticalThrusterAudioSource == null) return;

            // Volume is directly proportional to the vertical thrust level
            float targetVolume = verticalThrustLevel;
            // Pitch increases slightly with more thrust
            float targetPitch = 0.8f + verticalThrustLevel * 0.4f;

            // Smoothly adjust volume and pitch
            verticalThrusterAudioSource.volume = Mathf.Lerp(verticalThrusterAudioSource.volume, targetVolume, Time.deltaTime * 5f);
            verticalThrusterAudioSource.pitch = Mathf.Lerp(verticalThrusterAudioSource.pitch, targetPitch, Time.deltaTime * 5f);
        }
        private void UpdateMovementFX(float speed)
        {
            // Wind SFX
            HandleWindAudio(speed);
            // Speed Lines VFX
            HandleSpeedLinesVFX(speed);
        }

        private void HandleWindAudio(float speed)
        {
            if (movementAudioSource == null) return;

            // Use the ship's max speed for normalization, with a fallback.
            // Note: The original code safely handles cases where shipController.Properties is null.
            float maxSpeed = 800f; // Default fallback
            // This property access is left as-is from the original script; it will gracefully fail to null
            // and use the 800f fallback, which is correct behavior given the provided stubs.
            if (shipController.Properties != null && shipController.Properties.maxSpeed > 0)
            {
                maxSpeed = shipController.Properties.maxSpeed;
            }

            float normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);

            if (normalizedSpeed > 0.05f)
            {
                // Volume and pitch increase with speed
                float targetVolume = normalizedSpeed;
                float targetPitch = 0.8f + normalizedSpeed * 0.5f;

                movementAudioSource.volume = Mathf.Lerp(movementAudioSource.volume, targetVolume, Time.deltaTime * 3f);
                movementAudioSource.pitch = Mathf.Lerp(movementAudioSource.pitch, targetPitch, Time.deltaTime * 3f);
            }
            else
            {
                // Fade out wind noise
                movementAudioSource.volume = Mathf.Lerp(movementAudioSource.volume, 0f, Time.deltaTime * 5f);
            }
        }

        private void HandleSpeedLinesVFX(float speed)
        {
            if (speedLinesVFX == null) return;

            var emission = speedLinesVFX.emission;

            if (speed > speedLinesActivationThreshold)
            {
                if (!speedLinesVFX.isPlaying)
                {
                    speedLinesVFX.Play();
                }

                if (defaultEmissionRates.TryGetValue(speedLinesVFX, out float defaultRate))
                {
                    // Modulate emission rate based on speed past the threshold
                    float maxSpeed = 800f; // Default fallback
                    if (shipController.Properties != null)
                    {
                        maxSpeed = shipController.Properties.maxSpeed;
                    }

                    float speedRatio = Mathf.Clamp01((speed - speedLinesActivationThreshold) / (maxSpeed - speedLinesActivationThreshold));
                    emission.rateOverTime = defaultRate * speedRatio;
                }
            }
            else
            {
                if (speedLinesVFX.isEmitting)
                {
                    emission.rateOverTime = 0;
                }
            }
        }

        #endregion

        #region Event Handlers (One-Shot Effects)

        private void HandleGForceUpdate(float gForce)
        {
            if (!isAlive) return;
            if (gForce > minGForceForStressSound && Time.time - lastGForceSoundTime > gForceSoundCooldown)
            {
                if (gForceStressClips != null && gForceStressClips.Count > 0 && oneShotAudioSource != null)
                {
                    // Play a random stress sound
                    AudioClip clip = gForceStressClips[UnityEngine.Random.Range(0, gForceStressClips.Count)];
                    if (clip != null)
                    {
                        // Volume based on how much over the limit the G-force is (e.g., normalized over 5G excess)
                        float volume = Mathf.Clamp01(0.5f + (gForce - minGForceForStressSound) / 5.0f);
                        oneShotAudioSource.PlayOneShot(clip, volume);
                        lastGForceSoundTime = Time.time;
                    }
                }
            }
        }

        private void HandlePrimaryFired(Transform firePoint)
        {
            if (!isAlive) return;
            PlayOneShotSFX(primaryFireClip);
            SpawnVFXPrefab(primaryMuzzleFlashPrefab, firePoint.position, firePoint.rotation);
        }

        private void HandleSecondaryFired(Transform firePoint)
        {
            if (!isAlive) return;
            PlayOneShotSFX(secondaryFireClip);
            SpawnVFXPrefab(secondaryMuzzleFlashPrefab, firePoint.position, firePoint.rotation);
        }

        private void HandleBombDropped(Transform dropPoint)
        {
            if (!isAlive) return;
            PlayOneShotSFX(bombDropClip);
        }

        // REMOVED: HandleImpact, HandleShieldsBroken, HandleShieldsRecharged, HandleDeath.

        private void HandleProximityAlert()
        {
            if (!isAlive) return;
            PlayOneShotSFX(proximityAlertClip);
        }

        #endregion

        /// <summary>
        /// Immediately stops all continuous VFX and SFX. Used upon death detection and OnDisable.
        /// </summary>
        private void StopAllContinuousFX()
        {
            // Stop VFX by setting thrust levels to 0
            SetThrusterVFXState(mainThrustersVFX, 0f);
            SetThrusterVFXState(reverseThrustersVFX, 0f);
            SetThrusterVFXState(verticalThrustersVFX, 0f);
            SetBoostVFXActive(false);
            if (speedLinesVFX != null && speedLinesVFX.isEmitting)
            {
                var emission = speedLinesVFX.emission;
                emission.rateOverTime = 0;
            }

            // Stop SFX immediately
            StopLoopingAudioSource(engineAudioSource);
            StopLoopingAudioSource(boostAudioSource);
            StopLoopingAudioSource(movementAudioSource);
            StopLoopingAudioSource(reverseThrusterAudioSource);
            StopLoopingAudioSource(verticalThrusterAudioSource);
        }

        private void ResetFXState()
        {
            // Mark as alive so the Update loop can run.
            isAlive = true;
            wasAliveLastFrame = true; // Reset transition state

            // Reset timers.
            lastGForceSoundTime = -10f; // Reset G-force sound cooldown.

            // Ensure audio sources start silent or at default state.
            ResetAudioSourceState(engineAudioSource);
            ResetAudioSourceState(boostAudioSource);
            ResetAudioSourceState(movementAudioSource);
            ResetAudioSourceState(reverseThrusterAudioSource);
            ResetAudioSourceState(verticalThrusterAudioSource);
        }

        private void ResetAudioSourceState(AudioSource source)
        {
            if (source != null)
            {
                source.volume = 0f; // Start silent
                source.pitch = 1f; // Reset pitch to default
            }
        }

        private void StopLoopingAudioSource(AudioSource source)
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
            }
        }

        #region Utility Methods

        // Utility method to play a VFX system attached to this GameObject.
        private void PlayVFX(ParticleSystem vfxSystem)
        {
            if (vfxSystem != null)
            {
                vfxSystem.Play();
            }
        }

        // Utility method to instantiate a VFX prefab.
        private void SpawnVFXPrefab(ParticleSystem prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab != null)
            {
                // In a production environment, this should use an Object Pool.
                Instantiate(prefab, position, rotation);
                // We assume the prefab handles its own playback and destruction (e.g. Stop Action = Destroy/Disable)
            }
        }

        // Utility method to play a one-shot audio clip on the primary one-shot source.
        private void PlayOneShotSFX(AudioClip clip)
        {
            // Ensures robustness if the clip or the audio source is missing.
            if (clip != null && oneShotAudioSource != null)
            {
                oneShotAudioSource.PlayOneShot(clip);
            }
        }

        #endregion
    }
}