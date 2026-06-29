namespace StarWeaver.Core
{
    /// <summary>
    /// Interfaz para cualquier entidad (Jugador o IA) que pueda pilotar el OrbitalStarshipController.
    /// </summary>
    public interface IShipDriver
    {
        /// <summary>
        /// Llamado cuando el driver es asignado a un controlador de nave.
        /// </summary>
        void AssignController(OrbitalStarshipController controller);

        /// <summary>
        /// Llamado cuando el driver es removido de un controlador de nave.
        /// </summary>
        void ReleaseController();

        /// <summary>
        /// El driver debe proporcionar los inputs deseados para el fotograma actual.
        /// </summary>
        ShipInputState GetDesiredInputState();

        /// <summary>
        /// Indica si el driver está controlando activamente la nave (ej. el jugador no está en vista libre).
        /// </summary>
        bool IsActivelyControlling();

        /// <summary>
        /// Proporciona una descripción de los controles específicos del driver.
        /// </summary>
        string GetControlDescription();
    }
}