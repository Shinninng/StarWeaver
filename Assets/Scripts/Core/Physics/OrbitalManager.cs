using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarWeaver.Core
{
    public class OrbitalManager : MonoBehaviour
    {
        public static OrbitalManager Instance { get; private set; }

        [Header("Constantes Físicas")]
        [SerializeField] private float G = 1000f;
        [SerializeField][Range(0.1f, 50f)] private float simulationSpeed = 1f;

        private List<Rigidbody> orbitalBodies = new List<Rigidbody>();

        // Eventos para que OrbitalVisualization (y cualquier otro sistema)
        // mantenga su propia lista sincronizada sin depender de un array fijo.
        public static event Action<Rigidbody> OnBodyRegistered;
        public static event Action<Rigidbody> OnBodyUnregistered;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
        }

        void FixedUpdate()
        {
            SimulateGravity();
        }

        private void SimulateGravity()
        {
            float dt = Time.fixedDeltaTime * simulationSpeed;

            for (int i = 0; i < orbitalBodies.Count; i++)
            {
                if (orbitalBodies[i] == null || orbitalBodies[i].isKinematic) continue;

                Vector3 totalForce = Vector3.zero;

                for (int j = 0; j < orbitalBodies.Count; j++)
                {
                    if (i == j || orbitalBodies[j] == null) continue;

                    Vector3 direction = orbitalBodies[j].position - orbitalBodies[i].position;
                    float distanceSqr = direction.sqrMagnitude;

                    if (distanceSqr < 0.1f) continue;

                    float forceMagnitude = G * (orbitalBodies[i].mass * orbitalBodies[j].mass) / distanceSqr;
                    totalForce += direction.normalized * forceMagnitude;
                }

                orbitalBodies[i].AddForce(totalForce * dt, ForceMode.Force);
            }
        }

        public static void RegisterBody(Rigidbody body)
        {
            if (Instance == null) return;
            if (!Instance.orbitalBodies.Contains(body))
            {
                Instance.orbitalBodies.Add(body);
                OnBodyRegistered?.Invoke(body);
            }
        }

        public static void UnregisterBody(Rigidbody body)
        {
            if (Instance == null) return;
            if (Instance.orbitalBodies.Contains(body))
            {
                Instance.orbitalBodies.Remove(body);
                OnBodyUnregistered?.Invoke(body);
            }
        }
    }
}