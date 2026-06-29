using UnityEngine;

namespace StarWeaver.Camera
{
    /// <summary>
    /// Mantiene el PlanetCam alineado con la cámara principal en posición y rotación.
    /// El PlanetCam es una cámara Overlay pura (sin CinemachineCamera) cuyo único
    /// trabajo es renderizar la layer Planets a distancias enormes.
    /// </summary>
    public class PlanetCamFollower : MonoBehaviour
    {
        [Header("Referencia")]
        [Tooltip("Asignar la MainCamera desde el Inspector.")]
        [SerializeField] private UnityEngine.Camera mainCamera;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = UnityEngine.Camera.main;

            if (mainCamera == null)
                Debug.LogError("[PlanetCamFollower] No se encontró la MainCamera.", this);
        }

        // LateUpdate garantiza que copiamos la posición DESPUÉS de que
        // Cinemachine ya movió la MainCamera en ese frame.
        private void LateUpdate()
        {
            if (mainCamera == null) return;

            transform.SetPositionAndRotation(
                mainCamera.transform.position,
                mainCamera.transform.rotation
            );
        }
    }
}
