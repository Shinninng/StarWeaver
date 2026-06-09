using UnityEngine;

public class NaveGravedad : MonoBehaviour
{
    [Header("Configuracion")]
    public float fuerzaGravedad = 500f;   // intensidad de atraccion
    public float radioInfluencia = 2000f; // distancia maxima de efecto
    public LayerMask capaPlanetas;        // layer Planets

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        AplicarGravedad();
    }

    void AplicarGravedad()
    {
        // Busca todos los Celestial en rango
        Collider[] cercanos = Physics.OverlapSphere(
            transform.position,
            radioInfluencia,
            capaPlanetas
        );

        foreach (Collider col in cercanos)
        {
            Rigidbody rbPlaneta = col.GetComponent<Rigidbody>();
            if (rbPlaneta == null) continue;

            float distancia = Vector3.Distance(transform.position, col.transform.position);
            if (distancia < 0.1f) continue; // evita division por cero

            // Direccion hacia el planeta
            Vector3 direccion = (col.transform.position - transform.position).normalized;

            // Fuerza inversamente proporcional a la distancia
            float fuerza = fuerzaGravedad * rbPlaneta.mass / (distancia * distancia);

            rb.AddForce(direccion * fuerza);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visualiza radio de influencia en Scene
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, radioInfluencia);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radioInfluencia);
    }
}