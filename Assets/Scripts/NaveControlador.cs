using UnityEngine;
using Unity.Cinemachine;

public class NaveControlador : MonoBehaviour
{
    public NaveSplineFollow splineFollow;
    public NaveControlLibre controlLibre;
    public CinemachineCamera camaraSpline;   // DollyCineCam
    public CinemachineCamera camaraLibre;    // nueva cam tercera persona

    void Start()
    {
        // Estado inicial — modo cinematica
        controlLibre.enabled = false;
        camaraLibre.Priority = 0;
        camaraSpline.Priority = 10;

        // Escucha el fin del spline
        splineFollow.OnSplineTerminado += CambiarAModoLibre;
    }

    void CambiarAModoLibre()
    {
        // Desactiva spline
        splineFollow.enabled = false;

        // Activa control libre
        controlLibre.enabled = true;
        controlLibre.Activar();

        // Cambia camara
        camaraSpline.Priority = 0;
        camaraLibre.Priority = 10;

        Debug.Log("Modo libre activado");
    }
}