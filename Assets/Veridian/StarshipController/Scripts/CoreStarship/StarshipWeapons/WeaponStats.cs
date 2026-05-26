using UnityEngine;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// Defines the category of the weapon system.
    /// </summary>
    public enum WeaponType
    {
        Laser,
        Rocket,
        Bomb
    }

    /// <summary>
    /// ScriptableObject defining the configuration and statistics for a specific weapon system.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponStats", menuName = "Veridian/Starship/Weapon Stats")]
    public class WeaponStats : ScriptableObject
    {
        [Header("General")]
        [Tooltip("The display name of the weapon.")]
        public string weaponName = "Generic Weapon";
        [Tooltip("The category of the weapon, which determines its firing behavior.")]
        public WeaponType type = WeaponType.Laser;
        [Tooltip("The prefab instantiated when this weapon is fired or dropped.")]
        public GameObject projectilePrefab;

        [Header("Firing Mechanics")]
        [Tooltip("The minimum time (in seconds) required between consecutive shots (Cooldown/Rate of Fire).")]
        public float refireRate = 0.5f;
        [Tooltip("The maximum ammunition capacity. Set to 0 for infinite ammo.")]
        public int maxAmmo = 0;

        [Header("Laser Specific (Accuracy)")]
        [Tooltip("The maximum effective range (in meters) of the laser.")]
        public float maxRange = 1000f;
        [Tooltip("The distance (as a fraction of Max Range, 0.0 to 1.0) at which accuracy starts to decrease. 1.0 means perfect accuracy up to the maximum range.")]
        [Range(0f, 1f)]
        public float accuracyFalloffStart = 0.8f;
        [Tooltip("The minimum accuracy (0.0 to 1.0) when firing at the maximum range. Lower values result in greater spread.")]
        [Range(0f, 1f)]
        public float minAccuracy = 0.5f;

        [Header("Rocket/Projectile Specific")]
        [Tooltip("The speed of the projectile (m/s). Used primarily by AI for lead calculations. This should match the projectile's actual speed (e.g., RocketPropulsion max speed or Laser speed).")]
        public float projectileSpeed = 200f;

        [Header("Guided Weapon Settings (Rockets Only)")]
        [Tooltip("If true, this weapon utilizes guidance logic and requires the driver to be actively engaged with a target (must be WeaponType.Rocket).")]
        public bool isGuided = false;
        [Tooltip("The maximum angle (in degrees) off the ship's forward axis (boresight) where a lock can be maintained and the weapon fired.")]
        public float maxLockAngle = 30f;
        [Tooltip("The maximum distance (in meters) for maintaining a lock and firing the guided weapon.")]
        public float maxLockDistance = 1500f;
    }
}