using UnityEngine;
using UnityEngine.InputSystem;

public class NaveControlLibre : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadMovimiento = 500f;
    public float velocidadBoost = 1500f;
    public float velocidadRotacion = 2f;

    [Header("Gravedad planetas")]
    public float fuerzaGravedad = 500f;
    public float radioInfluencia = 3000f;
    public LayerMask capaPlanetas;

    Rigidbody rb;
    bool activo = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Activar()
    {
        activo = true;
        rb.isKinematic = false;
        rb.linearVelocity = transform.forward * velocidadMovimiento * 0.3f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
        if (!activo) return;
        MoverNave();
        AplicarGravedad();
    }

    void MoverNave()
    {
        // Nuevo Input System
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        float vel = keyboard.leftShiftKey.isPressed
            ? velocidadBoost
            : velocidadMovimiento;

        // WASD
        Vector3 input = Vector3.zero;
        if (keyboard.wKey.isPressed) input.z = 1f;
        if (keyboard.sKey.isPressed) input.z = -1f;
        if (keyboard.aKey.isPressed) input.x = -1f;
        if (keyboard.dKey.isPressed) input.x = 1f;
        if (keyboard.qKey.isPressed) input.y = -1f;
        if (keyboard.eKey.isPressed) input.y = 1f;

        rb.AddRelativeForce(input * vel);

        // Mouse
        float mouseX = mouse.delta.x.ReadValue() * velocidadRotacion;
        float mouseY = mouse.delta.y.ReadValue() * velocidadRotacion;

        transform.Rotate(Vector3.up, mouseX, Space.World);
        transform.Rotate(Vector3.right, -mouseY, Space.Self);
    }

    void AplicarGravedad()
    {
        Collider[] cercanos = Physics.OverlapSphere(
            transform.position, radioInfluencia, capaPlanetas
        );

        foreach (Collider col in cercanos)
        {
            Rigidbody rbPlaneta = col.GetComponent<Rigidbody>();
            if (rbPlaneta == null) continue;

            float distancia = Vector3.Distance(transform.position, col.transform.position);
            if (distancia < 0.1f) continue;

            Vector3 direccion = (col.transform.position - transform.position).normalized;
            float fuerza = fuerzaGravedad * rbPlaneta.mass / (distancia * distancia);
            rb.AddForce(direccion * fuerza);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, radioInfluencia);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radioInfluencia);
    }
}