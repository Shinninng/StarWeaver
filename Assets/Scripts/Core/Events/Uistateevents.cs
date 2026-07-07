using System;

namespace StarWeaver.Core
{
    /// <summary>
    /// Canal de eventos estático y desacoplado entre la UI y los sistemas de gameplay.
    ///
    /// PRINCIPIO: Ningún sistema de gameplay conoce a ningún panel de UI, y viceversa.
    /// La UI llama a RaiseMenuOpened/Closed. Los sistemas de gameplay se suscriben.
    /// Esto cumple con el principio de Inversión de Dependencias (SOLID - D).
    ///
    /// USO DESDE LA UI:
    ///   UIStateEvents.RaiseMenuOpened();
    ///   UIStateEvents.RaiseMenuClosed();
    ///
    /// USO DESDE SISTEMAS DE GAMEPLAY:
    ///   void OnEnable()  => UIStateEvents.OnMenuOpened += HandleMenuOpened;
    ///   void OnDisable() => UIStateEvents.OnMenuOpened -= HandleMenuOpened;
    /// </summary>
    public static class UIStateEvents
    {
        /// <summary>Se dispara cuando cualquier panel modal bloquea el control de la nave.</summary>
        public static event Action OnMenuOpened;

        /// <summary>Se dispara cuando se cierran todos los paneles modales y el control vuelve a la nave.</summary>
        public static event Action OnMenuClosed;

        public static void RaiseMenuOpened() => OnMenuOpened?.Invoke();
        public static void RaiseMenuClosed() => OnMenuClosed?.Invoke();
    }
}