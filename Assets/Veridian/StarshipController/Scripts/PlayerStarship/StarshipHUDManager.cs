using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Veridian.Starship.Core;
using Veridian.Starship.Weapons;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// Manages the Heads-Up Display (HUD) for the player's starship, displaying critical flight data, weapon status, and targeting information.
    /// </summary>
    public class StarshipHUDManager : MonoBehaviour
    {
        [Header("Target Ship (Auto-Detected)")]
        [Tooltip("The AtmosphericStarshipController instance being monitored by the HUD. This is typically auto-detected via the PlayerShipDriver.")]
        public AtmosphericStarshipController targetShip;
        private ShipSensorySystem sensorySystem;
        private HealthComponent healthComponent;
        private ShipWeaponController weaponController;
        private ShipProperties shipProperties;
        private PlayerShipDriver playerDriver;

        [Header("Targeting UI")]
        [Tooltip("Text element displayed when the player is actively engaging a target (RMB held and locked). E.g., 'ENGAGED'.")]
        public TMP_Text targetStatusText;

        // A reference to the main camera for screen calculations (if needed)
        private Camera playerCamera;

        [Header("Core HUD Elements")]
        [Tooltip("The CanvasGroup controlling the overall visibility of the HUD.")]
        public CanvasGroup hudCanvasGroup;
        [Tooltip("Text element displaying the current velocity (m/s).")]
        public TMP_Text velocityText;
        [Tooltip("The central, fixed crosshair image.")]
        public Image crosshairImage;
        [Tooltip("Text element displaying the remaining count of bombardment weapons (Bombs).")]
        public TMP_Text crateCountText;

        [Header("Thrust and Boost Gauges")]
        [Tooltip("Slider representing the main (forward/reverse) thrust level.")]
        public Slider thrustGaugeSlider;
        [Tooltip("The fill image for the thrust gauge, used for color changes.")]
        public Image thrustFillImage;
        [Tooltip("Slider representing the vertical thrust level.")]
        public Slider verticalThrustSlider;
        [Tooltip("The fill image for the vertical thrust gauge, used for color changes.")]
        public Image verticalFillImage;
        [Tooltip("Slider representing the remaining boost capacity.")]
        public Slider boostGaugeSlider;

        [Header("Flight Data Readouts")]
        [Tooltip("Text element displaying the current G-Force.")]
        public TMP_Text gForceText;
        [Tooltip("Text element displaying the altitude (absolute and AGL).")]
        public TMP_Text altitudeText;
        [Tooltip("Text element displayed when the sensory system detects an imminent collision.")]
        public TMP_Text proximityWarningText;

        [Header("Health and Weapons Status")]
        [Tooltip("Slider representing the current hull integrity/health.")]
        public Slider healthBarSlider;
        [Tooltip("Slider representing the current shield strength.")]
        public Slider shieldGaugeSlider;
        [Tooltip("Text element displaying the primary weapon ammo count.")]
        public TMP_Text primaryAmmoText;
        [Tooltip("Text element displaying the secondary weapon ammo count.")]
        public TMP_Text secondaryAmmoText;

        [Header("UI Color Configuration")]
        [Tooltip("Color used for the thrust gauge when moving forward.")]
        public Color forwardThrustColor = Color.green;
        [Tooltip("Color used for the thrust gauge when reversing.")]
        public Color reverseThrustColor = Color.red;
        [Tooltip("Color used for the vertical thrust gauge when moving up.")]
        public Color verticalUpColor = new(0.5f, 0.8f, 1f); // Light Blue
        [Tooltip("Color used for the vertical thrust gauge when moving down.")]
        public Color verticalDownColor = new(1f, 0.7f, 0.3f); // Orange
        [Tooltip("Color used for warnings (e.g., high G-Force, proximity alert).")]
        public Color warningColor = Color.red;
        [Tooltip("The default color for the G-Force text readout.")]
        public Color defaultGForceColor = Color.white;

        private void Start()
        {
            InitializeHUD();
        }

        // OnEnable/OnDisable for event subscription management
        void OnEnable()
        {
            // Re-initialize if Start failed to find the ship
            if (targetShip == null)
            {
                InitializeHUD();
            }
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// Initializes the HUD by finding the player's ship and setting up references and default states.
        /// </summary>
        private void InitializeHUD()
        {
            // 1. Prioritize finding the PlayerShipDriver as the "source of truth" for the player's ship.
            playerDriver = FindFirstObjectByType<PlayerShipDriver>();

            // 2. If no driver is found, the HUD has nothing to connect to.
            if (playerDriver == null)
            {
                Debug.LogWarning("StarshipHUDManager: No PlayerShipDriver found in the scene. HUD will be inactive.");
                SetHUDActive(false);
                return;
            }

            // 3. Reliably get the specific starship controller that the player is driving.
            targetShip = playerDriver.GetComponent<AtmosphericStarshipController>();
            if (targetShip == null)
            {
                Debug.LogError("StarshipHUDManager: PlayerShipDriver was found, but it's missing an AtmosphericStarshipController component.", playerDriver);
                SetHUDActive(false);
                return;
            }

            // Get component references from the target ship.
            sensorySystem = targetShip.SensorySystem;
            shipProperties = targetShip.Properties;
            healthComponent = targetShip.Health;
            weaponController = targetShip.Weapons;

            // Hide warning/status text by default
            if (proximityWarningText != null)
            {
                proximityWarningText.gameObject.SetActive(false);
            }
            if (targetStatusText != null)
            {
                targetStatusText.gameObject.SetActive(false);
            }

            // Initialize G-Force text color
            if (gForceText != null)
            {
                if (defaultGForceColor == Color.white || defaultGForceColor == Color.black)
                {
                    // Use the color assigned in the inspector if default colors are used.
                    defaultGForceColor = gForceText.color;
                }
                else
                {
                    gForceText.color = defaultGForceColor;
                }
            }

            // Initialize sliders
            if (thrustGaugeSlider != null) { thrustGaugeSlider.minValue = -1; thrustGaugeSlider.maxValue = 1; }
            if (verticalThrustSlider != null) { verticalThrustSlider.minValue = -1; verticalThrustSlider.maxValue = 1; }
            if (boostGaugeSlider != null) { boostGaugeSlider.minValue = 0; boostGaugeSlider.maxValue = 1; }
            if (healthBarSlider != null) { healthBarSlider.minValue = 0; healthBarSlider.maxValue = 1; }
            if (shieldGaugeSlider != null) { shieldGaugeSlider.minValue = 0; shieldGaugeSlider.maxValue = 1; }

            playerCamera = Camera.main; // Cache the main camera

            // Initialize crosshair visibility
            if (crosshairImage != null) crosshairImage.gameObject.SetActive(true);
        }

        /// <summary>
        /// Subscribes to necessary events from the target ship.
        /// </summary>
        private void SubscribeToEvents()
        {
            if (targetShip != null)
            {
                // Unsubscribe first to prevent double subscription
                targetShip.OnGForceUpdate -= UpdateGForceDisplay;
                targetShip.OnGForceUpdate += UpdateGForceDisplay;
            }
        }

        /// <summary>
        /// Unsubscribes from events when the HUD is disabled.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (targetShip != null)
            {
                targetShip.OnGForceUpdate -= UpdateGForceDisplay;
            }
        }

        void Update()
        {
            if (targetShip == null) return;

            // Check if the target ship is active in the scene.
            bool isActive = targetShip.enabled && targetShip.gameObject.activeInHierarchy;
            SetHUDActive(isActive);

            if (isActive)
            {
                UpdateVelocity();
                UpdateBoostGauge();
                UpdateThrustGauge();
                UpdateVerticalThrustGauge();
                UpdateCrateCounter(); // Updates Bomb Counter
                UpdateAltitudeDisplay();
                UpdateProximityWarning();
                UpdateHealthAndShields();
                UpdateWeaponStatus();
                UpdateTargetingUI();
            }
        }

        /// <summary>
        /// Updates the altitude display text (Absolute and Above Ground Level).
        /// </summary>
        private void UpdateAltitudeDisplay()
        {
            if (altitudeText == null || sensorySystem == null) return;

            if (!altitudeText.gameObject.activeSelf)
            {
                altitudeText.gameObject.SetActive(true);
            }

            float absoluteAltitude = targetShip.transform.position.y;
            float groundAltitude = sensorySystem.AltitudeAboveGround;
            altitudeText.text = $"ALT: {absoluteAltitude:F0} M\nAGL: {Mathf.RoundToInt(groundAltitude)} M";
        }

        /// <summary>
        /// Updates the visibility and content of the proximity warning text based on sensory system data.
        /// </summary>
        private void UpdateProximityWarning()
        {
            if (proximityWarningText == null || sensorySystem == null) return;

            if (sensorySystem.IsObstacleAhead)
            {
                if (!proximityWarningText.gameObject.activeSelf)
                {
                    proximityWarningText.gameObject.SetActive(true);
                }
                proximityWarningText.color = warningColor;
                string hint = sensorySystem.AvoidanceDirectionHint.ToString().ToUpper();
                proximityWarningText.text = $"PROXIMITY ALERT\nEVADE: {hint}";
            }
            else
            {
                if (proximityWarningText.gameObject.activeSelf)
                {
                    proximityWarningText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Updates the G-Force display text and changes its color based on the G-force intensity relative to the ship's limits.
        /// </summary>
        /// <param name="gForce">The current G-force value.</param>
        private void UpdateGForceDisplay(float gForce)
        {
            if (gForceText == null) return;

            gForceText.text = $"G-Force: {gForce:F1} G";

            // G-Force Warning Logic
            if (shipProperties != null && shipProperties.maxOverallGForce > 0)
            {
                float warningThreshold = shipProperties.maxOverallGForce * 0.8f; // 80% threshold

                if (gForce >= warningThreshold)
                {
                    gForceText.color = warningColor;
                }
                else
                {
                    gForceText.color = defaultGForceColor;
                }
            }
            else
            {
                // Ensure default color if properties are missing or max G-force is 0.
                if (gForceText.color != defaultGForceColor)
                {
                    gForceText.color = defaultGForceColor;
                }
            }
        }

        /// <summary>
        /// Updates the bomb counter display (referred to as Crate Counter in the method name).
        /// </summary>
        private void UpdateCrateCounter()
        {
            // First, check if the UI element and the weapon controller itself exist.
            if (crateCountText == null || weaponController == null || weaponController.bombardmentWeaponStats == null)
            {
                // If we don't have a bomb system, make sure the text is hidden.
                if (crateCountText != null)
                {
                    crateCountText.gameObject.SetActive(false);
                }
                return;
            }

            // A bomb system exists if the weapon has a max ammo greater than 0.
            bool hasBombSystem = weaponController.bombardmentWeaponStats.maxAmmo > 0;

            if (crateCountText.gameObject.activeSelf != hasBombSystem)
            {
                crateCountText.gameObject.SetActive(hasBombSystem);
            }

            if (hasBombSystem)
            {
                // Get the bomb count from the weapon controller.
                crateCountText.text = $"Bombs: {weaponController.CurrentBombAmmo}";
            }
        }

        /// <summary>
        /// Updates the health and shield bar sliders based on the HealthComponent data.
        /// </summary>
        private void UpdateHealthAndShields()
        {
            if (healthComponent == null) return;

            if (healthBarSlider != null)
            {
                // Using normalized properties for cleaner access
                healthBarSlider.value = healthComponent.CurrentHealthNormalized;
            }

            if (shieldGaugeSlider != null)
            {
                bool hasShields = healthComponent.HasShieldCapability;
                if (shieldGaugeSlider.gameObject.activeSelf != hasShields)
                {
                    shieldGaugeSlider.gameObject.SetActive(hasShields);
                }

                if (hasShields)
                {
                    shieldGaugeSlider.value = healthComponent.CurrentShieldsNormalized;
                }
            }
        }

        /// <summary>
        /// Updates the ammunition counters for primary and secondary weapons.
        /// </summary>
        private void UpdateWeaponStatus()
        {
            if (weaponController == null) return;

            if (primaryAmmoText != null)
            {
                UpdateAmmoText(primaryAmmoText, weaponController.primaryWeaponStats, weaponController.CurrentPrimaryAmmo);
            }

            if (secondaryAmmoText != null)
            {
                UpdateAmmoText(secondaryAmmoText, weaponController.secondaryWeaponStats, weaponController.CurrentSecondaryAmmo);
            }
        }

        /// <summary>
        /// Helper method to format and update an ammo text element.
        /// </summary>
        private void UpdateAmmoText(TMP_Text textElement, WeaponStats stats, int currentAmmo)
        {
            if (stats == null)
            {
                textElement.gameObject.SetActive(false);
                return;
            }

            if (!textElement.gameObject.activeSelf)
            {
                textElement.gameObject.SetActive(true);
            }

            string weaponName = stats.weaponName;
            if (stats.maxAmmo > 0)
            {
                // Finite ammo
                textElement.text = $"{weaponName}: {currentAmmo}";
            }
            else
            {
                // Infinite ammo
                textElement.text = $"{weaponName}: ∞";
            }
        }

        /// <summary>
        /// Updates the boost gauge slider based on the ship's current boost capacity.
        /// </summary>
        private void UpdateBoostGauge()
        {
            if (boostGaugeSlider == null) return;
            float maxBoost = targetShip.MaxBoost;
            bool hasBoost = maxBoost > 0;
            if (boostGaugeSlider.gameObject.activeSelf != hasBoost)
            {
                boostGaugeSlider.gameObject.SetActive(hasBoost);
            }
            if (hasBoost)
            {
                boostGaugeSlider.value = targetShip.CurrentBoost / maxBoost;
            }
        }

        /// <summary>
        /// Sets the overall visibility of the HUD using the CanvasGroup.
        /// </summary>
        /// <param name="active">Whether the HUD should be visible and interactive.</param>
        private void SetHUDActive(bool active)
        {
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = active ? 1f : 0f;
                hudCanvasGroup.interactable = active;
                hudCanvasGroup.blocksRaycasts = active;
            }
        }

        /// <summary>
        /// Updates the velocity display text.
        /// </summary>
        private void UpdateVelocity()
        {
            if (velocityText != null)
            {
                velocityText.text = $"Speed: {targetShip.CurrentSpeed:F1} m/s";
            }
        }

        /// <summary>
        /// Updates the main thrust gauge slider and its color based on the thrust direction and boost multiplier.
        /// </summary>
        private void UpdateThrustGauge()
        {
            if (thrustGaugeSlider == null) return;
            // Adjust max value based on current boost multiplier
            thrustGaugeSlider.maxValue = Mathf.Max(1f, targetShip.CurrentBoostMultiplier);
            thrustGaugeSlider.value = targetShip.CurrentThrustLevel * targetShip.CurrentBoostMultiplier;
            if (thrustFillImage != null)
            {
                thrustFillImage.color = targetShip.CurrentThrustLevel > 0.01f ? forwardThrustColor : reverseThrustColor;
            }
        }

        /// <summary>
        /// Updates the vertical thrust gauge slider and its color based on the thrust direction.
        /// </summary>
        private void UpdateVerticalThrustGauge()
        {
            if (verticalThrustSlider == null) return;
            verticalThrustSlider.value = targetShip.CurrentVerticalThrustLevel;
            if (verticalFillImage != null)
            {
                verticalFillImage.color = targetShip.CurrentVerticalThrustLevel > 0.01f ? verticalUpColor : verticalDownColor;
            }
        }

        /// <summary>
        /// Updates the targeting UI elements, specifically the crosshair visibility and the engagement status text.
        /// </summary>
        private void UpdateTargetingUI()
        {
            // Don't run if we don't have the necessary references
            if (playerDriver == null)
            {
                // Ensure targeting UI is hidden if driver is missing
                if (crosshairImage != null) crosshairImage.gameObject.SetActive(false);
                if (targetStatusText != null) targetStatusText.gameObject.SetActive(false);
                return;
            }

            // Read state from the player driver
            bool isEngaged = playerDriver.IsTargetEngaged;
            bool isFlying = playerDriver.IsActivelyControlling();

            // 1. Update Crosshair Visibility
            // The fixed crosshair is visible if the player is actively controlling the ship.
            if (crosshairImage != null)
            {
                crosshairImage.gameObject.SetActive(isFlying);
            }

            // 2. Update Engagement Status Text
            if (targetStatusText != null)
            {
                if (isEngaged)
                {
                    if (!targetStatusText.gameObject.activeSelf)
                    {
                        targetStatusText.gameObject.SetActive(true);
                    }
                    targetStatusText.text = "ENGAGED";
                }
                else
                {
                    if (targetStatusText.gameObject.activeSelf)
                    {
                        targetStatusText.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}