using System;
using UnityEngine;
using StarWeaver.Systems;

namespace StarWeaver.Core
{
    public enum StarshipMovementMode
    {
        /// <summary>Físicas completas con Rigidbody. Para el jugador y naves IA cercanas.</summary>
        Physics,
        /// <summary>Movimiento cinemático por Lerp. Para naves IA lejanas (optimización de CPU).</summary>
        Lerp
    }

    /// <summary>
    /// Controlador central de la nave. Recibe un ShipInputState de un IShipDriver
    /// (jugador o IA) y lo convierte en fuerzas físicas o movimiento cinemático.
    ///
    /// RESPONSABILIDAD: Física de vuelo, boost, G-Force, Flight Assist y armas.
    /// NO ES RESPONSABILIDAD SUYA: Leer input, conocer la cámara, gestionar menús.
    ///
    /// FIX CRÍTICO: InitializeDriver() movido a Start() para garantizar que
    /// OrbitalPlayerDriver (y cualquier otro IShipDriver) haya completado su
    /// propio Awake() antes de ser buscado con GetComponent.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ShipProperties))]
    public class OrbitalStarshipController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        //  Eventos públicos
        // ─────────────────────────────────────────────────────────────

        public event Action<float> OnGForceUpdate;
        public event Action<float> OnBoostChanged;

        // ─────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("Configuración")]
        [Tooltip("¿Esta nave es controlada por IA? Desactivar para la nave del jugador.")]
        [SerializeField] private bool isAI = false;

        [Header("Modo de Movimiento")]
        [SerializeField] private StarshipMovementMode _currentMovementMode = StarshipMovementMode.Physics;

        [Header("Driver")]
        [Tooltip("Asigná aquí el componente IShipDriver (ej: OrbitalPlayerDriver). " +
                 "Si se deja vacío, se buscará automáticamente en este GameObject.")]
        public MonoBehaviour defaultDriverComponent;

        // ─────────────────────────────────────────────────────────────
        //  Propiedades públicas
        // ─────────────────────────────────────────────────────────────

        public bool IsControllingShip { get; private set; } = true;
        public float CurrentThrustLevel { get; private set; }
        public float CurrentVerticalThrustLevel { get; private set; }
        public float CurrentBoostMultiplier { get; private set; } = 1f;
        public float CurrentGForce { get; private set; }
        public float CurrentBoostNormalized => currentBoost / Mathf.Max(properties.maxBoost, 0.001f);

        public float CurrentSpeed => _currentMovementMode == StarshipMovementMode.Physics
            ? (rb != null && !rb.isKinematic ? rb.linearVelocity.magnitude : 0f)
            : _lerpVelocity.magnitude;

        // ─────────────────────────────────────────────────────────────
        //  Estado interno
        // ─────────────────────────────────────────────────────────────

        private ShipProperties properties;
        private Rigidbody rb;
        private ShipWeaponController weapons;
        private IShipDriver currentDriver;

        private ShipInputState currentInputState;

        private Vector3 _lastVelocity;
        private float currentBoost;
        private float boostRechargeCooldown;
        private bool isTryingToBoost;
        private float currentGForceGovernor = 1f;
        private Vector3 _lerpVelocity;

        // ─────────────────────────────────────────────────────────────
        //  Inicialización
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            properties = GetComponent<ShipProperties>();
            weapons = GetComponent<ShipWeaponController>();

            if (properties == null)
            {
                Debug.LogError($"[{nameof(OrbitalStarshipController)}] Falta ShipProperties. " +
                               "El controlador se deshabilitará.", this);
                enabled = false;
                return;
            }

            // Solo inicializamos el estado interno aquí.
            // El driver se busca en Start() para respetar el orden de Awake entre componentes.
            currentBoost = properties.maxBoost;
            CurrentBoostMultiplier = 1f;
            currentGForceGovernor = 1f;

            ConfigureRigidbody();
        }

        private void Start()
        {
            // FIX: mover InitializeDriver a Start garantiza que todos los Awake()
            // de la escena terminaron. Así OrbitalPlayerDriver ya está inicializado
            // cuando GetComponent<IShipDriver>() lo busca.
            InitializeDriver();
        }

        private void InitializeDriver()
        {
            if (currentDriver != null) return;

            // Primero intentamos el campo explícito del Inspector.
            IShipDriver driverToAssign = defaultDriverComponent as IShipDriver;

            // Si no está asignado o el cast falló, buscamos en el GameObject.
            driverToAssign ??= GetComponent<IShipDriver>();

            if (driverToAssign != null)
            {
                SetDriver(driverToAssign);
                Debug.Log($"[{nameof(OrbitalStarshipController)}] Driver asignado: " +
                          $"{driverToAssign.GetType().Name}", this);
            }
            else
            {
                Debug.LogWarning($"[{nameof(OrbitalStarshipController)}] No se encontró ningún IShipDriver. " +
                                 "La nave no responderá a ningún input.", this);
            }
        }

        private void OnEnable()
        {
            // Aplicamos el modo de movimiento actual al activar el componente.
            // 'force: true' para que se ejecute aunque el modo no haya cambiado.
            if (rb != null)
                SetMovementMode(_currentMovementMode, force: true);
        }

        private void OnDisable()
        {
            if (rb == null) return;
            ResetAndClearPhysicsState();
            rb.isKinematic = true;
        }

        // ─────────────────────────────────────────────────────────────
        //  API Pública
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Asigna un nuevo driver en runtime.
        /// Útil para pasar el control de IA a jugador (entrar a una nave capturada).
        /// </summary>
        public void SetDriver(IShipDriver newDriver)
        {
            currentDriver?.ReleaseController();
            currentDriver = newDriver;

            if (currentDriver != null)
                currentDriver.AssignController(this);
            else
                currentInputState = new ShipInputState();

            if (Application.isPlaying && enabled)
                ConfigureRigidbody();
        }

        /// <summary>
        /// Cambia entre modo Physics y Lerp preservando la velocidad actual.
        /// </summary>
        public void SetMovementMode(StarshipMovementMode newMode, bool force = false)
        {
            if (_currentMovementMode == newMode && !force) return;

            StarshipMovementMode previousMode = _currentMovementMode;
            _currentMovementMode = newMode;

            if (newMode == StarshipMovementMode.Lerp)
            {
                if (previousMode == StarshipMovementMode.Physics && rb != null && !rb.isKinematic)
                    _lerpVelocity = rb.linearVelocity;

                if (rb != null) rb.isKinematic = true;
            }
            else
            {
                if (rb != null) rb.isKinematic = false;
                ConfigureRigidbody();

                // Reseteamos el governor para que no arrastre valores de una sesión anterior.
                currentGForceGovernor = 1f;

                if (previousMode == StarshipMovementMode.Lerp && rb != null)
                {
                    rb.linearVelocity = _lerpVelocity;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        public void ResetAndClearPhysicsState()
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            _lerpVelocity = Vector3.zero;
            _lastVelocity = Vector3.zero;

            CurrentThrustLevel = 0f;
            CurrentVerticalThrustLevel = 0f;
            CurrentBoostMultiplier = 1f;
            CurrentGForce = 0f;
            currentGForceGovernor = 1f;
        }

        // ─────────────────────────────────────────────────────────────
        //  Update Loop
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            UpdateInputFromDriver();

            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                HandleBoostLogic();
                HandleRamping();
            }
            else
            {
                UpdateLerpMovement();
            }

            HandleWeaponsFiring();
        }

        private void FixedUpdate()
        {
            if (_currentMovementMode != StarshipMovementMode.Physics) return;

            CalculateGForce();
            CalculateGForceLimiter();
            ApplyThrust();
            ApplyFlightAssist();
        }

        // ─────────────────────────────────────────────────────────────
        //  Input
        // ─────────────────────────────────────────────────────────────

        private void UpdateInputFromDriver()
        {
            if (currentDriver != null)
            {
                currentInputState = currentDriver.GetDesiredInputState();
                IsControllingShip = currentDriver.IsActivelyControlling();
            }
            else
            {
                currentInputState = new ShipInputState();
                IsControllingShip = false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Boost
        // ─────────────────────────────────────────────────────────────

        private void HandleBoostLogic()
        {
            isTryingToBoost = currentInputState.Boost
                              && currentBoost > 0f
                              && CurrentThrustLevel > 0.1f;

            if (isTryingToBoost)
            {
                currentBoost -= properties.boostDrainRate * Time.deltaTime;
                boostRechargeCooldown = properties.boostRechargeDelay;
            }
            else
            {
                if (boostRechargeCooldown > 0f)
                    boostRechargeCooldown -= Time.deltaTime;
                else if (currentBoost < properties.maxBoost)
                    currentBoost += properties.boostRechargeRate * Time.deltaTime;
            }

            currentBoost = Mathf.Clamp(currentBoost, 0f, properties.maxBoost);
            OnBoostChanged?.Invoke(CurrentBoostNormalized);
        }

        // ─────────────────────────────────────────────────────────────
        //  Ramping
        // ─────────────────────────────────────────────────────────────

        private void HandleRamping()
        {
            CurrentThrustLevel = Mathf.Lerp(
                CurrentThrustLevel, currentInputState.Thrust,
                Time.deltaTime * properties.thrustRampUpSpeed);

            CurrentVerticalThrustLevel = Mathf.Lerp(
                CurrentVerticalThrustLevel, currentInputState.Vertical,
                Time.deltaTime * properties.thrustRampUpSpeed);

            float targetBoost = isTryingToBoost ? properties.boostMultiplier : 1f;
            CurrentBoostMultiplier = Mathf.Lerp(
                CurrentBoostMultiplier, targetBoost,
                Time.deltaTime * properties.boostRampUpSpeed);
        }

        // ─────────────────────────────────────────────────────────────
        //  Empuje (FixedUpdate)
        // ─────────────────────────────────────────────────────────────

        private void ApplyThrust()
        {
            // Limitador de velocidad máxima: bloquea solo el empuje forward
            // cuando ya alcanzamos maxSpeed. No bloquea maniobras ni gravedad.
            if (rb.linearVelocity.magnitude >= properties.maxSpeed)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
                if (localVelocity.z > 0f) return;
            }

            if (Mathf.Abs(CurrentThrustLevel) > 0.01f)
            {
                float power = CurrentThrustLevel > 0f
                    ? properties.forwardThrustPower
                    : properties.reverseThrustPower;

                rb.AddForce(
                    CurrentBoostMultiplier * CurrentThrustLevel * currentGForceGovernor
                    * power * transform.forward,
                    ForceMode.Force);
            }

            if (Mathf.Abs(CurrentVerticalThrustLevel) > 0.01f)
            {
                rb.AddForce(
                    CurrentVerticalThrustLevel * currentGForceGovernor
                    * properties.verticalThrustPower * transform.up,
                    ForceMode.Force);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Flight Assist (FixedUpdate)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Sistema de rotación bifurcado:
        /// - Con input: torque libre, angularDamping = 0 (el solver no interfiere).
        /// - Sin input: angularDamping nativo de Unity frena la inercia angular.
        ///
        /// Por qué angularDamping y no Lerp manual: AddRelativeTorque encola el cambio;
        /// si pisamos angularVelocity con Lerp en la misma llamada, leemos el valor
        /// PRE-torque (el solver aún no integró). angularDamping se aplica DESPUÉS
        /// de integrar todas las fuerzas del step — es el método correcto en Unity 6.
        /// </summary>
        private void ApplyFlightAssist()
        {
            bool hasRotationInput = IsControllingShip &&
                                    (Mathf.Abs(currentInputState.Roll) > 0.01f ||
                                     Mathf.Abs(currentInputState.Pitch) > 0.01f ||
                                     Mathf.Abs(currentInputState.Yaw) > 0.01f);

            if (hasRotationInput)
                ApplyRotationTorque();
            else
                ApplyFlightAssistBraking();
        }

        private void ApplyRotationTorque()
        {
            rb.angularDamping = 0f;

            float pitchTorque = -currentInputState.Pitch * properties.pitchPower * currentGForceGovernor;
            float yawTorque = currentInputState.Yaw * properties.yawPower * currentGForceGovernor;
            float rollTorque = currentInputState.Roll * properties.rollPower * currentGForceGovernor;

            rb.AddRelativeTorque(new Vector3(pitchTorque, yawTorque, rollTorque), ForceMode.Force);
        }

        private void ApplyFlightAssistBraking()
        {
            // Cedemos el frenado al solver de Unity. Se aplica DESPUÉS de integrar
            // todas las fuerzas del frame — físicamente correcto y sin carreras de datos.
            rb.angularDamping = properties.rotationDamping;
        }

        // ─────────────────────────────────────────────────────────────
        //  G-Force
        // ─────────────────────────────────────────────────────────────

        private void CalculateGForce()
        {
            Vector3 currentVelocity = rb.linearVelocity;

            if (Time.fixedDeltaTime > 0f)
            {
                Vector3 acceleration = (currentVelocity - _lastVelocity) / Time.fixedDeltaTime;
                CurrentGForce = acceleration.magnitude / 9.81f;
            }

            _lastVelocity = currentVelocity;
            OnGForceUpdate?.Invoke(CurrentGForce);
        }

        private void CalculateGForceLimiter()
        {
            if (!properties.useGForceLimiter || properties.maxOverallGForce <= 0f)
            {
                currentGForceGovernor = 1f;
                return;
            }

            float maxG = properties.maxOverallGForce;

            if (CurrentGForce <= maxG)
                currentGForceGovernor = Mathf.Lerp(currentGForceGovernor, 1f, Time.fixedDeltaTime * 5f);
            else
                currentGForceGovernor = Mathf.Clamp(maxG / CurrentGForce, 0.05f, 1f);
        }

        // ─────────────────────────────────────────────────────────────
        //  Modo Lerp
        // ─────────────────────────────────────────────────────────────

        private void UpdateLerpMovement()
        {
            float rotRate = properties.lerpRotationSpeed;
            float pitch = -currentInputState.Pitch * rotRate * Time.deltaTime;
            float yaw = currentInputState.Yaw * rotRate * Time.deltaTime;
            float roll = currentInputState.Roll * rotRate * Time.deltaTime;

            if (Mathf.Abs(pitch) > 0.001f || Mathf.Abs(yaw) > 0.001f || Mathf.Abs(roll) > 0.001f)
                transform.rotation *= Quaternion.Euler(pitch, yaw, roll);

            Vector3 movement = currentInputState.Thrust * properties.lerpCruiseSpeed
                               * Time.deltaTime * transform.forward;
            transform.position += movement;

            if (Time.deltaTime > 0f)
            {
                _lerpVelocity = movement / Time.deltaTime;
                CurrentGForce = (_lerpVelocity - _lastVelocity).magnitude / (Time.deltaTime * 9.81f);
                _lastVelocity = _lerpVelocity;
                OnGForceUpdate?.Invoke(CurrentGForce);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Armas
        // ─────────────────────────────────────────────────────────────

        private void HandleWeaponsFiring()
        {
            if (weapons == null) return;

            if (currentInputState.FirePrimary)
                weapons.FirePrimary(currentInputState.AimPosition);

            if (currentInputState.FireSecondary)
                weapons.FireSecondary(currentInputState.AimPosition);
        }

        // ─────────────────────────────────────────────────────────────
        //  Configuración de Rigidbody
        // ─────────────────────────────────────────────────────────────

        private void ConfigureRigidbody()
        {
            if (rb == null || properties == null) return;

            rb.mass = properties.mass;
            rb.useGravity = false;  // La gravedad la maneja OrbitalManager
            rb.linearDamping = 0f;     // Sin fricción lineal — espacio vacío
            rb.angularDamping = 0f;     // Flight Assist lo controla dinámicamente
            rb.interpolation = !isAI
                ? RigidbodyInterpolation.Interpolate  // Suaviza el movimiento visual del jugador
                : RigidbodyInterpolation.None;
        }

        // ─────────────────────────────────────────────────────────────
        //  Validación en Editor
        // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (defaultDriverComponent != null && defaultDriverComponent as IShipDriver == null)
            {
                Debug.LogWarning(
                    $"[{nameof(OrbitalStarshipController)}] '{defaultDriverComponent.GetType().Name}' " +
                    $"no implementa IShipDriver. La nave no tendrá control.", this);
            }
        }
#endif
    }
}