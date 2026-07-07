using UnityEngine;
using StarWeaver.Input;
using StarWeaver.Core;

namespace StarWeaver.Core
{
    /// <summary>
    /// Gestiona el estado global del juego: pausa, inicio, fin de partida.
    ///
    /// RESPONSABILIDAD: Controlar Time.timeScale y disparar eventos de estado.
    /// No conoce la UI, no conoce la nave — usa UIStateEvents como canal de comunicación.
    ///
    /// PAUSA:
    /// Al pausar → Time.timeScale = 0 + UIStateEvents.RaiseMenuOpened()
    /// Al reanudar → Time.timeScale = 1 + UIStateEvents.RaiseMenuClosed()
    /// OrbitalPlayerDriver escucha esos eventos y deshabilita el input de la nave.
    ///
    /// NOTA: Este script escucha IsMenuPressed() en Update() directamente porque
    /// InputProvider usa WasPressedThisFrame(), que funciona correctamente aunque
    /// Time.timeScale sea 0 (el Input System no depende de Time.deltaTime).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        //  Singleton liviano
        // ─────────────────────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─────────────────────────────────────────────────────────────
        //  Estado
        // ─────────────────────────────────────────────────────────────

        public bool IsPaused { get; private set; } = false;

        // ─────────────────────────────────────────────────────────────
        //  Update
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            // Input System no depende de Time.timeScale, así que este check
            // funciona correctamente incluso con el juego pausado.
            InputProvider input = InputProvider.Instance;
            if (input == null) return;

            if (input.IsMenuPressed())
                TogglePause();
        }

        // ─────────────────────────────────────────────────────────────
        //  API Pública
        // ─────────────────────────────────────────────────────────────

        public void TogglePause()
        {
            if (IsPaused) ResumeGame();
            else PauseGame();
        }

        public void PauseGame()
        {
            if (IsPaused) return;
            IsPaused = true;

            Time.timeScale = 0f;

            // Notificamos a todos los sistemas que dependen del estado del menú.
            // OrbitalPlayerDriver desactivará los controles de nave al recibir esto.
            UIStateEvents.RaiseMenuOpened();

            Debug.Log("[GameManager] Juego pausado.");
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = 1f;
            UIStateEvents.RaiseMenuClosed();

            Debug.Log("[GameManager] Juego reanudado.");
        }

        private void OnDestroy()
        {
            // Garantizar que timeScale vuelva a 1 si el GameManager es destruido
            // inesperadamente (ej: recarga de escena en el Editor).
            Time.timeScale = 1f;
        }

        // ─────────────────────────────────────────────────────────────
        //  Validación en Editor
        // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            var input = FindAnyObjectByType<InputProvider>();
            if (input == null)
                Debug.LogWarning("[GameManager] No hay InputProvider en la escena. " +
                                 "La pausa con Tab/Esc no funcionará.", this);
        }
#endif
    }
}