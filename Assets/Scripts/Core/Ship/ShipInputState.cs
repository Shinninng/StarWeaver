using UnityEngine;

namespace StarWeaver.Core
{
    /// Representa el estado de entrada agregado solicitado por un conductor (Jugador o IA) 
    /// para el controlador de la nave en un solo fotograma.
    [System.Serializable]
    public struct ShipInputState
    {
        [Tooltip("Empuje hacia adelante (1) o reversa (-1). Normalizado de -1 a 1.")]
        public float Thrust;

        [Tooltip("Empuje vertical hacia arriba (1) o hacia abajo (-1). Normalizado de -1 a 1.")]
        public float Vertical;

        [Tooltip("Input de Alabeo (Roll). Derecha (1) o Izquierda (-1). Normalizado de -1 a 1.")]
        public float Roll;

        [Tooltip("Input de Cabeceo (Pitch - Nariz arriba/abajo). Valor de entrada escalado.")]
        public float Pitch;

        [Tooltip("Input de Guiñada (Yaw - Nariz Izquierda/Derecha). Valor de entrada escalado.")]
        public float Yaw;

        [Tooltip("¿Se solicita la función de Boost (Postcombustión)?")]
        public bool Boost;

        [Tooltip("¿Se solicita soltar una bomba en este fotograma?")]
        public bool FireBomb;

        [Tooltip("¿Se solicita fuego del arma primaria?")]
        public bool FirePrimary;

        [Tooltip("¿Se solicita fuego del arma secundaria?")]
        public bool FireSecondary;

        [Tooltip("La posición en el espacio de mundo a la que apunta el conductor (Jugador o IA).")]
        public Vector3 AimPosition;

        [Tooltip("Indica si el conductor está fijando activamente un objetivo específico.")]
        public bool IsTargetEngaged;
    }
}