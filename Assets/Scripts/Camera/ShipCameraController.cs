using UnityEngine;
using Unity.Cinemachine;
using StarWeaver.Input;
using StarWeaver.Core;

namespace StarWeaver.Camera
{
    /// <summary>
    /// Gestiona los modos de cámara de la nave usando Cinemachine 3.x.
    ///
    /// ARQUITECTURA:
    /// Este script NO mueve la cámara directamente. Solo controla las prioridades
    /// de las CinemachineCamera — Cinemachine hace el blend y el seguimiento.
    ///
    /// La rotación orbital con el mouse se implementa manipulando el eje horizontal
    /// de CinemachineOrbitalFollow directamente desde InputProvider, manteniendo
    /// la arquitectura de input centralizada del proyecto.
    ///
    /// SETUP EN UNITY (ver sección XML al final del archivo):
    /// 1. Crear dos CinemachineCamera en la jerarquía bajo "Camaras".
    /// 2. Crear un CameraRoot (Transform vacío) hijo de la nave.
    /// 3. Asignar las referencias en el Inspector de este componente.
    /// 4. Colocar este script en el GameObject de la nave.
    /// </summary>
    public class ShipCameraController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        //  Inspector — Cámaras
        // ─────────────────────────────────────────────────────────────

        [Header("Cámaras Cinemachine")]
        [Tooltip("CinemachineCamera configurada con OrbitalFollow para tercera persona.")]
        [SerializeField] private CinemachineCamera thirdPersonCamera;

        [Tooltip("CinemachineCamera configurada con HardLockToTarget para cockpit.")]
        [SerializeField] private CinemachineCamera cockpitCamera;

        [Header("Targets")]
        [Tooltip("Transform vacío hijo de la nave. Pivot del sistema de cámara. " +
                 "NO debe heredar la rotación de roll de la nave — ver Setup.")]
        [SerializeField] private Transform cameraRoot;

        [Tooltip("Transform que define la posición del punto de vista cockpit " +
                 "(encima/detrás del personaje, hijo de la nave).")]
        [SerializeField] private Transform cockpitTarget;

        [Header("Tercera Persona — Configuración Orbital")]
        [Tooltip("Distancia de seguimiento detrás de la nave.")]
        [SerializeField] private float thirdPersonDistance = 15f;

        [Tooltip("Altura del punto de seguimiento sobre la nave.")]
        [SerializeField] private float thirdPersonHeight = 4f;

        [Tooltip("Velocidad de rotación orbital del mouse (grados/segundo por unidad de delta).")]
        [SerializeField] private float orbitSensitivity = 120f;

        [Tooltip("Velocidad del eje vertical del mouse en modo orbital.")]
        [SerializeField] private float orbitVerticalSensitivity = 80f;

        [Tooltip("Límite inferior del eje vertical (grados). Evita que la cámara pase por debajo de la nave.")]
        [SerializeField][Range(-80f, 0f)] private float verticalMin = -30f;

        [Tooltip("Límite superior del eje vertical (grados). Evita que la cámara pase por encima en exceso.")]
        [SerializeField][Range(0f, 80f)] private float verticalMax = 60f;

        [Header("Prioridades")]
        [Tooltip("Prioridad de la cámara activa. La inactiva usa 0.")]
        [SerializeField] private int activePriority = 10;

        // ─────────────────────────────────────────────────────────────
        //  Estado interno
        // ─────────────────────────────────────────────────────────────

        public enum CameraMode { ThirdPerson, Cockpit }

        private CameraMode _currentMode = CameraMode.ThirdPerson;
        private CinemachineOrbitalFollow _orbitalFollow;

        // Ángulos acumulados para el control manual del orbit
        private float _orbitYaw = 0f;
        private float _orbitPitch = 20f; // Empieza con la cámara ligeramente arriba

        // ─────────────────────────────────────────────────────────────
        //  Ciclo de vida
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            ValidateReferences();
            SetupCameraTargets();
            CacheComponents();
        }

        private void Start()
        {
            ActivateMode(_currentMode);
        }

        private void LateUpdate()
        {
            // LateUpdate garantiza que la nave ya terminó su FixedUpdate/Update
            // antes de que calculemos la posición de la cámara.
            if (_currentMode == CameraMode.ThirdPerson)
                UpdateOrbitalInput();

            HandleCameraSwitch();
        }

        // ─────────────────────────────────────────────────────────────
        //  Setup
        // ─────────────────────────────────────────────────────────────

        private void ValidateReferences()
        {
            if (thirdPersonCamera == null)
                Debug.LogError("[ShipCameraController] Falta asignar 'thirdPersonCamera'.", this);
            if (cockpitCamera == null)
                Debug.LogError("[ShipCameraController] Falta asignar 'cockpitCamera'.", this);
            if (cameraRoot == null)
                Debug.LogError("[ShipCameraController] Falta asignar 'cameraRoot'.", this);
        }

        private void SetupCameraTargets()
        {
            // Asignamos los Follow y LookAt de cada cámara por código,
            // así no dependemos de que estén configurados manualmente en el Inspector.
            if (thirdPersonCamera != null && cameraRoot != null)
            {
                thirdPersonCamera.Follow = cameraRoot;
                thirdPersonCamera.LookAt = cameraRoot;
            }

            if (cockpitCamera != null)
            {
                Transform cockpitT = cockpitTarget != null ? cockpitTarget : transform;
                cockpitCamera.Follow = cockpitT;
                cockpitCamera.LookAt = cockpitT;
            }
        }

        private void CacheComponents()
        {
            if (thirdPersonCamera != null)
                _orbitalFollow = thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();

            if (_orbitalFollow == null && thirdPersonCamera != null)
                Debug.LogWarning("[ShipCameraController] La thirdPersonCamera no tiene " +
                                 "CinemachineOrbitalFollow. La rotación orbital no funcionará.", this);
        }

        // ─────────────────────────────────────────────────────────────
        //  Input orbital (LateUpdate)
        // ─────────────────────────────────────────────────────────────

        private void UpdateOrbitalInput()
        {
            if (_orbitalFollow == null) return;

            InputProvider input = InputProvider.Instance;
            if (input == null) return;

            Vector2 look = input.GetLookDelta();

            // Acumulamos los ángulos manualmente para tener control total
            // sobre los límites y la sensibilidad, sin depender del
            // CinemachineInputAxisController (que tiene su propio escalado).
            _orbitYaw += look.x * orbitSensitivity * Time.deltaTime;
            _orbitPitch -= look.y * orbitVerticalSensitivity * Time.deltaTime;
            _orbitPitch = Mathf.Clamp(_orbitPitch, verticalMin, verticalMax);

            // Aplicamos directamente al componente orbital de Cinemachine.
            // HorizontalAxis y VerticalAxis son los ejes de control en CM 3.x.
            _orbitalFollow.HorizontalAxis.Value = _orbitYaw;
            _orbitalFollow.VerticalAxis.Value = _orbitPitch;
        }

        // ─────────────────────────────────────────────────────────────
        //  Cambio de modo
        // ─────────────────────────────────────────────────────────────

        private void HandleCameraSwitch()
        {
            InputProvider input = InputProvider.Instance;
            if (input == null) return;

            // Por ahora: zoom/clic del scroll para cambiar de cámara.
            // Podés reasignar esto a cualquier otro input.
            if (input.IsZoomPressed())
                ToggleCameraMode();
        }

        public void ToggleCameraMode()
        {
            CameraMode next = _currentMode == CameraMode.ThirdPerson
                ? CameraMode.Cockpit
                : CameraMode.ThirdPerson;

            ActivateMode(next);
        }

        public void ActivateMode(CameraMode mode)
        {
            _currentMode = mode;

            // Cinemachine activa la cámara con prioridad más alta.
            // La que pierde prioridad queda en 0 y se hace invisible para el blender.
            bool thirdPersonActive = mode == CameraMode.ThirdPerson;

            if (thirdPersonCamera != null)
                thirdPersonCamera.Priority = thirdPersonActive ? activePriority : 0;

            if (cockpitCamera != null)
                cockpitCamera.Priority = thirdPersonActive ? 0 : activePriority;

            Debug.Log($"[ShipCameraController] Modo de cámara: {mode}");
        }

        // ─────────────────────────────────────────────────────────────
        //  CameraRoot — actualizar posición sin heredar roll
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            // El CameraRoot sigue la posición de la nave pero NO hereda su rotación.
            // Esto evita que la cámara de tercera persona gire cuando la nave hace roll.
            // Si querés que la cámara sí acompañe el roll, comentá estas líneas
            // y simplemente dejá al CameraRoot como hijo normal de la nave.
            if (cameraRoot != null)
                cameraRoot.position = transform.position;
        }

        // ─────────────────────────────────────────────────────────────
        //  Validación en Editor
        // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Recordatorio visual en el Inspector cuando faltan referencias.
            if (thirdPersonCamera == null || cockpitCamera == null || cameraRoot == null)
                Debug.LogWarning("[ShipCameraController] Faltan referencias. Ver tooltips en el Inspector.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraRoot == null) return;

            // Dibuja el punto de pivot de la cámara en la vista de escena.
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cameraRoot.position, 0.5f);
            Gizmos.DrawLine(transform.position, cameraRoot.position);
        }
#endif
    }
}

