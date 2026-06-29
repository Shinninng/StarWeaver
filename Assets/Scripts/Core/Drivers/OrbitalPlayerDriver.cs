using UnityEngine;
using StarWeaver.Core;
using StarWeaver.Input;

namespace StarWeaver.Core
{
    [RequireComponent(typeof(ShipProperties))]
    public class OrbitalPlayerDriver : MonoBehaviour, IShipDriver
    {
        private OrbitalStarshipController _controller;
        private ShipProperties _properties;

        [Header("Configuración de Apuntado")]
        [SerializeField] private float maxAimDistance = 1000f;

        // Usamos UnityEngine.Camera explícitamente para solucionar el error de compilación
        private UnityEngine.Camera _playerCamera;

        void Awake()
        {
            _properties = GetComponent<ShipProperties>();
            _playerCamera = UnityEngine.Camera.main;
        }

        public void AssignController(OrbitalStarshipController controller)
        {
            _controller = controller;
        }

        public void ReleaseController()
        {
            if (_controller != null)
            {
                _controller = null;
            }
        }

        public bool IsActivelyControlling()
        {
            return true;
        }

        public string GetControlDescription()
        {
            return "Control de Nave Orbital adaptado para InputProvider.";
        }

        public ShipInputState GetDesiredInputState()
        {
            ShipInputState state = new ShipInputState();

            // Usamos el Singleton nativo de tu InputProvider
            if (InputProvider.Instance == null) return state;

            // 1. Mapeo de Empuje y Guiñada (Yaw) desde el teclado (W, S, A, D)
            Vector2 movement = InputProvider.Instance.GetMovementInput();
            state.Thrust = movement.y;
            state.Yaw = movement.x;

            // 2. Desplazamiento Vertical (Espacio / Control Izquierdo) y Alabeo (Roll - Q/E)
            state.Vertical = InputProvider.Instance.GetVerticalMovement();
            state.Roll = InputProvider.Instance.GetRollInput();

            // 3. Rotación de Cabeceo (Pitch) usando el Mouse
            Vector2 lookDelta = InputProvider.Instance.GetLookDelta();
            state.Pitch = lookDelta.y;

            // 4. Modificadores (Boost y Armas) emparejados con tus métodos reales del asset
            state.Boost = InputProvider.Instance.IsBoostHeld();
            state.FirePrimary = InputProvider.Instance.IsFirePrimaryHeld();
            state.FireSecondary = InputProvider.Instance.IsFireSecondaryHeld();
            state.FireBomb = InputProvider.Instance.IsFireBombPressed();

            // 5. Cálculo del punto de mira en el espacio 3D
            state.AimPosition = CalculateAimPosition();

            return state;
        }

        private Vector3 CalculateAimPosition()
        {
            if (_playerCamera == null) _playerCamera = UnityEngine.Camera.main;
            if (_playerCamera == null) return transform.position + transform.forward * maxAimDistance;

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = _playerCamera.ScreenPointToRay(screenCenter);
            return ray.origin + ray.direction * maxAimDistance;
        }
    }
}