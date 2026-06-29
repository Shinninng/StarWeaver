using UnityEngine;
using UnityEngine.InputSystem;

namespace StarWeaver.Input
{
    /// <summary>
    /// Singleton centralizado de inputs para StarWeaver.
    /// Usa el New Input System a través de la clase generada StarWeaverInputActions
    /// (generada desde Assets/Scripts/Input/StarWeaverInputActions.inputactions).
    ///
    /// MAPA DE TECLAS (definido en el asset, no en este script):
    ///   WASD          → Move (Thrust en Y, Yaw en X)
    ///   Q / E         → Roll izquierda / derecha
    ///   Espacio/Ctrl  → Vertical arriba / abajo
    ///   Mouse Delta   → Look (Pitch)
    ///   Shift Izq     → Boost
    ///   Mouse 0 / 1   → FirePrimary / FireSecondary
    ///   F             → FireBomb
    ///   Mouse 2       → Zoom
    ///   T             → Interact
    ///   Tab / Esc     → Menu
    ///   R             → Reload
    ///   Scroll        → ScrollScanner
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
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<InputProvider>();

                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[InputProvider]");
                        _instance = go.AddComponent<InputProvider>();
                        Debug.Log("[InputProvider] Creado automáticamente como Singleton.");
                    }
                }
                return _instance;
            }
        }

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

            // Forzar que sea objeto raíz antes de DontDestroyOnLoad,
            // necesario si este GameObject quedó como hijo de otro en la escena.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeInputActions();
        }

        private void OnDestroy()
        {
            _actions?.Disable();
            _actions?.Dispose();
        }

        // ─────────────────────────────────────────
        //  Input Actions Asset (New Input System)
        // ─────────────────────────────────────────
        private StarWeaverInputActions _actions;

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
        //  API Pública — OrbitalPlayerDriver
        // ─────────────────────────────────────────

        /// <summary>WASD: x = Yaw (A/D), y = Thrust (W/S)</summary>
        public Vector2 GetMovementInput() => _moveAction.ReadValue<Vector2>();

        /// <summary>Espacio (+1) / Ctrl Izq (-1)</summary>
        public float GetVerticalMovement() => _verticalAction.ReadValue<float>();

        /// <summary>Q (-1) / E (+1)</summary>
        public float GetRollInput() => _rollAction.ReadValue<float>();

        /// <summary>Delta del mouse escalado por sensibilidad: x = horizontal, y = vertical (Pitch)</summary>
        public Vector2 GetLookDelta()
        {
            Vector2 raw = _lookAction.ReadValue<Vector2>();
            return new Vector2(raw.x * mouseSensitivityX, raw.y * mouseSensitivityY);
        }

        /// <summary>Shift Izquierdo sostenido</summary>
        public bool IsBoostHeld() => _boostAction.IsPressed();

        /// <summary>Mouse Botón 0 sostenido</summary>
        public bool IsFirePrimaryHeld() => _firePrimaryAction.IsPressed();

        /// <summary>Mouse Botón 1 sostenido</summary>
        public bool IsFireSecondaryHeld() => _fireSecondaryAction.IsPressed();

        /// <summary>F — one-shot por frame (equivalente a GetKeyDown)</summary>
        public bool IsFireBombPressed() => _fireBombAction.WasPressedThisFrame();

        // ─────────────────────────────────────────
        //  API Pública — funciones extra
        // ─────────────────────────────────────────

        /// <summary>
        /// Scroll del mouse: +1 = siguiente scanner, -1 = anterior, 0 = sin cambio.
        /// </summary>
        public int GetScannerScrollDelta()
        {
            float scroll = _scrollScannerAction.ReadValue<float>();
            if (scroll > 0.05f) return 1;
            if (scroll < -0.05f) return -1;
            return 0;
        }

        /// <summary>Botón del scroll (Mouse 2) — toggle de zoom</summary>
        public bool IsZoomPressed() => _zoomAction.WasPressedThisFrame();

        /// <summary>T — interactuar con el entorno</summary>
        public bool IsInteractPressed() => _interactAction.WasPressedThisFrame();

        /// <summary>Tab o Esc — abrir/cerrar menú</summary>
        public bool IsMenuPressed() => _menuAction.WasPressedThisFrame();

        /// <summary>R — reload / reservado</summary>
        public bool IsRPressed() => _reloadAction.WasPressedThisFrame();

        // ─────────────────────────────────────────
        //  Habilitar / Deshabilitar controles
        // ─────────────────────────────────────────

        /// <summary>Deshabilita todos los controles de la nave (ej: al abrir el menú de pausa).</summary>
        public void DisableShipControls() => _actions.Ship.Disable();

        /// <summary>Vuelve a habilitar los controles de la nave.</summary>
        public void EnableShipControls() => _actions.Ship.Enable();
    }
}