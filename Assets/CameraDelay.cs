using UnityEngine;
using Unity.Cinemachine;

public class CameraDelay : MonoBehaviour
{
    public CinemachineCamera dollyCam;
    public NaveSplineFollow nave;   // ← agregar referencia a la nave
    public float delay = 5f;

    void Start()
    {
        dollyCam.Priority = 0;
        nave.enabled = false;       // ← nave pausada al inicio
        StartCoroutine(ActivarCamara());
    }

    System.Collections.IEnumerator ActivarCamara()
    {
        yield return new WaitForSeconds(delay);
        nave.enabled = true;        // ← nave arranca
        dollyCam.Priority = 10;     // ← cámara activa
    }
}