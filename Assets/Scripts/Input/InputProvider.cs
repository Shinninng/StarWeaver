using UnityEngine;
using UnityEngine.InputSystem;

namespace StarWeaver.Input
{
    /// <summary>
    /// Singleton centralizado de inputs para StarWeaver.
    /// Usa el New Input System a través de la clase generada StarWeaverInputActions
    /// (generada desde Assets/Scripts/Input/StarWeaverInputActions.inputactions).
    ///
    /// MAPA DE TECLAS (definido en el asset .inputactions, no en este script):
    ///   WASD          → Move  (y = Thrust, x = Yaw)
    ///   Q / E         → Roll izquierda / derecha
    ///   Espacio/Ctrl  → Vertical arriba / abajo
    ///   Mouse Delta   → Look  (y = Pitch, x = Yaw opcional)
    ///   Shift Izq     → Boost
    ///   Mouse 0 / 1   → FirePrimary / FireSecondary
    ///   F             → FireBomb
    ///   Mouse 2       → Zoom
    ///   T             → Interact
    ///   Tab / Esc     → Menu
    ///   R             → Reload
    ///   Scroll        → ScrollScanner
    ///
    /// DISEÑO — Por qué Singleton aquí:
    /// InputProvider es un servicio de infraestructura pura, sin estado de juego.
    /// El patrón Singleton está justificado porque: (1) solo puede existir un dispositivo
    /// de input por sesión, (2) su ciclo de vida es el de la aplicación completa, y
    /// (3) acceder a él por DI en cada MonoBehaviour que necesita input agrega
    /// complejidad sin beneficio real a esta escala de proyecto.
    /// Si el proyecto crece a multijugador local (split-screen), se reemplaza
    /// por un InputProvider por jugador pasado explícitamente al IShipDriver.
    /// </summary>
    public class InputProvider : MonoBehaviour
    {
        // ─────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────

        private static InputProvider _instance;

        public static InputProvider Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // FIX: FindFirstObjectByType deprecado en Unity 6.
                // FindAnyObjectByType es equivalente para nuestro caso (solo existe
                // una instancia) y no depende del orden de instance ID interno.
                _instance = Object.FindAnyObjectByType<InputProvider>();

                if (_instance == null)
                {
                    GameObject go = new GameObject("[InputProvider]");
                    _instance = go.AddComponent<InputProvider>();
                    Debug.Log("[InputProvider] Creado automáticamente como Singleton.");
                }

                return _instance;
            }
        }

        // ─────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────

        [Header("Sensibilidad del Mouse")]
        [SerializeField] private float mouseSensitivityX = 1f;
        [SerializeField] private float mouseSensitivityY = 1f;

        // ─────────────────────────────────────────
        //  Ciclo de vida
        // ─────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[InputProvider] Instancia duplicada destruida.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Necesario si el GameObject quedó como hijo de otro en la escena,
            // ya que DontDestroyOnLoad requiere que sea un objeto raíz.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeInputActions();
        }

        private void OnDestroy()
        {
            // Liberamos los recursos del Input System explícitamente.
            // Sin esto, el asset queda vivo en memoria entre recargas de escena en el Editor.
            _actions?.Disable();
            _actions?.Dispose();
        }

        // ─────────────────────────────────────────
        //  Input Actions
        // ─────────────────────────────────────────

        private StarWeaverInputActions _actions;

        // Cacheamos las referencias a las acciones individuales para evitar
        // el lookup por string en cada frame (que es lo que hace _actions.Ship.Move
        // internamente si no lo cacheás).
        private InputAction _moveAction;
        private InputAction _verticalAction;
        private InputAction _rollAction;
        private InputAction _lookAction;
        private InputAction _boostAction;
        private InputAction _firePrimaryAction;
        private InputAction _fireSecondaryAction;
        private InputAction _fireBombAction;
        private InputAction _zoomAction;
        private InputAction _interactAction;
        private InputAction _reloadAction;
        private InputAction _menuAction;
        private InputAction _scrollScannerAction;

        private void InitializeInputActions()
        {
            _actions = new StarWeaverInputActions();
            _actions.Enable();

            _moveAction = _actions.Ship.Move;
            _verticalAction = _actions.Ship.Vertical;
            _rollAction = _actions.Ship.Roll;
            _lookAction = _actions.Ship.Look;
            _boostAction = _actions.Ship.Boost;
            _firePrimaryAction = _actions.Ship.FirePrimary;
            _fireSecondaryAction = _actions.Ship.FireSecondary;
            _fireBombAction = _actions.Ship.FireBomb;
            _zoomAction = _actions.Ship.Zoom;
            _interactAction = _actions.Ship.Interact;
            _reloadAction = _actions.Ship.Reload;
            _menuAction = _actions.Ship.Menu;
            _scrollScannerAction = _actions.Ship.ScrollScanner;
        }

        // ─────────────────────────────────────────
        //  API Pública — Movimiento
        // ─────────────────────────────────────────

        /// <summary>WASD: x = Yaw (A/D), y = Thrust (W/S). Rango [-1, 1] en cada eje.</summary>
        public Vector2 GetMovementInput() => _moveAction.ReadValue<Vector2>();

        /// <summary>Espacio = +1 (subir) / Ctrl Izq = -1 (bajar).</summary>
        public float GetVerticalMovement() => _verticalAction.ReadValue<float>();

        /// <summary>Q = -1 (roll izquierda) / E = +1 (roll derecha).</summary>
        public float GetRollInput() => _rollAction.ReadValue<float>();

        /// <summary>
        /// Delta del mouse escalado por sensibilidad.
        /// x = horizontal (Yaw opcional), y = vertical (Pitch).
        /// El escalado por sensibilidad se aplica aquí para centralizar el ajuste.
        /// </summary>
        public Vector2 GetLookDelta()
        {
            Vector2 raw = _lookAction.ReadValue<Vector2>();
            return new Vector2(raw.x * mouseSensitivityX, raw.y * mouseSensitivityY);
        }

        // ─────────────────────────────────────────
        //  API Pública — Acciones
        // ─────────────────────────────────────────

        /// <summary>Shift Izquierdo — sostenido.</summary>
        public bool IsBoostHeld() => _boostAction.IsPressed();

        /// <summary>Mouse Botón 0 — sostenido (disparo continuo).</summary>
        public bool IsFirePrimaryHeld() => _firePrimaryAction.IsPressed();

        /// <summary>Mouse Botón 1 — sostenido.</summary>
        public bool IsFireSecondaryHeld() => _fireSecondaryAction.IsPressed();

        /// <summary>F — one-shot por frame. Equivalente a GetKeyDown en el Input Manager legacy.</summary>
        public bool IsFireBombPressed() => _fireBombAction.WasPressedThisFrame();

        /// <summary>Mouse 2 (scroll click) — one-shot por frame.</summary>
        public bool IsZoomPressed() => _zoomAction.WasPressedThisFrame();

        /// <summary>T — interactuar con objetos del entorno.</summary>
        public bool IsInteractPressed() => _interactAction.WasPressedThisFrame();

        /// <summary>Tab / Esc — abrir o cerrar menú.</summary>
        public bool IsMenuPressed() => _menuAction.WasPressedThisFrame();

        /// <summary>R — recarga / acción reservada.</summary>
        public bool IsRPressed() => _reloadAction.WasPressedThisFrame();

        /// <summary>
        /// Scroll del mouse para cambiar el scanner activo.
        /// Devuelve +1 (siguiente), -1 (anterior) o 0 (sin cambio).
        /// El umbral de 0.05 evita micro-movimientos involuntarios del scroll.
        /// </summary>
        public int GetScannerScrollDelta()
        {
            float scroll = _scrollScannerAction.ReadValue<float>();
            if (scroll > 0.05f) return 1;
            if (scroll < -0.05f) return -1;
            return 0;
        }

        // ─────────────────────────────────────────
        //  Control de habilitación (para menús, pausa, etc.)
        // ─────────────────────────────────────────

        /// <summary>
        /// Desactiva todos los controles de la nave.
        /// Llamar desde UIStateEvents o desde el sistema de pausa.
        /// Mientras el mapa esté desactivado, todos los ReadValue devuelven default.
        /// </summary>
        public void DisableShipControls() => _actions.Ship.Disable();

        /// <summary>Re-activa los controles de la nave al cerrar el menú o despausar.</summary>
        public void EnableShipControls() => _actions.Ship.Enable();
    }
}