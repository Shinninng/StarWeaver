using UnityEngine;

namespace Veridian.Starship.Core
{
    /// <summary>
    /// ScriptableObject defining the physical properties of an environment, including gravity and atmospheric drag coefficients.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnvironmentProfile", menuName = "Veridian/Starship/Environment Profile")]
    public class EnvironmentProfileSO : ScriptableObject
    {
        [Header("Gravity")]
        [Tooltip("The strength of gravity in meters per second squared (m/s^2). Earth standard is 9.81.")]
        public float gravityStrength = 9.81f;

        [Header("Base Aerodynamic Drag (Linear)")]
        [Tooltip("Base linear drag coefficient for forward/reverse movement. Primarily affects handling at low speeds. Higher values mean more resistance.")]
        public float baseForwardLinearDrag = 300f;

        [Tooltip("Base linear drag coefficient for sideways (strafing) and vertical movement. Primarily affects handling at low speeds. Typically higher than forward drag.")]
        public float baseSidewaysLinearDrag = 2000f;

        [Header("Base Aerodynamic Drag (Quadratic)")]
        [Tooltip("Base quadratic drag coefficient for forward/reverse movement. Drag increases with the square of velocity. Primarily determines the ship's top speed.")]
        public float baseForwardQuadraticDrag = 0.13f;

        [Tooltip("Base quadratic drag coefficient for sideways and vertical movement. Primarily determines the top speed of strafing/vertical maneuvers.")]
        public float baseSidewaysQuadraticDrag = 1f;
    }
}