using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Interface for components that provide the current activity status of the player.
    /// </summary>
    public interface IPlayerActivityProvider
    {
        /// <summary>
        /// Indicates if the player is currently in control and actively playing (e.g., flying the ship).
        /// When true, the GameStateManager will enforce the Default cursor state (Locked and Hidden).
        /// This state is required for both normal flight and aiming modes to ensure uninterrupted mouse input.
        /// </summary>
        bool IsPlayerActivelyPlaying { get; }
    }

    /// <summary>
    /// Manages the overall game state, including pause status, time scale, and cursor visibility/locking.
    /// Functions as a singleton and enforces strict rules for the cursor based on player activity and application focus.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the GameStateManager.
        /// </summary>
        public static GameStateManager Instance { get; private set; }

        /// <summary>
        /// Defines the possible states of the game simulation.
        /// </summary>
        public enum GameState
        {
            Playing,
            Paused
        }

        /// <summary>
        /// The current state of the game.
        /// </summary>
        public GameState CurrentState { get; private set; } = GameState.Playing;

        /// <summary>
        /// Indicates if the game is currently paused.
        /// </summary>
        public bool IsPaused => CurrentState == GameState.Paused;

        /// <summary>
        /// Invoked when the game is paused.
        /// </summary>
        public event Action OnGamePaused;
        /// <summary>
        /// Invoked when the game is resumed.
        /// </summary>
        public event Action OnGameResumed;

        private IPlayerActivityProvider _playerActivityProvider;

        // Tracks if the application currently has focus.
        private bool _isApplicationFocused = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Initialize focus state based on current application status.
            _isApplicationFocused = Application.isFocused;
            // Optional: DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // Ensure the state is initialized correctly at the start of the first frame.
            UpdateSystemState();
        }

        void Update()
        {
            // Continuously enforce the correct system state every frame.
            // This ensures the cursor adheres strictly to the rules, even if external factors or other scripts attempt interference.
            UpdateSystemState();
        }

        /// <summary>
        /// Registers a component as the provider of player activity status.
        /// </summary>
        /// <param name="provider">The IPlayerActivityProvider instance.</param>
        public void RegisterPlayerActivityProvider(IPlayerActivityProvider provider)
        {
            if (_playerActivityProvider != null && _playerActivityProvider != provider)
            {
                Debug.LogWarning("GameStateManager: Overriding existing IPlayerActivityProvider.");
            }
            _playerActivityProvider = provider;
            // Immediately update state when provider changes.
            UpdateSystemState();
        }

        /// <summary>
        /// Unregisters the current player activity provider.
        /// </summary>
        /// <param name="provider">The IPlayerActivityProvider instance to unregister.</param>
        public void UnregisterPlayerActivityProvider(IPlayerActivityProvider provider)
        {
            if (_playerActivityProvider == provider)
            {
                _playerActivityProvider = null;
                // Immediately update state when provider changes.
                UpdateSystemState();
            }
        }

        /// <summary>
        /// Toggles the game between Playing and Paused states.
        /// </summary>
        public void TogglePause()
        {
            if (CurrentState == GameState.Playing)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// Sets the game state to Paused.
        /// </summary>
        public void PauseGame()
        {
            if (CurrentState == GameState.Paused) return;

            CurrentState = GameState.Paused;
            // Call UpdateSystemState() for immediate response.
            UpdateSystemState();
            OnGamePaused?.Invoke();
        }

        /// <summary>
        /// Sets the game state to Playing.
        /// </summary>
        public void ResumeGame()
        {
            if (CurrentState == GameState.Playing) return;

            CurrentState = GameState.Playing;
            // Call UpdateSystemState() for immediate response.
            UpdateSystemState();
            OnGameResumed?.Invoke();
        }

        /// <summary>
        /// Restarts the current game session by reloading the active scene.
        /// </summary>
        public void RestartGame()
        {
            // Ensure the game is resumed and time scale is explicitly reset before reloading.
            ResumeGame();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Evaluates the strict rules for time scale and cursor state and applies them.
        /// This method is called every frame and when state changes occur.
        /// </summary>
        private void UpdateSystemState()
        {
            // 1. Determine Time Scale
            //float desiredTimeScale = (CurrentState == GameState.Paused) ? 0f : 1f;

            // Rule 1: If the application loses focus, unlock the cursor.
            if (!_isApplicationFocused)
            {
                ApplyCursorState(CursorLockMode.None);
                return;
            }

            // Rule 2: Determine cursor lock based on player activity.
            bool isPlayerActive = _playerActivityProvider != null && _playerActivityProvider.IsPlayerActivelyPlaying;

            if (isPlayerActive)
            {
                // Player is active (flying/aiming), cursor must be locked.
                ApplyCursorState(CursorLockMode.Locked);
            }
            else
            {
                // Player is not active (e.g., in a menu, or ship destroyed), cursor is unlocked but STILL hidden.
                ApplyCursorState(CursorLockMode.None);
            }
        }

        /// <summary>
        /// Applies the desired cursor state using standard Unity API calls.
        /// Enforces that the cursor is always hidden in this specific implementation.
        /// </summary>
        /// <param name="lockMode">The desired CursorLockMode.</param>
        private void ApplyCursorState(CursorLockMode lockMode)
        {
            // --- Always hide the cursor ---
            // Optimization: Avoid redundant calls if already hidden.
            if (Cursor.visible)
            {
                Cursor.visible = false;
            }
            // -----------------------------

            // Apply the requested lock state
            // Optimization: Avoid redundant calls if already in the desired state.
            if (Cursor.lockState != lockMode)
            {
                Cursor.lockState = lockMode;
            }
        }



        /// <summary>
        /// Handles application focus changes, updating the system state immediately.
        /// </summary>
        /// <param name="hasFocus">True if the application gained focus, false otherwise.</param>
        void OnApplicationFocus(bool hasFocus)
        {
            if (_isApplicationFocused == hasFocus) return;

            _isApplicationFocused = hasFocus;
            // Immediately update the system state when focus changes.
            UpdateSystemState();
        }
    }
}