using UnityEngine;
using StarWeaver.Core; // Conectamos con el núcleo de físicas

namespace StarWeaver.Player
{
    [RequireComponent(typeof(ShipProperties))]
    [RequireComponent(typeof(OrbitalStarshipController))]
    public class PlayerShipDriver : MonoBehaviour, IShipDriver
    {
        private OrbitalStarshipController controller;
        private ShipProperties properties;

        private bool isActivelyControlling = true;

        [Header("Camera Reference")]
        [SerializeField] private UnityEngine.Camera _playerCamera; // <--UnityEngine.Camera
        [SerializeField] private float maxAimDistance = 1000f;

        // Aquí guardarías las referencias a tus Input Actions del nuevo Input System si usas código
        // Ejemplo: private InputAction moveAction;

        void Awake()
        {
            properties = GetComponent<ShipProperties>();
            controller = GetComponent<OrbitalStarshipController>();
        }

        void Start()
        {
            if (_playerCamera == null)
            {
                _playerCamera = UnityEngine.Camera.main; // <-- UnityEngine.Camera
            }

            // Registro de gravedad
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                StarWeaver.Core.OrbitalManager.RegisterBody(rb);
            }
        }

        #region IShipDriver Implementation

        public void AssignController(OrbitalStarshipController targetController)
        {
            controller = targetController;
        }

        public void ReleaseController()
        {
            controller = null;
        }

        public ShipInputState GetDesiredInputState()
        {
            ShipInputState state = new ShipInputState();

            if (!isActivelyControlling) return state;

            // =========================================================================
            // REEMPLAZO CON EL INPUT SYSTEM NUEVO:
            // Aquí asignarás los valores leyendo tus Actions. 
            // Por ahora, te dejo un mapeo clásico provisional para que puedas probar el movimiento al instante.
            // =========================================================================

            // Empuje longitudinal (W / S)
            if (UnityEngine.Input.GetKey(KeyCode.W)) state.Thrust = 1f;
            else if (UnityEngine.Input.GetKey(KeyCode.S)) state.Thrust = -1f;

            // Empuje vertical (Espacio / Control Izquierdo)
            if (UnityEngine.Input.GetKey(KeyCode.Space)) state.Vertical = 1f;
            else if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) state.Vertical = -1f;

            // Rotaciones (Ejes de giro rápidos)
            // Alabeo / Roll (Q / E)
            if (UnityEngine.Input.GetKey(KeyCode.Q)) state.Roll = -1f;
            else if (UnityEngine.Input.GetKey(KeyCode.E)) state.Roll = 1f;

            // Cabeceo y Guiñada (Pitch y Yaw) controlados tradicionalmente por Flechas o Mouse
            state.Pitch = UnityEngine.Input.GetAxis("Vertical"); // Flechas Arriba/Abajo o Mouse Y
            state.Yaw = UnityEngine.Input.GetAxis("Horizontal");  // Flechas Izquierda/Derecha o Mouse X

            // Boost (Shift Izquierdo)
            state.Boost = UnityEngine.Input.GetKey(KeyCode.LeftShift);

            // Cálculo del punto de mira en el espacio
            state.AimPosition = CalculateAimPosition();
            state.IsTargetEngaged = false;

            return state;
        }

        public bool IsActivelyControlling() => isActivelyControlling;

        public string GetControlDescription()
        {
            return "Controles Orbitales de StarWeaver: W/S Empuje, Q/E Giro, Mouse/Flechas Dirección.";
        }

        #endregion

        private Vector3 CalculateAimPosition()
        {
            if (_playerCamera == null)
            {
                return transform.position + transform.forward * maxAimDistance;
            }

            // Create a ray from the center of the screen.
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = _playerCamera.ScreenPointToRay(screenCenter); // Esto ahora va a funcionar solo al arreglar el tipo arriba

            return ray.origin + ray.direction * maxAimDistance;
        }
    }
}