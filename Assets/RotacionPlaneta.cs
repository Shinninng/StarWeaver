using UnityEngine;

public class RotacionPlaneta : MonoBehaviour
{
    [Header("Rotacion (sobre si mismo)")]
    public Vector3 ejeRotacion = Vector3.up; // Y = eje vertical
    public float velocidadRotacion = 20f;

    [Header("Traslacion (orbita alrededor de un punto)")]
    public Transform centroOrbita;           // El sol 
    public float velocidadOrbita = 10f;
    public Vector3 ejeOrbita = Vector3.up;   // Plano de la orbita

    void Update()
    {
        // Rotacion
        transform.Rotate(ejeRotacion * velocidadRotacion * Time.deltaTime, Space.Self);

        // Traslacion
        if (centroOrbita != null)
        {
            transform.RotateAround(
                centroOrbita.position,
                ejeOrbita,
                velocidadOrbita * Time.deltaTime
            );
        }
    }
}