using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Veridian.Starship.Weapons;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// A UnityEvent specialization for ship destruction events, allowing the StarshipIdentity to be passed in the Inspector.
    /// </summary>
    [System.Serializable]
    public class ShipDestroyedEvent : UnityEvent<StarshipIdentity> { }

    /// <summary>
    /// Manages health, shields, damage intake, regeneration, and death events for starships and other destructible objects.
    /// Implements the IHealthProvider interface and serves as the authority for impact/destruction FX.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthComponent : MonoBehaviour, IHealthProvider
    {
        //--------------------------------------------------------------------------------
        #region Fields and Properties
        //--------------------------------------------------------------------------------

        [Header("--- Core Configuration ---")]
        [SerializeField, Tooltip("The maximum amount of hull integrity (health). If 0, the object is invincible.")]
        private float maxHealth = 100f;
        [SerializeField, Tooltip("The maximum capacity of the energy shields. If 0, the object has no shields.")]
        private float maxShields = 100f;

        [Header("--- Player Targeting ---")]
        [Tooltip("An optional child GameObject (e.g., a targeting bracket UI element) to activate when this object is acquired as a target by the player. Managed by the PlayerShipDriver.")]
        public GameObject playerTargetVisualizer;

        [Header("Shield Regeneration")]
        [SerializeField, Tooltip("The time in seconds that must pass after taking damage before shields begin to recharge.")]
        private float shieldRegenDelay = 1f;
        [SerializeField, Tooltip("The rate at which shields recharge per second once regeneration begins.")]
        private float shieldRegenRate = 5f;

        [Header("Lifecycle Management")]
        [SerializeField, Tooltip("If true AND no StarshipIdentity is present (Simple Mode), this GameObject will disable itself upon death. If a StarshipIdentity is present (Starship Mode), lifecycle is managed externally by the FactionManager.")]
        private bool disableGameObjectOnDeath = true;

        // --- Public Properties ---
        public float CurrentHealth { get; private set; }
        public float CurrentShields { get; private set; }
        public float MaxHealth => maxHealth;
        public float MaxShields => maxShields;

        public bool HasHealthCapability => maxHealth > 0;
        public bool HasShieldCapability => maxShields > 0;
        // An object is considered alive if it cannot take damage (maxHealth <= 0) or if it has health remaining.
        public bool IsAlive => !HasHealthCapability || CurrentHealth > 0f;

        public float CurrentHealthNormalized
        {
            get
            {
                if (!HasHealthCapability) return 0f;
                return Mathf.Clamp01(CurrentHealth / maxHealth);
            }
        }

        public float CurrentShieldsNormalized
        {
            get
            {
                if (!HasShieldCapability) return 0f;
                return Mathf.Clamp01(CurrentShields / maxShields);
            }
        }

        public bool AreShieldsDown => CurrentShields <= 0f;

        // --- Private Runtime Fields ---
        private float timeSinceLastDamage;
        private bool shieldsWereDown = false;
        private StarshipIdentity _identity; // Cached identity (null in Simple Mode)
        private ShipWeaponController _weaponController; // Optional weapon controller

        // Destruction sequence tracking (NEW)
        private List<Renderer> _renderersDisabledDuringDestruction = new();
        private List<Collider> _collidersDisabledDuringDestruction = new();
        private Coroutine _destructionCoroutine = null;

        #endregion

        //--------------------------------------------------------------------------------
        #region Events
        //--------------------------------------------------------------------------------

        [Header("--- Events ---")]
        // General Unity Events
        [Tooltip("Invoked when any damage is taken (shield or hull).")]
        public UnityEvent OnDamageTaken;
        [Tooltip("Invoked when shields are fully recharged after having been previously depleted.")]
        public UnityEvent OnShieldsRecharged;
        [Tooltip("Invoked when shields are depleted (reach zero).")]
        public UnityEvent OnShieldsBroken;

        // Starship Mode Events
        [Tooltip("Invoked upon death only when a StarshipIdentity is present (Starship Mode). Passes the identity of the destroyed ship.")]
        public ShipDestroyedEvent OnDeath;

        // Simple Mode Events
        [Tooltip("Invoked upon death only when no StarshipIdentity is present (Simple Mode).")]
        public UnityEvent OnDeathSimple;

        // General C# Events
        public event Action<bool> OnImpact; // Parameter: bool wasShieldHit
        public event Action OnShieldsBrokenEvent;
        public event Action OnShieldsRechargedEvent;

        // Starship Mode C# Events
        public event Action<StarshipIdentity> OnDeathEvent;

        // Simple Mode C# Events
        public event Action OnDeathEventSimple;

        // Global event for ship destruction
        public static event Action<StarshipIdentity, StarshipIdentity> OnShipDestroyedGlobal;

        #endregion

        //--------------------------------------------------------------------------------
        #region Internal FX System
        //--------------------------------------------------------------------------------

        [Header("--- Internal FX (Authoritative Feedback) ---")]
        [SerializeField, Tooltip("Forces the internal FX system off. Use this if FX should be entirely suppressed for this object.")]
        private bool forceDisableInternalFX = false;

        [Tooltip("The time (in seconds) to wait after destruction effects start before disabling the GameObject. This ensures effects can complete.")]
        public float destructionDelay = 3.0f; // NEW FIELD

        [Header("Internal Audio (Optional)")]
        [SerializeField, Tooltip("Sound clip played when shields are impacted.")]
        private AudioClip shieldImpactSound;
        [SerializeField, Tooltip("Sound clip played when shields are broken.")]
        private AudioClip shieldBreakSound; // NEW FIELD
        [SerializeField, Tooltip("Sound clip played when the hull is impacted (shields are down).")]
        private AudioClip hullImpactSound;
        [SerializeField, Tooltip("Sound clip played upon death.")]
        private AudioClip deathSound;

        [Header("Internal VFX (Optional)")]
        // Note: These ParticleSystems should typically be children of this GameObject and configured with PlayOnAwake=false.
        [SerializeField, Tooltip("Particle effect played when shields are impacted.")]
        private ParticleSystem shieldImpactVFX;
        [SerializeField, Tooltip("Particle effect played when shields are broken.")]
        private ParticleSystem shieldBreakVFX; // NEW FIELD
        [SerializeField, Tooltip("Particle effect played when the hull is impacted.")]
        private ParticleSystem hullImpactVFX;
        [SerializeField, Tooltip("Particle effect played upon death.")]
        private ParticleSystem deathExplosionVFX;

        // Internal FX Runtime
        private AudioSource _internalAudioSource;
        private bool _shouldUseInternalFX = false;

        private void InitializeInternalFX()
        {
            if (forceDisableInternalFX)
            {
                _shouldUseInternalFX = false;
                return;
            }

            // 1. HealthComponent is now the authority. We do not defer to StarshipFXController.
            _shouldUseInternalFX = true;

            // 2. Manage AudioSource
            // Check if any audio clips are assigned before setting up the AudioSource
            bool requiresAudio = shieldImpactSound != null || hullImpactSound != null || deathSound != null || shieldBreakSound != null;

            if (requiresAudio)
            {
                // Attempt to find an existing AudioSource
                if (!TryGetComponent(out _internalAudioSource))
                {
                    // Add one dynamically if needed and none exists
                    _internalAudioSource = gameObject.AddComponent<AudioSource>();
                    _internalAudioSource.playOnAwake = false;
                    // Configure for typical 3D in-world object sound
                    _internalAudioSource.spatialBlend = 1.0f;
                }
            }
        }

        private void PlayInternalSound(AudioClip clip)
        {
            if (_internalAudioSource != null && clip != null)
            {
                _internalAudioSource.PlayOneShot(clip);
            }
        }

        private void PlayInternalVFX(ParticleSystem vfx)
        {
            if (vfx != null)
            {
                vfx.Play();
            }
        }

        #endregion

        //--------------------------------------------------------------------------------
        #region Unity Lifecycle
        //--------------------------------------------------------------------------------

        void Awake()
        {
            // 1. Determine Operational Mode
            if (TryGetComponent(out _identity))
            {
                TryGetComponent(out _weaponController);
            }

            // 2. Initialize Internal FX System
            InitializeInternalFX();

            // 3. Initial setup (Health/Shields reset)
            Initialize();

            // Ensure the target visualizer is off by default.
            if (playerTargetVisualizer != null)
            {
                playerTargetVisualizer.SetActive(false);
            }
        }

        void Update()
        {
            if (!IsAlive) return;

            HandleShieldRegeneration();
        }

        // OnValidate for configuration checks
        private void OnValidate()
        {
            // Sanitize input values
            maxHealth = Mathf.Max(0, maxHealth);
            maxShields = Mathf.Max(0, maxShields);
            destructionDelay = Mathf.Max(0.1f, destructionDelay);
        }

        #endregion

        //--------------------------------------------------------------------------------
        #region Core Logic
        //--------------------------------------------------------------------------------

        /// <summary>
        /// Resets health and shields to maximum capacity, restores visibility/collision, and optionally resets ammunition. Used during initialization and respawning.
        /// </summary>
        public void Initialize()
        {
            // 0. Stop any ongoing destruction sequence (e.g., forced respawn before delay finishes)
            if (_destructionCoroutine != null)
            {
                StopCoroutine(_destructionCoroutine);
                _destructionCoroutine = null;
            }

            // Restore components disabled during the previous life cycle. (Robust Restoration)
            foreach (var renderer in _renderersDisabledDuringDestruction)
            {
                if (renderer != null) renderer.enabled = true;
            }
            _renderersDisabledDuringDestruction.Clear();

            foreach (var collider in _collidersDisabledDuringDestruction)
            {
                if (collider != null) collider.enabled = true;
            }
            _collidersDisabledDuringDestruction.Clear();


            // 1. Reset Health and Shields
            CurrentHealth = maxHealth;
            CurrentShields = maxShields;
            timeSinceLastDamage = shieldRegenDelay; // Start ready to regen
            shieldsWereDown = HasShieldCapability && AreShieldsDown;

            // 2. Update Identity Status and Handle Weapon Reset (Starship Mode only)
            if (_identity != null)
            {
                _identity.SetAliveStatus(true);

                // Check the flag on the identity and reset weapons if required (e.g. upon respawn).
                if (_identity.ResetWeaponsOnRespawn && _weaponController != null)
                {
                    _weaponController.ResetAmmunition();
                }
            }
        }

        private void HandleShieldRegeneration()
        {
            if (!HasShieldCapability) return;

            timeSinceLastDamage += Time.deltaTime;

            if (timeSinceLastDamage >= shieldRegenDelay && CurrentShields < maxShields)
            {
                CurrentShields += shieldRegenRate * Time.deltaTime;
                CurrentShields = Mathf.Clamp(CurrentShields, 0f, maxShields);

                // Check if shields just fully recharged after being broken
                if (shieldsWereDown && CurrentShields >= maxShields)
                {
                    shieldsWereDown = false;
                    OnShieldsRecharged?.Invoke();
                    OnShieldsRechargedEvent?.Invoke();
                    // Explicitly no shield recharge sound.
                }
            }
        }

        /// <summary>
        /// Applies damage to the object, prioritizing shields first, then health.
        /// </summary>
        /// <param name="amount">The amount of damage to apply.</param>
        /// <param name="attacker">The identity of the attacker (optional, used for scorekeeping in Starship Mode).</param>
        public void ApplyDamage(float amount, StarshipIdentity attacker = null)
        {
            if (!IsAlive || amount <= 0) return;

            timeSinceLastDamage = 0f;
            float damageRemaining = amount;
            bool wasShieldHit = false;
            bool wasHullHit = false;
            bool shieldsBrokenThisHit = false; // Track if shields broke specifically in this application

            // 1. Apply damage to shields
            if (HasShieldCapability && CurrentShields > 0)
            {
                wasShieldHit = true;
                if (damageRemaining >= CurrentShields)
                {
                    // Shields broken
                    damageRemaining -= CurrentShields;
                    CurrentShields = 0f;
                    if (!shieldsWereDown)
                    {
                        shieldsWereDown = true;
                        shieldsBrokenThisHit = true;
                        OnShieldsBroken?.Invoke();
                        OnShieldsBrokenEvent?.Invoke();

                        // Trigger Internal Shield Break FX
                        if (_shouldUseInternalFX)
                        {
                            PlayInternalSound(shieldBreakSound);
                            PlayInternalVFX(shieldBreakVFX);
                        }
                    }
                }
                else
                {
                    // Shields damaged but still up
                    CurrentShields -= damageRemaining;
                    damageRemaining = 0f;
                }
            }

            // 2. Apply spillover damage to health
            if (HasHealthCapability && damageRemaining > 0)
            {
                wasHullHit = true;
                CurrentHealth -= damageRemaining;
                CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
            }

            // 3. Trigger Feedback (Events)
            OnDamageTaken?.Invoke();
            OnImpact?.Invoke(wasShieldHit);

            // 4. Trigger Internal Impact FX
            if (_shouldUseInternalFX)
            {
                if (wasHullHit)
                {
                    // Always play hull impact if the hull took damage (even spillover).
                    PlayInternalSound(hullImpactSound);
                    PlayInternalVFX(hullImpactVFX);
                }
                else if (wasShieldHit && !shieldsBrokenThisHit)
                {
                    // Only play standard shield impact if shields were hit BUT NOT broken this frame.
                    // If they were broken, the shieldBreakVFX/SFX is sufficient feedback.
                    PlayInternalSound(shieldImpactSound);
                    PlayInternalVFX(shieldImpactVFX);
                }
            }

            // 5. Check for death
            if (!IsAlive)
            {
                Die(attacker);
            }
        }

        private void Die(StarshipIdentity killer)
        {
            // 1. Turn off the playerTargetVisualizer.
            if (playerTargetVisualizer != null)
            {
                playerTargetVisualizer.SetActive(false);
            }

            // 2. Fire all destruction events FIRST.
            // This is critical so the FactionManager (if present) gets the event and starts its respawn timer.

            if (_identity != null)
            {
                // "Managed Mode" (Starship)
                _identity.SetAliveStatus(false);
                OnDeath?.Invoke(_identity);
                OnDeathEvent?.Invoke(_identity);
                OnShipDestroyedGlobal?.Invoke(_identity, killer);
            }
            else
            {
                // "Simple Mode" (e.g., Asteroid, Turret)
                OnDeathSimple?.Invoke();
                OnDeathEventSimple?.Invoke();
            }

            // 3. Handle Destruction Sequence (Unified Flow)

            if (_shouldUseInternalFX)
            {
                // A. Trigger Death FX
                PlayInternalSound(deathSound);
                PlayInternalVFX(deathExplosionVFX);

                // B. Start Delayed Destruction Coroutine
                if (_destructionCoroutine != null)
                {
                    StopCoroutine(_destructionCoroutine);
                }
                // Ensure the GameObject is active to run the coroutine.
                if (gameObject.activeInHierarchy)
                {
                    _destructionCoroutine = StartCoroutine(DelayedDestructionCoroutine());
                }
            }
            else
            {
                // 4. Fallback (No Internal FX)

                // Simple Mode: Self-disable immediately if configured.
                if (_identity == null && disableGameObjectOnDeath)
                {
                    gameObject.SetActive(false);
                }

                // Managed Mode: Do nothing here. The object remains active (and visible) but 'dead'.
                // FactionManager will handle the respawn cycle.
            }
        }

        /// <summary>
        /// Coroutine to handle the delayed destruction process, ensuring effects can play out.
        /// </summary>
        private IEnumerator DelayedDestructionCoroutine()
        {
            // Clear tracking lists for the new sequence
            _renderersDisabledDuringDestruction.Clear();
            _collidersDisabledDuringDestruction.Clear();

            // Part 1: Hide. Immediately disable all relevant Renderers and Colliders on this GameObject and all its children.

            // Disable Renderers (MeshRenderer, SkinnedMeshRenderer, etc.)
            // We use GetComponentsInChildren<Renderer> to catch all types.
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Crucial: Skip ParticleSystemRenderers so the destruction VFX remains visible.
                if (renderer is ParticleSystemRenderer) continue;

                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    _renderersDisabledDuringDestruction.Add(renderer);
                }
            }

            // Disable Colliders
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                if (collider.enabled)
                {
                    collider.enabled = false;
                    _collidersDisabledDuringDestruction.Add(collider);
                }
            }

            // Part 2: Wait. Pause execution for the destructionDelay duration.
            yield return new WaitForSeconds(destructionDelay);

            // Part 3: Final Disable.
            // This cleans up the object and allows the FactionManager to find it in an "inactive" state.
            // We check !IsAlive in case a very fast respawn (Initialize) already occurred during the wait.
            if (!IsAlive)
            {
                gameObject.SetActive(false);
            }

            _destructionCoroutine = null;
        }

        #endregion
    }
    public interface IHealthProvider
    {
        float CurrentHealthNormalized { get; }
        float CurrentShieldsNormalized { get; }
        bool IsAlive { get; }
        bool HasHealthCapability { get; }
        bool HasShieldCapability { get; }

    }
}