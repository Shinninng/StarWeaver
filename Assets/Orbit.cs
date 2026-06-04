using UnityEngine;

public class Orbit : MonoBehaviour
{
    // Constante gravitacional
    readonly float G = 1000f;

    // Cuerpos celestiales
    GameObject[] celestials;
    Rigidbody[] rigidbodies;

    [SerializeField] bool IsElipticalOrbit = false;

    [Header("Debug Orbitas")]
    [SerializeField] bool showOrbits = true;
    [SerializeField] bool showForces = true;
    [SerializeField] bool showVelocities = true;
    [SerializeField] int orbitResolution = 100;
    [SerializeField] float arrowScale = 0.001f;
    [SerializeField] Color forceColor = Color.red;
    [SerializeField] Color velocityColor = Color.cyan;
    [SerializeField] Color orbitColor = Color.white;

    void Start()
    {
        // Busca planetas por tag
        celestials = GameObject.FindGameObjectsWithTag("Celestial");

        // Cachea Rigidbodies
        rigidbodies = new Rigidbody[celestials.Length];
        for (int i = 0; i < celestials.Length; i++)
        {
            rigidbodies[i] = celestials[i].GetComponent<Rigidbody>();
            // Suaviza movimiento
            rigidbodies[i].interpolation = RigidbodyInterpolation.Interpolate;
        }

        SetInitialVelocity();
    }

    void FixedUpdate()
    {
        // Aplica gravedad
        Gravity();
    }

    void SetInitialVelocity()
    {
        for (int i = 0; i < celestials.Length; i++)
        {
            for (int j = 0; j < celestials.Length; j++)
            {
                if (i == j) continue;

                float m2 = rigidbodies[j].mass;
                // Distancia entre cuerpos
                float r = Vector3.Distance(
                    celestials[i].transform.position,
                    celestials[j].transform.position
                );

                // Orienta hacia objetivo
                celestials[i].transform.LookAt(celestials[j].transform);

                if (IsElipticalOrbit)
                    // Velocidad orbita eliptica
                    rigidbodies[i].linearVelocity += celestials[i].transform.right
                        * Mathf.Sqrt((G * m2) * ((2 / r) - (1 / (r * 1.5f))));
                else
                    // Velocidad orbita circular
                    rigidbodies[i].linearVelocity += celestials[i].transform.right
                        * Mathf.Sqrt((G * m2) / r);
            }
        }
    }

    void Gravity()
    {
        for (int i = 0; i < celestials.Length; i++)
        {
            for (int j = 0; j < celestials.Length; j++)
            {
                if (i == j) continue;

                float m1 = rigidbodies[i].mass;
                float m2 = rigidbodies[j].mass;
                // Distancia actual
                float r = Vector3.Distance(
                    celestials[i].transform.position,
                    celestials[j].transform.position
                );

                // Fuerza gravitacional: F = G(m1*m2)/r²
                rigidbodies[i].AddForce(
                    (celestials[j].transform.position - celestials[i].transform.position).normalized
                    * (G * (m1 * m2) / (r * r))
                );
            }
        }
    }

    // ─── DEBUG ────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // Inicializa en edit mode
        if (celestials == null)
            celestials = GameObject.FindGameObjectsWithTag("Celestial");
        if (celestials == null || celestials.Length == 0) return;

        // Refresca cache Rigidbodies
        if (rigidbodies == null || rigidbodies.Length != celestials.Length)
        {
            rigidbodies = new Rigidbody[celestials.Length];
            for (int i = 0; i < celestials.Length; i++)
                rigidbodies[i] = celestials[i].GetComponent<Rigidbody>();
        }

        for (int i = 0; i < celestials.Length; i++)
        {
            if (celestials[i] == null || rigidbodies[i] == null) continue;

            // Dibuja trayectoria
            if (showOrbits)
                DrawPredictedOrbit(i);

            // Dibuja velocidad actual
            if (showVelocities && Application.isPlaying)
                DrawArrow(
                    celestials[i].transform.position,
                    rigidbodies[i].linearVelocity * arrowScale,
                    velocityColor
                );

            // Dibuja fuerzas gravitacionales
            if (showForces)
            {
                for (int j = 0; j < celestials.Length; j++)
                {
                    if (i == j || celestials[j] == null) continue;

                    float m1 = rigidbodies[i].mass;
                    float m2 = rigidbodies[j].mass;
                    float r = Vector3.Distance(
                        celestials[i].transform.position,
                        celestials[j].transform.position
                    );

                    // Dirección hacia atractor
                    Vector3 forceDir = (celestials[j].transform.position
                        - celestials[i].transform.position).normalized;
                    float forceMag = G * (m1 * m2) / (r * r);

                    DrawArrow(
                        celestials[i].transform.position,
                        forceDir * forceMag * arrowScale,
                        forceColor
                    );
                }
            }

            // Etiqueta con datos
            DrawLabel(i);
        }
    }

    void DrawPredictedOrbit(int index)
    {
        // Busca cuerpo central
        int centerIndex = GetMostMassiveIndex(index);
        if (centerIndex < 0) return;

        Vector3 center = celestials[centerIndex].transform.position;
        Vector3 current = celestials[index].transform.position;
        // Radio de orbita
        float radius = Vector3.Distance(current, center);

        Gizmos.color = new Color(orbitColor.r, orbitColor.g, orbitColor.b, 0.4f);

        Vector3 prev = Vector3.zero;
        for (int k = 0; k <= orbitResolution; k++)
        {
            // Puntos del circulo
            float angle = k * 360f / orbitResolution;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 point = center + rot * (current - center).normalized * radius;

            if (k > 0)
                Gizmos.DrawLine(prev, point);

            prev = point;
        }
    }

    void DrawArrow(Vector3 origin, Vector3 vector, Color color)
    {
        if (vector == Vector3.zero) return;

        Gizmos.color = color;
        // Linea principal
        Gizmos.DrawLine(origin, origin + vector);

        // Cabeza de flecha
        Vector3 tip = origin + vector;
        Vector3 perpendicular = Vector3.Cross(vector.normalized, Vector3.up).normalized;
        float headSize = vector.magnitude * 0.2f;

        Gizmos.DrawLine(tip, tip - vector.normalized * headSize + perpendicular * headSize * 0.5f);
        Gizmos.DrawLine(tip, tip - vector.normalized * headSize - perpendicular * headSize * 0.5f);
    }

    void DrawLabel(int index)
    {
#if UNITY_EDITOR
        Vector3 pos = celestials[index].transform.position;
        string name = celestials[index].name;
        float mass = rigidbodies[index].mass;
        // Velocidad solo en Play
        float speed = Application.isPlaying
            ? rigidbodies[index].linearVelocity.magnitude
            : 0f;

        string label = $"{name}\nMasa: {mass:F0}\nVel: {speed:F1}";

        UnityEditor.Handles.color = Color.white;
        // Label encima del planeta
        UnityEditor.Handles.Label(pos + Vector3.up * 15f, label);
#endif
    }

    int GetMostMassiveIndex(int excludeIndex)
    {
        // Encuentra cuerpo más masivo
        int result = -1;
        float maxMass = 0f;

        for (int i = 0; i < celestials.Length; i++)
        {
            if (i == excludeIndex || rigidbodies[i] == null) continue;
            if (rigidbodies[i].mass > maxMass)
            {
                maxMass = rigidbodies[i].mass;
                result = i;
            }
        }

        return result;
    }
}