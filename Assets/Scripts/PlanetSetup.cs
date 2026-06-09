using UnityEngine;

public class PlanetSetup : MonoBehaviour
{
    public Transform sol;

    [ContextMenu("Aplicar configuracion")]
    void AplicarConfiguracion()
    {
        var datos = new (string nombre, float masa, float escala, float distancia)[]
        {
            ("Sun",     1000000f, 1200f,     0f),
            ("Mercury",    500f,    6f,   2200f),
            ("Venus",     8000f,   15f,   2800f),
            ("Earth",    10000f,   97f,   3600f),
            ("Moon",       100f,   24f,   3750f),
            ("Mars",      1000f,    8f,   4800f),
            ("Jupiter", 3000000f, 500f,  12000f),
            ("Saturn",  900000f,  420f,  22000f),
            ("Uran",    140000f,  180f,  38000f),
            ("Neptun",  170000f,  175f,  55000f),
        };

        foreach (var d in datos)
        {
            GameObject obj = GameObject.Find(d.nombre);
            if (obj == null)
            {
                Debug.LogWarning($"No encontre: {d.nombre}");
                continue;
            }

            // Escala
            obj.transform.localScale = Vector3.one * d.escala;

            // Masa
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.mass = d.masa;
            else
                Debug.LogWarning($"{d.nombre} no tiene Rigidbody");

            // Posicion local (porque son hijos de SistemaSolar)
            if (sol != null && d.nombre != sol.name)
                obj.transform.localPosition = new Vector3(d.distancia, 0f, 0f);
            else
                obj.transform.localPosition = Vector3.zero;

            Debug.Log($"OK: {d.nombre}");
        }

        [ContextMenu("Listar todos los Celestial")]
        void ListarCelestials()
        {
            GameObject[] todos = GameObject.FindGameObjectsWithTag("Celestial");
            foreach (var obj in todos)
                Debug.Log($"Encontrado: '{obj.name}' en pos {obj.transform.position}");
        }
    }
}