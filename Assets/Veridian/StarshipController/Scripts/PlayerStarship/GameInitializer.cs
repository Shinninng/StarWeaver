using UnityEngine;
namespace Veridian.Starship.Player
{
    /// <summary>
    /// Initializes the game state, ensuring the player ship, camera, and input systems are correctly configured and linked at startup.
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("Player Configuration")]
        [Tooltip("The main camera for the player. If not assigned, Camera.main will be used.")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("The PlayerShipDriver instance representing the player's ship. If not assigned, it will be searched for in the scene.")]
        [SerializeField] private PlayerShipDriver _playerDriver;

        [Header("Input System Fallback")]
        [Tooltip("If an InputProvider is not found in the scene, this prefab will be instantiated. If this is also null, an InputProvider component will be added to this GameInitializer GameObject.")]
        [SerializeField] private InputProvider _inputProviderPrefab;

        void Awake()
        {
            // Initialize systems in order of dependency.
            if (!InitializeInputProvider()) return;
            if (!InitializeCameraSystem()) return;
            InitializePlayer();
        }

        /// <summary>
        /// Ensures an InputProvider instance exists in the scene.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        private bool InitializeInputProvider()
        {
            // Check if the singleton instance already exists.
            if (InputProvider.Instance != null) return true;

            // Check if an InputProvider component exists anywhere in the scene.
            InputProvider existingProvider = FindFirstObjectByType<InputProvider>();
            if (existingProvider != null) return true; // It will initialize itself in its own Awake().

            // If no provider exists, create one.
            if (_inputProviderPrefab != null)
            {
                Debug.Log("InputProvider not found, instantiating prefab.");
                Instantiate(_inputProviderPrefab);
            }
            else
            {
                Debug.Log("InputProvider not found and no prefab assigned, creating one on GameInitializer.");
                // Adding the component triggers its Awake(), which sets the Instance.
                gameObject.AddComponent<InputProvider>();
            }

            // Final check (the Instance should be set by the newly created provider's Awake)
            if (InputProvider.Instance == null)
            {
                Debug.LogError("GameInitializer: Failed to initialize InputProvider. Game cannot start.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Locates the main camera and ensures it has the necessary StarshipCameraController component.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        private bool InitializeCameraSystem()
        {
            // 1. Determine the main camera.
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            if (_playerCamera == null)
            {
                Debug.LogError("GameInitializer: No main camera found. Ensure a camera is tagged as MainCamera or assigned to the initializer.");
                return false;
            }

            // 2. Ensure the StarshipCameraController exists on the camera GameObject.
            if (!_playerCamera.TryGetComponent<StarshipCameraController>(out _))
            {
                Debug.Log("StarshipCameraController not found on the main camera, adding it.");
                _playerCamera.gameObject.AddComponent<StarshipCameraController>();
            }
            return true;
        }

        /// <summary>
        /// Locates the player driver and links it with the initialized camera system.
        /// </summary>
        private void InitializePlayer()
        {
            // 1. Find the player driver.
            if (_playerDriver == null)
            {
                _playerDriver = FindFirstObjectByType<PlayerShipDriver>();
            }

            if (_playerDriver == null)
            {
                Debug.LogError("GameInitializer: No PlayerShipDriver found in the scene. The player cannot control a ship.");
                return;
            }

            // 2. Get the StarshipCameraController (we ensured it exists in InitializeCameraSystem).
            StarshipCameraController starshipCam = _playerCamera.GetComponent<StarshipCameraController>();

            // 3. Ensure the player's ship GameObject is active before initialization.
            if (!_playerDriver.gameObject.activeInHierarchy)
            {
                _playerDriver.gameObject.SetActive(true);
            }

            // 4. Link the driver with the camera system.
            // This call also triggers the cursor lock via the GameStateManager.
            _playerDriver.InitializePlayerView(starshipCam);
        }
    }
}