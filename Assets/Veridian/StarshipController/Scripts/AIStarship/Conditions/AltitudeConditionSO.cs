using UnityEngine;


namespace Veridian.Starship.AI
{
    [CreateAssetMenu(fileName = "Condition_Altitude", menuName = "Starship AI/Conditions/Altitude Check")]
    public class AltitudeConditionSO : AiConditionSO
    {
        [Header("Detection Zone Boundaries")]
        [Tooltip("The minimum allowed altitude above ground. If the ship flies lower than this, the condition returns true.")]
        public float detectionMinimumAltitude = 100f;

        [Tooltip("The maximum allowed altitude above ground. If the ship flies higher than this, the condition returns true.")]
        public float detectionMaximumAltitude = 5000f;

        protected override bool CheckCondition(AiBrain brain)
        {
            // Access the SensorySystem cached in the AiBrain.
            if (brain.SensorySystem == null || !brain.SensorySystem.enabled)
            {
                // If sensors are unavailable, we cannot determine the altitude. Assume safe (false).
                return false;
            }

            // Crucially, access the property as required.
            float currentAltitude = brain.SensorySystem.AltitudeAboveGround;

            // Check if too low.
            if (currentAltitude < detectionMinimumAltitude)
            {
                return true;
            }

            // Check if too high. We ensure the sensor reading is valid (less than max range)
            // before concluding we are too high, preventing false positives if the sensor returns its max value when far above terrain.
            if (currentAltitude > detectionMaximumAltitude && currentAltitude < brain.SensorySystem.maxGroundCheckDistance)
            {
                return true;
            }

            return false;
        }
    }
}