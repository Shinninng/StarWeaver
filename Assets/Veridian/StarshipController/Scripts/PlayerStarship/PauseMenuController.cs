using UnityEngine;
using UnityEngine.UI;
using Veridian.Starship.Core;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// Manages the visibility and interaction of the pause menu UI, and handles the pause input by communicating with the GameStateManager.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI Configuration")]
        [Tooltip("The root GameObject of the pause menu UI (e.g., the main panel). This will be activated when paused and deactivated when resumed.")]
        public GameObject pauseMenuRoot;

        [Header("Interactive Elements")]
        [Tooltip("The UI Button used to resume the game.")]
        public Button resumeButton;
        [Tooltip("The UI Button used to restart the current game session/scene.")]
        public Button restartButton;

        private InputProvider _input;
        private GameStateManager _gameStateManager;

        void Start()
        {
            // Find the InputProvider instance.
            _input = InputProvider.Instance;
            if (_input == null)
            {
                Debug.LogError("PauseMenuController: InputProvider instance not found. Pause input will not work.", this);
            }

            // Find the GameStateManager instance.
            _gameStateManager = GameStateManager.Instance;
            if (_gameStateManager == null)
            {
                // Fallback: If not initialized yet (e.g., Awake order issue), try finding it in the scene.
                _gameStateManager = FindFirstObjectByType<GameStateManager>();
            }

            if (_gameStateManager == null)
            {
                // If still not found, we must create a fallback to ensure functionality.
                Debug.LogWarning("PauseMenuController: GameStateManager not found. Creating fallback instance.");
                // Ensure the Instance is set by creating the component which triggers its Awake().
                new GameObject("GameStateManager_Fallback").AddComponent<GameStateManager>();
                _gameStateManager = GameStateManager.Instance;
            }


            // Ensure the pause menu is hidden at the start.
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(false);
            }

            // Hook up button events.
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumeClicked);
            }
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            // Subscribe to game state changes.
            _gameStateManager.OnGamePaused += ShowPauseMenu;
            _gameStateManager.OnGameResumed += HidePauseMenu;
        }

        void OnDestroy()
        {
            // Unsubscribe from game state changes.
            if (_gameStateManager != null)
            {
                _gameStateManager.OnGamePaused -= ShowPauseMenu;
                _gameStateManager.OnGameResumed -= HidePauseMenu;
            }
        }

        void Update()
        {
            // Check for pause input.
            if (_input != null && _input.IsPausePressed())
            {
                TogglePause();
            }
        }

        /// <summary>
        /// Toggles the game's pause state via the GameStateManager.
        /// </summary>
        private void TogglePause()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.TogglePause();
            }
        }

        /// <summary>
        /// Shows the pause menu UI.
        /// </summary>
        private void ShowPauseMenu()
        {
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Hides the pause menu UI.
        /// </summary>
        private void HidePauseMenu()
        {
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Handles the resume button click event.
        /// </summary>
        private void OnResumeClicked()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.ResumeGame();
            }
        }

        /// <summary>
        /// Handles the restart button click event.
        /// </summary>
        private void OnRestartClicked()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.RestartGame();
            }
        }
    }
}