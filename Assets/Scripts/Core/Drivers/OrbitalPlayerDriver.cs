using UnityEngine;
using StarWeaver.Core;
using StarWeaver.Input;

namespace StarWeaver.Core
{
    /// <summary>
    /// Driver de nave controlado por el jugador humano.
    /// Lee input del InputProvider (New Input System) y lo traduce a ShipInputState.
    ///
    /// RESPONSABILIDAD ÚNICA: Traducir input humano a comandos de nave.
    /// No conoce física, no aplica fuerzas, no sabe nada de armas ni cámara.
    ///
    /// FIX CRÍTICO: Registra el Rigidbody en OrbitalManager para que la simulación
    /// gravitacional incluya a la nave. Sin esto, la nave no recibe fuerzas orbitales.
    /// </summary>
    [RequireComponent(typeof(ShipProperties))]
    [RequireComponent(typeof(Rigidbody))]
    public class OrbitalPlayerDriver : MonoBehaviour, IShipDriver
    {
        [Header("Apuntado")]
        [SerializeField] private float maxAimDistance = 1000f;

        [Header("Control del Mouse")]
        [Tooltip("Si está activo, el eje X del mouse controla el Yaw además de A/D.")]
        [SerializeField] private bool mouseControlsYaw = false;

        private UnityEngine.Camera _playerCamera;
        private OrbitalStarshipController _controller;
        private Rigidbody _rb;

        private bool _isActivelyControlling = true;

        // ─────────────────────────────────────────────────────────────
        //  Ciclo de vida
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // CRÍTICO: registrar en OrbitalManager aquí (Start), no en Awake.
            // OrbitalManager.Instance puede no estar listo en Awake si su propio
            // Awake no corrió todavía. Start garantiza que toda la escena inicializó.
            if (_rb != null)
            {
                OrbitalManager.RegisterBody(_rb);
                Debug.Log($"[OrbitalPlayerDriver] Nave '{gameObject.name}' registrada en OrbitalManager.");
            }
            else
            {
                Debug.LogError($"[OrbitalPlayerDriver] No se encontró Rigidbody en '{gameObject.name}'. " +
                               "La nave no participará en la simulación orbital.", this);
            }
        }

        private void OnEnable()
        {
            UIStateEvents.OnMenuOpened += HandleMenuOpened;
            UIStateEvents.OnMenuClosed += HandleMenuClosed;
        }

        private void OnDisable()
        {
            UIStateEvents.OnMenuOpened -= HandleMenuOpened;
            UIStateEvents.OnMenuClosed -= HandleMenuClosed;
        }

        private void OnDestroy()
        {
            // CRÍTICO: desregistrar al destruir para que el OrbitalManager
            // no intente aplicar fuerzas a un Rigidbody destruido (NullReference).
            if (_rb != null)
                OrbitalManager.UnregisterBody(_rb);
        }

        // ─────────────────────────────────────────────────────────────
        //  IShipDriver
        // ─────────────────────────────────────────────────────────────

        public void AssignController(OrbitalStarshipController controller) => _controller = controller;
        public void ReleaseController() => _controller = null;
        public bool IsActivelyControlling() => _isActivelyControlling;

        public string GetControlDescription() =>
            "WASD: Empuje | A/D: Yaw | Q/E: Roll | Mouse Y: Pitch | Espacio/Ctrl: Vertical | Shift: Boost";

        public ShipInputState GetDesiredInputState()
        {
            ShipInputState state = new ShipInputState();
            if (!_isActivelyControlling) return state;

            InputProvider input = InputProvider.Instance;
            if (input == null)
            {
                Debug.LogWarning("[OrbitalPlayerDriver] InputProvider no encontrado en escena.");
                return state;
            }

            // ── Traslación ───────────────────────────────────────────
            Vector2 movement = input.GetMovementInput();
            state.Thrust = movement.y;   // W/S
            state.Vertical = input.GetVerticalMovement(); // Espacio / Ctrl

            // ── Rotación ─────────────────────────────────────────────
            state.Roll = input.GetRollInput(); // Q/E

            Vector2 lookDelta = input.GetLookDelta();
            state.Pitch = lookDelta.y; // Mouse Y → cabeceo

            // Yaw: A/D siempre. Mouse X opcional según configuración.
            state.Yaw = mouseControlsYaw
                ? movement.x + lookDelta.x
                : movement.x;

            // ── Boost y Armas ─────────────────────────────────────────
            state.Boost = input.IsBoostHeld();
            state.FirePrimary = input.IsFirePrimaryHeld();
            state.FireSecondary = input.IsFireSecondaryHeld();
            state.FireBomb = input.IsFireBombPressed();

            // ── Punto de mira ─────────────────────────────────────────
            state.AimPosition = CalculateAimPosition();

            return state;
        }

        // ─────────────────────────────────────────────────────────────
        //  Handlers de UI
        // ─────────────────────────────────────────────────────────────

        private void HandleMenuOpened()
        {
            _isActivelyControlling = false;
            InputProvider.Instance?.DisableShipControls();
        }

        private void HandleMenuClosed()
        {
            _isActivelyControlling = true;
            InputProvider.Instance?.EnableShipControls();
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────

        private Vector3 CalculateAimPosition()
        {
            if (_playerCamera == null)
                _playerCamera = UnityEngine.Camera.main;

            if (_playerCamera == null)
                return transform.position + transform.forward * maxAimDistance;

            Ray ray = _playerCamera.ScreenPointToRay(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            return ray.origin + ray.direction * maxAimDistance;
        }

        // ─────────────────────────────────────────────────────────────
        //  Validación en Editor
        // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            var provider = FindAnyObjectByType<InputProvider>();
            if (provider == null)
                Debug.LogWarning($"[{nameof(OrbitalPlayerDriver)}] No hay InputProvider en la escena.", this);

            var orbitalManager = FindAnyObjectByType<OrbitalManager>();
            if (orbitalManager == null)
                Debug.LogWarning($"[{nameof(OrbitalPlayerDriver)}] No hay OrbitalManager en la escena. " +
                                 "La nave no recibirá fuerzas gravitacionales.", this);
        }
#endif
    }
}