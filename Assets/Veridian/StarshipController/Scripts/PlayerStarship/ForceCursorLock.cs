using UnityEngine;

namespace Veridian.Starship.Player
{

    /// <summary>
    /// Aggressively forces the cursor to be locked and hidden on every frame.
    /// This is a brute-force solution to override any other script interfering with the cursor state.
    /// </summary>
    public class ForceCursorLock : MonoBehaviour
    {


        // This is called every frame, after all other Update functions.
        void LateUpdate()
        {
            ForceState();
            Cursor.visible = false;
        }

        // This is called when the script is first enabled.
        void OnEnable()
        {
            ForceState();
        }

        private void ForceState()
        {
            // Set the state directly.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}