using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// Provides a centralized, singleton access point for player input using Unity's Input System, configured via Inspector KeyCode bindings.
    /// </summary>
    public class InputProvider : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the InputProvider.
        /// </summary>
        public static InputProvider Instance { get; private set; }

        /// <summary>
        /// Defines the mapping between game actions and specific keyboard/mouse inputs.
        /// </summary>
        [System.Serializable]
        public class KeyBindings
        {
            [Header("Starship Flight Controls")]
            [Tooltip("Key for forward thrust (W).")]
            public KeyCode forwardThrust = KeyCode.W;
            [Tooltip("Key for backward thrust/reverse (S).")]
            public KeyCode backwardThrust = KeyCode.S;
            [Tooltip("Key for yaw left (A). Also used for character movement left.")]
            public KeyCode leftThrust = KeyCode.A;
            [Tooltip("Key for yaw right (D). Also used for character movement right.")]
            public KeyCode rightThrust = KeyCode.D;
            [Tooltip("Key for vertical thrust upwards (Space).")]
            public KeyCode verticalThrustUp = KeyCode.Space;
            [Tooltip("Key for vertical thrust downwards (Left Control).")]
            public KeyCode verticalThrustDown = KeyCode.LeftControl;
            [Tooltip("Key for rolling the ship left (Q).")]
            public KeyCode rollLeft = KeyCode.Q;
            [Tooltip("Key for rolling the ship right (E).")]
            public KeyCode rollRight = KeyCode.E;
            [Tooltip("Key to activate the boost (Left Shift).")]
            public KeyCode boost = KeyCode.LeftShift;

            [Header("Character Controls (For potential FPS integration)")]
            [Tooltip("Key for character jump (Space).")]
            public KeyCode jump = KeyCode.Space;
            [Tooltip("Key for character run/sprint (Left Shift).")]
            public KeyCode run = KeyCode.LeftShift;
            [Tooltip("Key for character crouch (C).")]
            public KeyCode crouch = KeyCode.C;

            [Header("System & Action Controls")]
            [Tooltip("Key to fire/drop the bombardment weapon (G).")]
            public KeyCode fireBomb = KeyCode.G;
            [Tooltip("Key to zoom the camera in (Z).")]
            public KeyCode zoomIn = KeyCode.Z;
            [Tooltip("Key to zoom the camera out (X).")]
            public KeyCode zoomOut = KeyCode.X;
            [Tooltip("Key to pause the game (Escape).")]
            public KeyCode pause = KeyCode.Escape;

            [Header("Weapon Controls")]
            [Tooltip("Input for Primary Fire (e.g., Lasers). Default: Left Mouse Button.")]
            public KeyCode firePrimary = KeyCode.Mouse0;
            [Tooltip("Input for Secondary Fire (e.g., Rockets/Missiles). Default: F key.")]
            public KeyCode fireSecondary = KeyCode.F;

            [Tooltip("Input to hold for engaging a target (Auto-Aim/Missile Lock). Default: Right Mouse Button.")]
            public KeyCode aimMode = KeyCode.Mouse1;
        }

        [Tooltip("Configure the desired keyboard and mouse controls for the game actions here.")]
        public KeyBindings keyBindings;

        private InputActionAsset _inputAsset;
        private InputActionMap _playerMap;

        [Header("Event Broadcasting")]
        [Tooltip("If true, the provider will broadcast events (like OnFirePrimaryPressed) when inputs are detected. Disable this if another system is handling these specific events.")]
        public bool broadcastFireEvents = true;

        /// <summary>
        /// Event invoked when the primary fire input is detected (if broadcastFireEvents is enabled).
        /// </summary>
        public static event Action OnFirePrimaryPressed;

        // Input Actions
        public InputAction MoveAction { get; private set; }
        public InputAction VerticalMovementAction { get; private set; }
        public InputAction RollAction { get; private set; }
        public InputAction ZoomAction { get; private set; }
        public InputAction LookAction { get; private set; }
        public InputAction PauseAction { get; private set; }
        public InputAction RunAction { get; private set; }
        public InputAction JumpAction { get; private set; }
        public InputAction CrouchAction { get; private set; }
        public InputAction BoostAction { get; private set; }
        public InputAction FireBombAction { get; private set; }
        public InputAction ScrollAction { get; private set; }
        public InputAction FirePrimaryAction { get; private set; }
        public InputAction FireSecondaryAction { get; private set; }
        public InputAction AimModeAction { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DefineActionsFromInspector();
        }

        private void OnEnable()
        {
            if (_inputAsset != null)
            {
                _inputAsset.Enable();
            }
        }

        private void OnDisable()
        {
            if (_inputAsset != null)
            {
                _inputAsset.Disable();
            }
        }

        private void Update()
        {
            if (!broadcastFireEvents)
            {
                return;
            }
            if (IsFirePrimaryHeld())
            {
                OnFirePrimaryPressed?.Invoke();
            }
        }

        /// <summary>
        /// Dynamically generates the InputActionAsset and binds the actions based on the KeyBindings configured in the Inspector.
        /// </summary>
        private void DefineActionsFromInspector()
        {
            if (_inputAsset != null) return;

            _inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            _playerMap = _inputAsset.AddActionMap("Player");

            // Movement Actions
            MoveAction = _playerMap.AddAction("Move", InputActionType.Value);
            MoveAction.AddCompositeBinding("Dpad")
                .With("Up", KeyCodeToPath(keyBindings.forwardThrust))
                .With("Down", KeyCodeToPath(keyBindings.backwardThrust))
                .With("Left", KeyCodeToPath(keyBindings.leftThrust))
                .With("Right", KeyCodeToPath(keyBindings.rightThrust));

            VerticalMovementAction = _playerMap.AddAction("VerticalMovement", InputActionType.Value);
            VerticalMovementAction.AddCompositeBinding("1DAxis")
               .With("Positive", KeyCodeToPath(keyBindings.verticalThrustUp))
               .With("Negative", KeyCodeToPath(keyBindings.verticalThrustDown));

            RollAction = _playerMap.AddAction("Roll", InputActionType.Value);
            RollAction.AddCompositeBinding("1DAxis")
                .With("Positive", KeyCodeToPath(keyBindings.rollLeft))
                .With("Negative", KeyCodeToPath(keyBindings.rollRight));

            ZoomAction = _playerMap.AddAction("Zoom", InputActionType.Value);
            ZoomAction.AddCompositeBinding("1DAxis")
                .With("Positive", KeyCodeToPath(keyBindings.zoomIn))
                .With("Negative", KeyCodeToPath(keyBindings.zoomOut));

            JumpAction = _playerMap.AddAction("Jump", InputActionType.Button, KeyCodeToPath(keyBindings.jump));
            RunAction = _playerMap.AddAction("Run", InputActionType.Button, KeyCodeToPath(keyBindings.run));
            CrouchAction = _playerMap.AddAction("Crouch", InputActionType.Button, KeyCodeToPath(keyBindings.crouch));

            // Look Action
            LookAction = _playerMap.AddAction("Look", InputActionType.Value, "<Mouse>/delta");

            // System Actions
            PauseAction = _playerMap.AddAction("Pause", InputActionType.Button, KeyCodeToPath(keyBindings.pause));

            BoostAction = _playerMap.AddAction("Boost", InputActionType.Button, KeyCodeToPath(keyBindings.boost));
            FireBombAction = _playerMap.AddAction("FireBomb", InputActionType.Button, KeyCodeToPath(keyBindings.fireBomb));
            ScrollAction = _playerMap.AddAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");

            // Weapon Actions
            FirePrimaryAction = _playerMap.AddAction("FirePrimary", InputActionType.Button);
            AddBinding(FirePrimaryAction, keyBindings.firePrimary, "<Mouse>/leftButton");

            FireSecondaryAction = _playerMap.AddAction("FireSecondary", InputActionType.Button);
            AddBinding(FireSecondaryAction, keyBindings.fireSecondary, null); // No mouse default

            AimModeAction = _playerMap.AddAction("AimMode", InputActionType.Button);
            AddBinding(AimModeAction, keyBindings.aimMode, "<Mouse>/rightButton");

            if (this.enabled)
            {
                _inputAsset.Enable();
            }
        }

        /// <summary>
        /// Helper method to add bindings to an InputAction, handling keyboard and mouse inputs correctly.
        /// </summary>
        private void AddBinding(InputAction action, KeyCode key, string defaultMouseBinding)
        {
            if (key == KeyCode.Mouse0)
            {
                action.AddBinding("<Mouse>/leftButton");
            }
            else if (key == KeyCode.Mouse1)
            {
                action.AddBinding("<Mouse>/rightButton");
            }
            else if (key == KeyCode.Mouse2)
            {
                action.AddBinding("<Mouse>/middleButton");
            }
            else if (key != KeyCode.None)
            {
                action.AddBinding(KeyCodeToPath(key));
            }
            else if (!string.IsNullOrEmpty(defaultMouseBinding))
            {
                // Fallback to the hardcoded default if key is None
                action.AddBinding(defaultMouseBinding);
            }
        }

        /// <summary>
        /// Converts a Unity KeyCode enum value to the corresponding Input System path string.
        /// </summary>
        private string KeyCodeToPath(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            string keyName = key.ToString();
            keyName = key switch
            {
                KeyCode.LeftControl => "leftCtrl",
                KeyCode.RightControl => "rightCtrl",
                KeyCode.LeftShift => "leftShift",
                KeyCode.RightShift => "rightShift",
                KeyCode.LeftAlt => "leftAlt",
                KeyCode.RightAlt => "rightAlt",
                KeyCode.Escape => "escape",
                _ => keyName.ToLower()
            };
            return $"<Keyboard>/{keyName}";
        }

        #region Public Accessors
        // Provides methods to read the state of the configured input actions.

        public bool IsBoostHeld() => BoostAction.IsPressed();
        public bool IsFireBombPressed() => FireBombAction.WasPressedThisFrame();

        // Weapon Accessors
        public bool IsFirePrimaryHeld() => FirePrimaryAction.IsPressed();
        public bool IsFireSecondaryHeld() => FireSecondaryAction.IsPressed();

        // Targeting and Aiming Accessors
        public bool IsAimModeHeld() => AimModeAction.IsPressed();

        // Look and Movement Accessors
        public Vector2 GetLookDelta() => LookAction.ReadValue<Vector2>() * 0.08f; // 0.08f is a scaling factor for mouse delta sensitivity.
        public Vector2 GetMovementInput() => MoveAction.ReadValue<Vector2>();
        public float GetVerticalMovement() => VerticalMovementAction.ReadValue<float>();
        public float GetRollInput() => RollAction.ReadValue<float>();
        public float GetZoomInput() => ZoomAction.ReadValue<float>();

        // Character Action Accessors
        public bool IsRunHeld() => RunAction.IsPressed();
        public bool IsJumpPressed() => JumpAction.WasPressedThisFrame();
        public bool IsCrouchHeld() => CrouchAction.IsPressed();

        // System Accessors
        public bool IsPausePressed() => PauseAction.WasPressedThisFrame();

        public float GetMouseScroll()
        {
            float scroll = ScrollAction.ReadValue<float>();
            // Normalize the scroll value (typically 120 per step)
            return Mathf.Clamp(scroll / 120f, -1f, 1f);
        }
        #endregion
    }
}