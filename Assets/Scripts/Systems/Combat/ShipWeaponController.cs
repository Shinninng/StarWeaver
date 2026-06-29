using System;
using System.Collections.Generic;
using UnityEngine;
using StarWeaver.Core; // Conectamos con el controlador orbital

namespace StarWeaver.Systems
{
    public class ShipWeaponController : MonoBehaviour
    {
        // Eventos para conectar efectos visuales y de sonido de forma desacoplada
        public event Action<Transform> OnPrimaryFired;
        public event Action<Transform> OnSecondaryFired;

        [Header("Configuraciones de Armas")]
        public WeaponStats primaryWeaponStats;
        public WeaponStats secondaryWeaponStats;

        [Header("Puntos de Disparo (Muzzle Transforms)")]
        [Tooltip("Lista de cañones para el arma primaria. Si hay más de uno, disparará alternadamente.")]
        public List<Transform> primaryFirePoints = new List<Transform>();
        [Tooltip("Lista de cañones para el arma secundaria.")]
        public List<Transform> secondaryFirePoints = new List<Transform>();

        private int _primaryFirePointIndex = 0;
        private int _secondaryFirePointIndex = 0;

        private float _nextPrimaryFireTime;
        private float _nextSecondaryFireTime;

        private OrbitalStarshipController _shipController;

        void Awake()
        {
            _shipController = GetComponent<OrbitalStarshipController>();
        }

        void Update()
        {
            // Aquí podríamos agregar lógica de recarga o enfriamiento continuo si tus sistemas lo requieren a futuro
        }

        /// <summary>
        /// Intenta disparar el arma primaria hacia una posición en el espacio.
        /// </summary>
        public void FirePrimary(Vector3 aimPosition)
        {
            if (primaryWeaponStats == null || Time.time < _nextPrimaryFireTime) return;

            Transform firePoint = GetNextFirePoint(primaryFirePoints, ref _primaryFirePointIndex);
            if (firePoint == null) firePoint = this.transform; // Fallback al centro de la nave

            ExecuteShot(primaryWeaponStats, firePoint, aimPosition);

            _nextPrimaryFireTime = Time.time + primaryWeaponStats.fireRate;
            OnPrimaryFired?.Invoke(firePoint);
        }

        /// <summary>
        /// Intenta disparar el arma secundaria.
        /// </summary>
        public void FireSecondary(Vector3 aimPosition)
        {
            if (secondaryWeaponStats == null || Time.time < _nextSecondaryFireTime) return;

            Transform firePoint = GetNextFirePoint(secondaryFirePoints, ref _secondaryFirePointIndex);
            if (firePoint == null) firePoint = this.transform;

            ExecuteShot(secondaryWeaponStats, firePoint, aimPosition);

            _nextSecondaryFireTime = Time.time + secondaryWeaponStats.fireRate;
            OnSecondaryFired?.Invoke(firePoint);
        }

        private void ExecuteShot(WeaponStats stats, Transform firePoint, Vector3 aimPosition)
        {
            if (stats.projectilePrefab == null) return;

            // Calcular dirección básica
            Vector3 targetDirection = (aimPosition - firePoint.position).normalized;
            if (targetDirection == Vector3.zero) targetDirection = firePoint.forward;

            // Aplicar el modelo de precisión/dispersión basado en la distancia
            targetDirection = ApplyAccuracyModel(targetDirection, firePoint.position, aimPosition, stats);

            // Instanciar Proyectil
            GameObject projectileGo = Instantiate(stats.projectilePrefab, firePoint.position, Quaternion.LookRotation(targetDirection));

            // Si el proyectil usa físicas, le aplicamos la velocidad base + la inercia actual de la nave para que sea realista en el espacio
            Rigidbody projRb = projectileGo.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                Vector3 shipVelocity = Vector3.zero;
                Rigidbody shipRb = GetComponent<Rigidbody>();
                if (shipRb != null) shipVelocity = shipRb.linearVelocity;

                projRb.linearVelocity = shipVelocity + (targetDirection * stats.projectileSpeed);
            }

            // Instanciar Fogonazo (Muzzle Flash) si existe
            if (stats.muzzleFlashPrefab != null)
            {
                Destroy(Instantiate(stats.muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint), 2f);
            }
        }

        private Transform GetNextFirePoint(List<Transform> firePoints, ref int index)
        {
            if (firePoints == null || firePoints.Count == 0) return null;
            Transform point = firePoints[index];
            index = (index + 1) % firePoints.Count; // Cicla entre los cañones
            return point;
        }

        private Vector3 ApplyAccuracyModel(Vector3 direction, Vector3 origin, Vector3 target, WeaponStats stats)
        {
            // Modelo de dispersión nativo simplificado para StarWeaver
            float distance = Vector3.Distance(origin, target);

            // Si el objetivo está muy lejos del rango del arma, se vuelve impreciso
            if (distance > stats.maxRange * 0.5f)
            {
                float deviationAngle = UnityEngine.Random.Range(-3f, 3f);
                return Quaternion.AngleAxis(deviationAngle, Vector3.up) * direction;
            }

            return direction;
        }
    }
}