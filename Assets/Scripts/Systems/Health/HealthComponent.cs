using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace StarWeaver.Systems
{
    public class HealthComponent : MonoBehaviour
    {
        [Header("Configuración de Vida y Escudos")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxShields = 100f;

        [Header("Regeneración de Escudos")]
        [SerializeField] private bool canRegenerateShields = true;
        [SerializeField] private float shieldRegenRate = 10f;
        [SerializeField] private float shieldRegenDelay = 3f;

        [Header("Eventos de Unidad (UnityEvents para Diseñadores)")]
        public UnityEvent OnDamageTaken;
        public UnityEvent OnShieldsDepleted;
        public UnityEvent OnDestroyed;

        // Propiedades públicas para consultar el estado
        public float CurrentHealth { get; private set; }
        public float CurrentShields { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public float HealthPercent => maxHealth > 0 ? CurrentHealth / maxHealth : 0f;
        public float ShieldPercent => maxShields > 0 ? CurrentShields / maxShields : 0f;

        private float _nextShieldRegenTime;

        void Awake()
        {
            CurrentHealth = maxHealth;
            CurrentShields = maxShields;
        }

        void Update()
        {
            if (canRegenerateShields && IsAlive)
            {
                HandleShieldRegeneration();
            }
        }

        /// <summary>
        /// Aplica daño a la entidad. El escudo absorberá el impacto primero.
        /// </summary>
        public void TakeDamage(float damageAmount)
        {
            if (!IsAlive || damageAmount <= 0f) return;

            _nextShieldRegenTime = Time.time + shieldRegenDelay; // Reiniciar delay de regeneración
            OnDamageTaken?.Invoke();

            // 1. Aplicar daño al Escudo primero
            if (CurrentShields > 0f)
            {
                CurrentShields -= damageAmount;
                if (CurrentShields <= 0f)
                {
                    damageAmount = Mathf.Abs(CurrentShields); // El daño remanente pasa al casco
                    CurrentShields = 0f;
                    OnShieldsDepleted?.Invoke();
                }
                else
                {
                    damageAmount = 0f; // Escudo absorbió todo
                }
            }

            // 2. Aplicar daño restante a la Vida
            if (damageAmount > 0f)
            {
                CurrentHealth -= damageAmount;
                if (CurrentHealth <= 0f)
                {
                    CurrentHealth = 0f;
                    Die();
                }
            }
        }

        private void HandleShieldRegeneration()
        {
            if (Time.time < _nextShieldRegenTime || CurrentShields >= maxShields) return;

            CurrentShields += shieldRegenRate * Time.deltaTime;
            CurrentShields = Mathf.Clamp(CurrentShields, 0f, maxShields);
        }

        private void Die()
        {
            OnDestroyed?.Invoke();
            Debug.Log($"{gameObject.name} ha sido destruido en el espacio.");

            // Comportamiento de muerte básico: desactivamos el objeto. 
            // Más adelante acá meterás tu script de explosiones o de desove de asteroides.
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Cura o repara el casco de la nave.
        /// </summary>
        public void Repair(float amount)
        {
            if (!IsAlive) return;
            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
        }
    }
}