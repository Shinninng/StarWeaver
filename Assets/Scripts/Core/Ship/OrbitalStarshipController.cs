using System;
using UnityEngine;
using StarWeaver.Systems; // Agregamos el namespace de tus sistemas de armas

namespace StarWeaver.Core
{
    public enum StarshipMovementMode
    {
        Physics,
        Lerp
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ShipProperties))]
    public class OrbitalStarshipController : MonoBehaviour
    {
        public event Action<float> OnGForceUpdate;

        [Header("Configuration")]
        public bool isAI = true;

        [Header("Performance Optimization")]
        [SerializeField] private StarshipMovementMode _currentMovementMode = StarshipMovementMode.Physics;

        private ShipProperties properties;
        private Rigidbody rb;

        [Header("Driver Configuration")]
        public MonoBehaviour defaultDriverComponent;
        private IShipDriver currentDriver;

        private ShipInputState currentInputState;
        public bool IsControllingShip { get; private set; } = true;
        public float CurrentThrustLevel { get; private set; }
        public float CurrentVerticalThrustLevel { get; private set; }
        public float CurrentBoostMultiplier { get; private set; } = 1f;
        public float CurrentGForce { get; private set; }

        private Vector3 lastVelocity;
        private float currentBoost;
        private float boostRechargeCooldown;
        private bool isTryingToBoost;
        private float currentGForceGovernor = 1.0f;

        private Vector3 _lerpVelocity;

        // LA VARIABLE DECLARADA CORRECTAMENTE AQUÍ (Nivel de clase):
        private ShipWeaponController weapons;

        public float CurrentSpeed => _currentMovementMode == StarshipMovementMode.Physics
            ? (rb != null && !rb.isKinematic ? rb.linearVelocity.magnitude : 0f)
            : _lerpVelocity.magnitude;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            properties = GetComponent<ShipProperties>();

            // Asignamos el componente a la variable de la clase
            weapons = GetComponent<ShipWeaponController>();

            if (properties == null)
            {
                Debug.LogError("OrbitalStarshipController requiere un componente ShipProperties.", this);
                this.enabled = false;
                return;
            }

            InitializeController();
        }

        private void InitializeController()
        {
            InitializeDriver();
            currentBoost = properties.maxBoost;
            CurrentBoostMultiplier = 1f;
            currentGForceGovernor = 1f;
        }

        void OnEnable()
        {
            SetMovementMode(_currentMovementMode, true);
        }

        void OnDisable()
        {
            if (rb)
            {
                ResetAndClearPhysicsState();
                rb.isKinematic = true;
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
            lastVelocity = Vector3.zero;

            CurrentThrustLevel = 0f;
            CurrentVerticalThrustLevel = 0f;
            CurrentBoostMultiplier = 1f;
            CurrentGForce = 0f;
        }

        private void InitializeDriver()
        {
            if (currentDriver != null) return;

            IShipDriver driverToAssign = defaultDriverComponent as IShipDriver;
            driverToAssign ??= GetComponent<IShipDriver>();

            if (driverToAssign != null)
            {
                SetDriver(driverToAssign);
            }
        }

        public void SetDriver(IShipDriver newDriver)
        {
            currentDriver?.ReleaseController();
            currentDriver = newDriver;

            if (currentDriver != null)
            {
                currentDriver.AssignController(this);
            }
            else
            {
                currentInputState = new ShipInputState();
            }

            if (Application.isPlaying && this.enabled)
            {
                ConfigureRigidbody();
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb == null || properties == null) return;

            rb.mass = properties.mass;

            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                rb.useGravity = false;
                rb.linearDamping = 0f;
                rb.angularDamping = properties.angularDrag;
                rb.interpolation = !isAI ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
        }

        public void SetMovementMode(StarshipMovementMode newMode, bool force = false)
        {
            if (_currentMovementMode == newMode && !force) return;

            StarshipMovementMode previousMode = _currentMovementMode;
            _currentMovementMode = newMode;

            if (newMode == StarshipMovementMode.Lerp)
            {
                if (previousMode == StarshipMovementMode.Physics && rb != null && !rb.isKinematic)
                {
                    _lerpVelocity = rb.linearVelocity;
                }
                if (rb != null) rb.isKinematic = true;
            }
            else
            {
                if (rb != null) rb.isKinematic = false;
                ConfigureRigidbody();

                if (previousMode == StarshipMovementMode.Lerp && rb != null)
                {
                    rb.linearVelocity = _lerpVelocity;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        void Update()
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

            // Llamamos al método de disparo en cada frame
            HandleWeaponsFiring();
        }

        void FixedUpdate()
        {
            if (_currentMovementMode == StarshipMovementMode.Physics)
            {
                CalculateGForce();
                CalculateGForceLimiter();

                ApplyThrust();
                ApplyRotation();
            }
        }

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

        private void HandleBoostLogic()
        {
            isTryingToBoost = currentInputState.Boost && currentBoost > 0f && CurrentThrustLevel > 0.1f;

            if (isTryingToBoost)
            {
                currentBoost -= properties.boostDrainRate * Time.deltaTime;
                boostRechargeCooldown = properties.boostRechargeDelay;
            }
            else
            {
                if (boostRechargeCooldown > 0f) boostRechargeCooldown -= Time.deltaTime;
                else if (currentBoost < properties.maxBoost) currentBoost += properties.boostRechargeRate * Time.deltaTime;
            }
            currentBoost = Mathf.Clamp(currentBoost, 0f, properties.maxBoost);
        }

        private void HandleRamping()
        {
            CurrentThrustLevel = Mathf.Lerp(CurrentThrustLevel, currentInputState.Thrust, Time.deltaTime * properties.thrustRampUpSpeed);
            CurrentVerticalThrustLevel = Mathf.Lerp(CurrentVerticalThrustLevel, currentInputState.Vertical, Time.deltaTime * properties.thrustRampUpSpeed);

            float targetBoostMultiplier = 1f;
            if (isTryingToBoost)
            {
                targetBoostMultiplier = properties.boostMultiplier;
            }
            CurrentBoostMultiplier = Mathf.Lerp(CurrentBoostMultiplier, targetBoostMultiplier, Time.deltaTime * properties.boostRampUpSpeed);
        }

        private void ApplyThrust()
        {
            float effectiveGovernor = currentGForceGovernor;

            if (Mathf.Abs(CurrentThrustLevel) > 0.01f)
            {
                float power = CurrentThrustLevel > 0 ? properties.forwardThrustPower : properties.reverseThrustPower;
                rb.AddForce(CurrentBoostMultiplier * CurrentThrustLevel * effectiveGovernor * power * transform.forward, ForceMode.Force);
            }

            if (Mathf.Abs(CurrentVerticalThrustLevel) > 0.01f)
            {
                rb.AddForce(CurrentVerticalThrustLevel * effectiveGovernor * properties.verticalThrustPower * transform.up, ForceMode.Force);
            }
        }

        private void ApplyRotation()
        {
            float maneuverabilityMultiplier = 1.0f;
            float effectiveGovernor = currentGForceGovernor;

            float rollTorque = currentInputState.Roll * properties.rollPower * effectiveGovernor;
            float pitchTorque = 0f;
            float yawTorque = 0f;

            if (IsControllingShip)
            {
                yawTorque = currentInputState.Yaw * properties.yawPower * maneuverabilityMultiplier * effectiveGovernor;
                pitchTorque = -currentInputState.Pitch * properties.pitchPower * maneuverabilityMultiplier * effectiveGovernor;
            }

            Vector3 torque = new Vector3(pitchTorque, yawTorque, rollTorque);
            rb.AddRelativeTorque(torque, ForceMode.Force);

            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, properties.rotationDamping * Time.fixedDeltaTime);
        }

        private void CalculateGForce()
        {
            Vector3 currentVelocity = rb.linearVelocity;
            if (Time.fixedDeltaTime > 0)
            {
                Vector3 currentAcceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;
                CurrentGForce = currentAcceleration.magnitude / 9.81f;
            }
            lastVelocity = currentVelocity;
            OnGForceUpdate?.Invoke(CurrentGForce);
        }

        private void CalculateGForceLimiter()
        {
            if (!properties.useGForceLimiter || properties.maxOverallGForce <= 0)
            {
                currentGForceGovernor = 1.0f;
                return;
            }

            float maxG = properties.maxOverallGForce;
            if (CurrentGForce <= maxG)
            {
                currentGForceGovernor = Mathf.Lerp(currentGForceGovernor, 1.0f, Time.fixedDeltaTime * 5.0f);
            }
            else
            {
                currentGForceGovernor = Mathf.Clamp(1.0f / (CurrentGForce / maxG), 0.05f, 1.0f);
            }
        }

        private void UpdateLerpMovement()
        {
            float rotationRate = properties.lerpRotationSpeed;
            float pitch = -currentInputState.Pitch * rotationRate * Time.deltaTime;
            float yaw = currentInputState.Yaw * rotationRate * Time.deltaTime;
            float roll = currentInputState.Roll * rotationRate * Time.deltaTime;

            if (Mathf.Abs(pitch) > 0.001f || Mathf.Abs(yaw) > 0.001f || Mathf.Abs(roll) > 0.001f)
            {
                transform.rotation *= Quaternion.Euler(pitch, yaw, roll);
            }

            float forwardSpeed = currentInputState.Thrust * properties.lerpCruiseSpeed;
            Vector3 totalMovement = forwardSpeed * Time.deltaTime * transform.forward;
            transform.position += totalMovement;

            if (Time.deltaTime > 0)
            {
                _lerpVelocity = totalMovement / Time.deltaTime;
                CurrentGForce = (_lerpVelocity - lastVelocity).magnitude / (Time.deltaTime * 9.81f);
                lastVelocity = _lerpVelocity;
                OnGForceUpdate?.Invoke(CurrentGForce);
            }
        }

        private void HandleWeaponsFiring()
        {
            if (weapons == null) return;

            if (currentInputState.FirePrimary)
            {
                weapons.FirePrimary(currentInputState.AimPosition);
            }
            if (currentInputState.FireSecondary)
            {
                weapons.FireSecondary(currentInputState.AimPosition);
            }
        }
    }
}