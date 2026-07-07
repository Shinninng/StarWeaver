using UnityEngine;

namespace StarWeaver.Core
{
    /// <summary>
    /// Contiene todas las propiedades de vuelo y físicas de una nave en StarWeaver.
    ///
    /// NOTA ARQUITECTÓNICA: Este MonoBehaviour es funcional para la etapa actual del proyecto.
    /// Cuando el juego tenga múltiples tipos de nave (Caza, Fragata, etc.), la evolución
    /// natural es migrar estos datos a un ShipDataSO : ScriptableObject, y que este
    /// componente simplemente los consuma. Eso permitirá crear perfiles de nave en el
    /// Editor sin duplicar GameObjects.
    /// </summary>
    public class ShipProperties : MonoBehaviour
    {
        [Header("Físicas de Núcleo")]
        [Tooltip("Masa de la nave en kg. Afecta la inercia y cómo le afectan las fuerzas gravitatorias.")]
        public float mass = 5000f;

        [Tooltip("Amortiguación de rotación aplicada por el Flight Assist cuando el jugador suelta los controles. " +
                 "Valores más altos frenan la rotación más rápido.")]
        public float rotationDamping = 2.5f;

        // RENOMBRADO: era 'angularDrag' (nombre del Inspector pre-Unity 6).
        // En Unity 6, la propiedad del Rigidbody se llama 'angularDamping'.
        // Mantenemos coherencia entre el nombre del campo y la API que usamos.
        [Tooltip("Amortiguación angular base del Rigidbody. Se usa como valor inicial; " +
                 "el Flight Assist lo sobreescribe dinámicamente en runtime.")]
        public float angularDamping = 1.5f;

        [Header("Límites de Velocidad")]
        [Tooltip("Velocidad máxima alcanzable con propulsión convencional (m/s). " +
                 "Evita que la física de Unity falle con velocidades extremas.")]
        public float maxSpeed = 400f;

        [Header("Propulsores Principales y RCS")]
        [Tooltip("Fuerza del motor principal hacia adelante (Newtons).")]
        public float forwardThrustPower = 45000f;

        [Tooltip("Fuerza del motor de retroceso (Newtons).")]
        public float reverseThrustPower = 20000f;

        [Tooltip("Fuerza de los propulsores verticales para maniobras orbitales o acople (Newtons).")]
        public float verticalThrustPower = 25000f;

        [Tooltip("Velocidad con la que el empuje alcanza su potencia máxima (suavizado de input).")]
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

        [Header("Limitador de Fuerzas G")]
        public bool useGForceLimiter = true;
        public float maxOverallGForce = 9.0f;

        [Header("Sistema de Supersalto (Hyperdrive)")]
        [Tooltip("Si está activo, la nave ignorará la simulación orbital y usará velocidad hiperespacial.")]
        public bool isHyperdriveActive = false;
        [Tooltip("Velocidad de desplazamiento durante el Supersalto (m/s).")]
        public float hyperdriveSpeed = 5000f;

        [Header("Parámetros de IA (Modo Lerp)")]
        [Tooltip("Velocidad de crucero cuando la IA está lejos del jugador y usa movimiento cinemático.")]
        public float lerpCruiseSpeed = 150f;
        [Tooltip("Velocidad de rotación de la IA en modo cinemático (grados/segundo).")]
        public float lerpRotationSpeed = 45f;

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = mass;
            }
        }
    }
}