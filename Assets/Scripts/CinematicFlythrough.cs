using UnityEngine;

public class CinematicFlythrough : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;

    [Header("Movimiento")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 2f;
    public float waypointThreshold = 0.3f;

    [Header("Control")]
    public bool autoStart = true;
    public bool loop = false;

    private int currentIndex = 0;
    private bool isMoving = false;

    void Start()
    {
        if (autoStart && waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            currentIndex = 1;
            isMoving = true;
        }
    }

    void Update()
    {
        if (!isMoving || waypoints.Length < 2) return;

        Transform target = waypoints[currentIndex];

        // Mover hacia el waypoint
        transform.position = Vector3.Lerp(
            transform.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        // Rotar suavemente hacia el waypoint
        Vector3 direction = (target.position - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }

        // ¿Llegamos al punto?
        if (Vector3.Distance(transform.position, target.position) < waypointThreshold)
        {
            currentIndex++;

            if (currentIndex >= waypoints.Length)
            {
                if (loop) currentIndex = 0;
                else isMoving = false; // Fin del recorrido
            }
        }
    }

    // Llamar desde botón u otro script para iniciar manualmente
    public void StartFlythrough()
    {
        currentIndex = 1;
        transform.position = waypoints[0].position;
        isMoving = true;
    }
}