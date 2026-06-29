using UnityEngine;
using System.Collections.Generic;
using StarWeaver.Core;

namespace StarWeaver.Systems
{
    public class OrbitalVisualization : MonoBehaviour
    {
        [Header("Configuración Visual")]
        [SerializeField] private bool showOrbits = true;
        [SerializeField] private bool showSOIFields = true;
        [SerializeField] private int orbitResolution = 100;
        [SerializeField] private Color orbitColor = new Color(0f, 0.8f, 1f, 0.4f);
        [SerializeField] private Material lineMaterial;

        [Header("Parámetros Matemáticos (Deben coincidir con tu física)")]
        [SerializeField] private float G = 5000f;

        // Lista en lugar de array: se actualiza sola cuando los cuerpos
        // se registran o desregistran en OrbitalManager.
        private List<GameObject> _celestials = new List<GameObject>();

        // Dictionaries para asociar cada cuerpo con sus LineRenderers.
        // Usar el GameObject como clave permite encontrar y limpiar la línea
        // exacta cuando un cuerpo es destruido.
        private Dictionary<GameObject, LineRenderer> _orbitLines = new Dictionary<GameObject, LineRenderer>();
        private Dictionary<GameObject, LineRenderer> _soiLines = new Dictionary<GameObject, LineRenderer>();

        // ─────────────────────────────────────────
        //  Ciclo de vida
        // ─────────────────────────────────────────

        private void OnEnable()
        {
            OrbitalManager.OnBodyRegistered += HandleBodyRegistered;
            OrbitalManager.OnBodyUnregistered += HandleBodyUnregistered;
        }

        private void OnDisable()
        {
            // Siempre desuscribirse para evitar callbacks a un objeto destruido.
            OrbitalManager.OnBodyRegistered -= HandleBodyRegistered;
            OrbitalManager.OnBodyUnregistered -= HandleBodyUnregistered;
        }

        private void Start()
        {
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            // Poblar la lista inicial con los cuerpos que ya están registrados
            // antes de que este componente existiera (ej: planetas que despiertan antes).
            // Los buscamos por tag como fallback de arranque.
            GameObject[] existingBodies = GameObject.FindGameObjectsWithTag("Celestial");
            foreach (var body in existingBodies)
            {
                AddCelestialBody(body);
            }
        }

        private void LateUpdate()
        {
            if (_celestials.Count == 0) return;

            if (showOrbits) DrawPredictedOrbits();
            if (showSOIFields) DrawSphereOfInfluenceFields();
        }

        // ─────────────────────────────────────────
        //  Handlers de eventos de OrbitalManager
        // ─────────────────────────────────────────

        private void HandleBodyRegistered(Rigidbody body)
        {
            // Solo nos interesan cuerpos celestes (planetas, sol, lunas).
            // Las naves también se registran en OrbitalManager, así que
            // filtramos por tag para no dibujar órbitas de naves.
            if (!body.CompareTag("Celestial")) return;
            AddCelestialBody(body.gameObject);
        }

        private void HandleBodyUnregistered(Rigidbody body)
        {
            if (body == null) return;
            RemoveCelestialBody(body.gameObject);
        }

        // ─────────────────────────────────────────
        //  Gestión de cuerpos y LineRenderers
        // ─────────────────────────────────────────

        private void AddCelestialBody(GameObject body)
        {
            if (_celestials.Contains(body)) return;

            _celestials.Add(body);

            // Línea de órbita: hija de este visualizador, en world space.
            GameObject orbitGo = new GameObject($"OrbitLine_{body.name}");
            orbitGo.transform.SetParent(this.transform);
            LineRenderer orbitLr = orbitGo.AddComponent<LineRenderer>();
            ConfigureLineRenderer(orbitLr, orbitColor, 2f);
            _orbitLines[body] = orbitLr;

            // Línea de SOI: hija del planeta para que lo siga automáticamente.
            GameObject soiGo = new GameObject($"SOILine_{body.name}");
            soiGo.transform.SetParent(body.transform);
            soiGo.transform.localPosition = Vector3.zero;
            LineRenderer soiLr = soiGo.AddComponent<LineRenderer>();
            ConfigureLineRenderer(soiLr, new Color(1f, 0.2f, 0.2f, 0.15f), 1.5f);
            _soiLines[body] = soiLr;
        }

        private void RemoveCelestialBody(GameObject body)
        {
            if (!_celestials.Contains(body)) return;

            _celestials.Remove(body);

            // Destruir el LineRenderer de órbita.
            if (_orbitLines.TryGetValue(body, out LineRenderer orbitLr) && orbitLr != null)
            {
                Destroy(orbitLr.gameObject);
            }
            _orbitLines.Remove(body);

            // El LineRenderer de SOI era hijo del planeta destruido,
            // así que Unity ya lo destruyó junto con él. Solo limpiamos el dict.
            _soiLines.Remove(body);
        }

        private void ConfigureLineRenderer(LineRenderer lr, Color color, float width)
        {
            lr.sharedMaterial = lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = orbitResolution + 1;
            lr.useWorldSpace = true;
            lr.loop = true;
        }

        // ─────────────────────────────────────────
        //  Dibujo
        // ─────────────────────────────────────────

        private void DrawPredictedOrbits()
        {
            GameObject sol = FindSun();
            if (sol == null) return;

            Rigidbody solRb = sol.GetComponent<Rigidbody>();
            float solMass = solRb != null ? solRb.mass : 100000f;

            foreach (var body in _celestials)
            {
                // Verificación defensiva: el cuerpo podría haber sido destruido
                // entre frames antes de que llegue el evento de desregistro.
                if (body == null)
                {
                    if (_orbitLines.TryGetValue(body, out LineRenderer lr) && lr != null)
                        lr.positionCount = 0;
                    continue;
                }

                if (body == sol)
                {
                    if (_orbitLines.TryGetValue(body, out LineRenderer sunLr))
                        sunLr.positionCount = 0;
                    continue;
                }

                if (!_orbitLines.TryGetValue(body, out LineRenderer orbitLr)) continue;

                Vector3 planetPos = body.transform.position;
                float radius = Vector3.Distance(planetPos, sol.transform.position);

                orbitLr.positionCount = orbitResolution + 1;
                for (int theta = 0; theta <= orbitResolution; theta++)
                {
                    float angle = (theta * 2f * Mathf.PI) / orbitResolution;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    orbitLr.SetPosition(theta, sol.transform.position + offset);
                }
            }
        }

        private void DrawSphereOfInfluenceFields()
        {
            foreach (var body in _celestials)
            {
                if (body == null) continue;
                if (!_soiLines.TryGetValue(body, out LineRenderer soiLr)) continue;
                if (soiLr == null) continue;

                Rigidbody rb = body.GetComponent<Rigidbody>();
                if (rb == null) continue;

                float soiRadius = Mathf.Sqrt(rb.mass * G) * 0.15f;

                soiLr.positionCount = orbitResolution + 1;
                for (int theta = 0; theta <= orbitResolution; theta++)
                {
                    float angle = (theta * 2f * Mathf.PI) / orbitResolution;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * soiRadius, 0f, Mathf.Sin(angle) * soiRadius);
                    soiLr.SetPosition(theta, body.transform.position + offset);
                }
            }
        }

        private GameObject FindSun()
        {
            GameObject sun = null;
            float maxMass = 0f;

            foreach (var body in _celestials)
            {
                // Verificación defensiva contra referencias inválidas.
                if (body == null) continue;

                Rigidbody rb = body.GetComponent<Rigidbody>();
                if (rb != null && rb.mass > maxMass)
                {
                    maxMass = rb.mass;
                    sun = body;
                }
            }
            return sun;
        }
    }
}