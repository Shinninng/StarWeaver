using TMPro;
using UnityEngine;

namespace Veridian.Starship.Player
{
    /// <summary>
    /// A simple script to display the current FPS on a TextMeshProUGUI element.
    /// Also handles setting the application's target frame rate.
    /// </summary>
    public class FpsDisplay : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("The UI Text element to display the FPS on.")]
        public TextMeshProUGUI fpsText;

        [Tooltip("How often to update the FPS text (in seconds).")]
        public float pollingTime = 0.5f;

        [Header("Performance")]
        [Tooltip("Sets the target frame rate for the application. Default: 120. Use 0 for uncapped, or -1 for V-Sync.")]
        [SerializeField]
        private int targetFrameRate = 120;

        private float time;
        private int frameCount;

        void Awake()
        {
            // Apply the target frame rate once at startup
            Application.targetFrameRate = targetFrameRate;
        }

        void Update()
        {
            // Accumulate time and frame count
            time += Time.unscaledDeltaTime;
            frameCount++;

            // Check if it's time to update the text
            if (time >= pollingTime)
            {
                // Calculate frames per second
                float fps = frameCount / time;

                // Update the text display
                if (fpsText != null)
                {
                    fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";
                }

                // Reset the counters
                time = 0f;
                frameCount = 0;
            }
        }
    }
}