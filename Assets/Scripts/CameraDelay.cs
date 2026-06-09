using UnityEngine;
using Unity.Cinemachine;

public class CameraDelay : MonoBehaviour
{
    public CinemachineCamera dollyCam;
    public NaveSplineFollow nave;
    public float delay = 5f;

    void Start()
    {
        // Verifica antes de usar
        if (dollyCam == null) { Debug.LogError("Falta asignar dollyCam"); return; }
        if (nave == null) { Debug.LogError("Falta asignar nave"); return; }

        dollyCam.Priority = 0;
        nave.enabled = false;
        StartCoroutine(ActivarCamara());
    }

    System.Collections.IEnumerator ActivarCamara()
    {
        yield return new WaitForSeconds(delay);
        nave.enabled = true;
        dollyCam.Priority = 10;
    }
}