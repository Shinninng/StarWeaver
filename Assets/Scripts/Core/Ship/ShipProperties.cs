using UnityEngine;

namespace StarWeaver.Core
{
    /// <summary>
    /// Mantiene las características de vuelo en tiempo de ejecución y las propiedades físicas de la nave en StarWeaver.
    /// </summary>
    public class ShipProperties : MonoBehaviour
    {
        [Header("Físicas de Núcleo")]
        [Tooltip("Masa de la nave en kg. Afecta la inercia y cómo le afectan las fuerzas gravitatorias de los planetas.")]
        public float mass = 5000f;

        [Tooltip("Amortiguación de rotación (RCS automático). Valores más altos ayudan a que la nave deje de girar más rápido al soltar el control.")]
        public float rotationDamping = 2.5f;

        [Tooltip("Amortiguación angular nativa del Rigidbody en Unity.")]
        public float angularDrag = 1.5f;

        [Header("Límites de Velocidad Convencional")]
        [Tooltip("La velocidad máxima convencional que la nave puede alcanzar con propulsión normal (m/s). Evita que la física de Unity falle por velocidades infinitas.")]
        public float maxSpeed = 400f;

        [Header("Propulsores Principales y de Maniobra (RCS)")]
        [Tooltip("Fuerza del motor principal hacia adelante.")]
        public float forwardThrustPower = 45000f;

        [Tooltip("Fuerza del motor de retroceso.")]
        public float reverseThrustPower = 20000f;

        [Tooltip("Fuerza de los propulsores verticales/laterales (maniobras orbitales o de acople).")]
        public float verticalThrustPower = 25000f;

        [Tooltip("Velocidad con la que el empuje alcanza su potencia máxima.")]
        public float thrustRampUpSpeed = 5f;

        [Header("Capacidad de Giro (Torque)")]
        public float pitchPower = 15f;
        public float yawPower = 12f;
        public float rollPower = 20f;

        [Header("Sistema de Boost (Postcombustión)")]
        public float boostMultiplier = 2.0f;
        public float maxBoost = 100f;
        public float boostDrainRate = 25f;
        public float boostRechargeRate = 15f;
        public float boostRechargeDelay = 2f;
        public float boostRampUpSpeed = 4f;

        [Header("Limitador de Fuerzas G (Opcional)")]
        public bool useGForceLimiter = true;
        public float maxOverallGForce = 9.0f;

        [Header("Sistema de Supersalto (Hyperdrive)")]
        [Tooltip("¿La nave está actualmente en modo Supersalto? Mientras esté activo, ignorará la simulación orbital.")]
        public bool isHyperdriveActive = false;
        [Tooltip("Velocidad extrema de desplazamiento lineal durante el Supersalto.")]
        public float hyperdriveSpeed = 5000f;

        [Header("Optimización de IA (Movimiento Cinemático)")]
        [Tooltip("Velocidad de crucero de la IA cuando está lejos del jugador y usa LERP.")]
        public float lerpCruiseSpeed = 150f;
        [Tooltip("Velocidad de rotación de la IA lejana.")]
        public float lerpRotationSpeed = 45f;

        private void Awake()
        {
            // Nos aseguramos de que el Rigidbody de la nave tenga asignada la masa correcta de nuestras propiedades
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = mass;
            }
        }
    }
}