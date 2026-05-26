using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// Manages the global environment settings for the scene, such as gravity and atmospheric drag coefficients.
    /// Functions as a singleton.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the EnvironmentManager.
        /// </summary>
        public static EnvironmentManager Instance { get; private set; }

        [Tooltip("The active environment profile defining gravity strength and atmospheric drag properties for the current scene.")]
        public EnvironmentProfileSO currentProfile;

        /// <summary>
        /// Public accessor for the currently active environment profile.
        /// </summary>
        public EnvironmentProfileSO CurrentProfile => currentProfile;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (currentProfile == null)
            {
                Debug.LogWarning("EnvironmentManager: No EnvironmentProfileSO assigned. Physics may be unpredictable.");
            }
            else
            {
                ApplyGlobalPhysicsSettings();
            }
        }

        void OnValidate()
        {
            // Allow changing the profile or its values during runtime in the editor
            if (Application.isPlaying && currentProfile != null)
            {
                ApplyGlobalPhysicsSettings();
            }
        }

        /// <summary>
        /// Applies settings from the current profile that affect the global physics engine (specifically, the gravity vector).
        /// </summary>
        private void ApplyGlobalPhysicsSettings()
        {
            // Set the global gravity vector based on the environment profile.
            // We assume gravity always pulls down (negative Y).
            Physics.gravity = new Vector3(0, -currentProfile.gravityStrength, 0);
        }
    }
}