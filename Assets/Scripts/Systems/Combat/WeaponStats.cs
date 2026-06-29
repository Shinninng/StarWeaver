using UnityEngine;

namespace StarWeaver.Systems
{
    [System.Serializable]
    public enum WeaponFiringMode
    {
        Single,
        Burst,
        Automatic
    }

    [System.Serializable]
    public enum WeaponType
    {
        Energy,
        Kinetic,
        Missile,
        Bomb
    }

    [CreateAssetMenu(fileName = "NewWeaponStats", menuName = "StarWeaver/Combat/Weapon Stats")]
    public class WeaponStats : ScriptableObject
    {
        [Header("Identificación del Arma")]
        public string weaponName = "Láser de Plasma";
        public WeaponType weaponType = WeaponType.Energy;
        public WeaponFiringMode firingMode = WeaponFiringMode.Automatic;

        [Header("Parámetros de Disparo")]
        [Tooltip("Daño por impacto directo.")]
        public float damage = 15f;

        [Tooltip("Velocidad de salida del proyectil (m/s).")]
        public float projectileSpeed = 600f;

        [Tooltip("Rango máximo que puede viajar el proyectil antes de destruirse (metros).")]
        public float maxRange = 1200f;

        [Tooltip("Tiempo en segundos entre disparos.")]
        public float fireRate = 0.15f;

        [Header("Configuración de Ráfaga (Burst)")]
        [Tooltip("Cantidad de proyectiles disparados por ráfaga (si aplica).")]
        public int burstCount = 3;
        [Tooltip("Tiempo entre cada proyectil dentro de la misma ráfaga.")]
        public float burstDelay = 0.05f;

        [Header("Consumo de Energía / Munición")]
        [Tooltip("¿El arma consume energía de la nave al disparar?")]
        public bool consumesEnergy = true;
        public float energyCostPerShot = 5f;

        [Header("Visuales y Audio")]
        [Tooltip("El Prefab del proyectil que se va a instanciar (debe tener un Rigidbody o script de movimiento).")]
        public GameObject projectilePrefab;

        [Tooltip("Efecto de partículas para el fogonazo del cañón (Muzzle Flash).")]
        public GameObject muzzleFlashPrefab;

        [Tooltip("Efecto de sonido al disparar.")]
        public AudioClip fireSound;
    }
}