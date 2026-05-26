using UnityEngine;

namespace Veridian.Starship.AI
{
    /// <summary>
    /// ScriptableObject defining the combat characteristics and parameters for the AiWeaponController.
    /// Determines how the AI handles accuracy, firing patterns, weapon usage, and general combat maneuvering style (used by BehaviorAttack).
    /// </summary>
    [CreateAssetMenu(fileName = "NewCombatPersonality", menuName = "Starship AI/Combat Personality")]
    public class CombatPersonalitySO : ScriptableObject
    {
        [Header("Accuracy and Targeting")]
        [Tooltip("The maximum angle (degrees) off the calculated aim point the AI is allowed to fire primary weapons. Defines the required precision for a firing solution.")]
        [Range(1f, 45f)]
        public float FiringConeAngle = 5.0f;

        [Tooltip("Scale of simulated aiming error. Represents meters of offset per 100m distance to the target. Higher values result in less accurate fire.")]
        [Range(0f, 10f)]
        public float ErrorScale = 2.0f;

        [Tooltip("Multiplier applied to the weapon's base max range. >1.0 means the AI engages from further away; <1.0 means closer engagement.")]
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
        [Tooltip("The probability (0.0-1.0) per second that the AI will fire a secondary weapon when available and conditions are met. E.g., 0.1 means a 10% chance per second.")]
        [Range(0f, 1f)]
        public float SecondaryFireRate = .2f;

        [Tooltip("The minimum time (in seconds) the AI must be continuously engaged with a target before it considers using secondary weapons.")]
        public float SecondaryWarmupDelay = 3.0f;

        [Header("Maneuvering Style (Used by BehaviorAttack)")]
        [Tooltip("How frequently the AI attempts evasive maneuvers (0.0-1.0). Higher values lead to more frequent evasion checks, especially when under fire (IsInDanger).")]
        [Range(0f, 1f)]
        public float Evasiveness = 0.3f;

        [Tooltip("The ideal combat distance (in meters) the AI will try to maintain during the 'Dogfighting' state. Determines if the AI prefers close-quarters or standoff combat.")]
        public float PreferredEngagementRange = 300f;

        [Tooltip("The tolerance (in meters) around the PreferredEngagementRange. The AI switches between 'Engaging' and 'Dogfighting' maneuvers when outside this buffer zone.")]
        public float EngagementRangeTolerance = 50f;
    }
}