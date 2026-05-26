namespace Veridian.Starship.Core
{
    /// <summary>
    /// Interface for any entity (Player or AI) that can pilot the AtmosphericStarshipController.
    /// </summary>
    public interface IShipDriver
    {
        /// <summary>
        /// Called when the driver is assigned to a controller.
        /// </summary>
        void AssignController(AtmosphericStarshipController controller);

        /// <summary>
        /// Called when the driver is removed from a controller.
        /// </summary>
        void ReleaseController();

        /// <summary>
        /// The driver must provide the desired inputs for the current frame.
        /// </summary>
        ShipInputState GetDesiredInputState();

        /// <summary>
        /// Indicates if the driver is actively controlling the ship (e.g., Player is not in FreeLook).
        /// Used by the controller to determine if rotational inputs should be applied.
        /// </summary>
        bool IsActivelyControlling();

        /// <summary>
        /// Provides a description of the controls, specific to the driver (e.g., keybindings for player, mode for AI).
        /// </summary>
        string GetControlDescription();
    }
}