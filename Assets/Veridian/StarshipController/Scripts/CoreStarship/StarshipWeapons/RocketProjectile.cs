using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RocketPropulsion))]
    public class RocketProjectile : ProjectileBase
    {
        [Header("Rocket Settings")]
        public float damage = 50f;
        public float explosionRadius = 15f;
        public GameObject explosionVFXPrefab;

        [Tooltip("Layers to damage via explosion. Set to -1 for Everything.")]
        public LayerMask damageLayerMask = -1;

        [Header("Terrain Interaction")]
        [Tooltip("If true, the explosion will remove trees and terrain details within its radius.")]
        public bool clearsVegetationOnImpact = false;

        private Rigidbody rb;
        private RocketPropulsion propulsion;
        private Collider physicsCollider;
        private Collider triggerCollider;
        private MeshRenderer meshRenderer;
        private bool hasImpacted = false; // Use this flag like 'hasExploded'
        private Collider[] _overlapResults = new Collider[32];
        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody>();
            propulsion = GetComponent<RocketPropulsion>();
            meshRenderer = GetComponentInChildren<MeshRenderer>();

            Collider[] colliders = GetComponents<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) triggerCollider = col;
                else physicsCollider = col;
            }
        }

        protected override void OnInitialized()
        {
            if (rb != null)
            {
                rb.linearVelocity = InheritedVelocity;
                rb.linearDamping = 0f;
                rb.angularDamping = 0.05f;
            }
        }

        // Trigger for Starships (Large Trigger Collider)
        void OnTriggerEnter(Collider other)
        {
            // If already exploding from a physics collision, ignore trigger
            if (hasImpacted) return;

            HealthComponent hitHealth = other.GetComponentInParent<HealthComponent>();

            // Check if it's a valid target (has health AND is not the owner)
            if (hitHealth != null && (OwnerHealth == null || hitHealth != OwnerHealth))
            {
                // Set flag *immediately*
                hasImpacted = true;

                Vector3 impactPoint = other.ClosestPoint(transform.position);
                Vector3 impactNormal = (transform.position - impactPoint).normalized;
                // Start the explosion sequence
                StartCoroutine(HandleImpactSequence(impactPoint, impactNormal));
            }
            // Ignore trigger collisions with objects that don't have health (like ground)
        }

        // Collision for Ground/Obstacles (Small Physics Collider)
        void OnCollisionEnter(Collision collision)
        {
            // If already exploding from a trigger, ignore physics collision
            if (hasImpacted) return;

            // Check if we hit our owner (the ship that spawned us)
            HealthComponent hitHealth = collision.gameObject.GetComponentInParent<HealthComponent>();
            if (OwnerHealth != null && hitHealth != null && hitHealth == OwnerHealth)
            {
                return; // Ignore self-collision, do not explode yet
            }

            // If we're here, we hit something valid (not ourselves).
            // This could be the ground, a building, or even a direct hit on an enemy ship.
            // Set flag *immediately*
            hasImpacted = true;

            ContactPoint contact = collision.GetContact(0);
            // Start the explosion sequence
            StartCoroutine(HandleImpactSequence(contact.point, contact.normal));
        }

        // --- DEPRECATED OVERRIDE ---
        protected override void ApplyDamage(GameObject hitObject, HealthComponent directHitHealth)
        {
            // Explosion logic is now fully in ApplyAreaDamage.
        }

        // --- Coroutine handles sequence and delayed destruction ---
        // Removed 'hitObject' parameter as it's not strictly needed for the sequence logic anymore
        private IEnumerator HandleImpactSequence(Vector3 impactPoint, Vector3 impactNormal)
        {
            // --- Stop rocket ---
            if (propulsion != null) propulsion.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true; // Important: Make kinematic AFTER setting velocity to zero
            }
            if (physicsCollider != null) physicsCollider.enabled = false;
            if (triggerCollider != null) triggerCollider.enabled = false;
            if (meshRenderer != null) meshRenderer.enabled = false;

            // --- Apply AoE Damage CENTERED ON IMPACT POINT ---
            // This is now always called after a valid collision or trigger
            ApplyAreaDamage(impactPoint);

            // --- Spawn Impact VFX ---
            SpawnImpactFX(impactPoint, impactNormal);

            // --- Wait briefly for VFX ---
            // Increased slightly to ensure VFX definitely has time to start
            yield return new WaitForSeconds(0.2f); // You might adjust this based on your VFX duration

            // --- Destroy the rocket GameObject ---
            Destroy(gameObject);
        }

        // --- Area Damage method (like ExplosiveImpact) ---
        private void ApplyAreaDamage(Vector3 explosionCenter)
        {
            // Use the NonAlloc version, which fills our pre-defined array
            int hitCount = Physics.OverlapSphereNonAlloc(explosionCenter, explosionRadius, _overlapResults, damageLayerMask);

            HashSet<HealthComponent> damagedHealths = new(); // Prevent multi-hitting same object

            // Debug.Log($"Applying Area Damage at {explosionCenter}, Radius: {explosionRadius}"); // DEBUG

            // Loop only up to the number of colliders we actually hit
            for (int i = 0; i < hitCount; i++)
            {
                // Get the collider from our re-usable array
                Collider collider = _overlapResults[i];

                HealthComponent health = collider.GetComponentInParent<HealthComponent>();

                if (health == null)
                {
                    // Debug.Log($"  - Hit '{collider.name}', but no HealthComponent found."); // DEBUG
                    continue;
                }
                if (OwnerHealth != null && health == OwnerHealth)
                {
                    // Debug.Log($"  - Hit '{collider.name}', but it's the owner."); // DEBUG
                    continue; // Don't damage owner
                }
                if (damagedHealths.Contains(health))
                {
                    // Debug.Log($"  - Hit '{collider.name}', but already damaged this explosion."); // DEBUG
                    continue; // Don't damage same object multiple times per explosion
                }
                damagedHealths.Add(health);

                float distance = Vector3.Distance(explosionCenter, collider.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                float appliedDamage = damage * falloff;

                // Debug.Log($"  - Damaging '{collider.name}' (HealthComponent: {health.name}) for {appliedDamage} damage."); // DEBUG
                health.ApplyDamage(appliedDamage);

                Rigidbody otherRb = collider.GetComponentInParent<Rigidbody>();
                if (otherRb != null)
                {
                    otherRb.AddExplosionForce(damage * 50f, explosionCenter, explosionRadius);
                }
            }

            // Terrain interaction logic using the explosionCenter
            if (clearsVegetationOnImpact)
            {
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float terrainHeight = terrain.SampleHeight(explosionCenter);
                    if (Mathf.Abs(explosionCenter.y - terrainHeight) < explosionRadius) // Check if close enough vertically
                    {
                        ClearVegetation(terrain, explosionCenter);
                    }
                }
            }
            System.Array.Clear(_overlapResults, 0, hitCount);
        }

        // SpawnImpactFX remains the same
        protected override void SpawnImpactFX(Vector3 impactPoint, Vector3 impactNormal)
        {
            if (explosionVFXPrefab != null)
            {
                Quaternion impactRotation;

                // THE FIX: Check if the impactNormal is a zero vector.
                // We use sqrMagnitude because it's faster than checking Vector3.zero.
                if (impactNormal.sqrMagnitude < 0.0001f)
                {
                    // The normal is invalid, so use a default (no rotation)
                    impactRotation = Quaternion.identity;
                }
                else
                {
                    // The normal is valid, use it for the rotation
                    impactRotation = Quaternion.LookRotation(impactNormal);
                }

                // This is now safe and will not crash
                Instantiate(explosionVFXPrefab, impactPoint, impactRotation);
            }
        }

        // ClearVegetation remains the same
        private void ClearVegetation(Terrain terrain, Vector3 impactPoint)
        {
            // (Code is identical to previous version)
            TerrainData terrainData = terrain.terrainData;
            List<TreeInstance> trees = new(terrainData.treeInstances);
            trees.RemoveAll(tree =>
            {
                Vector3 treeWorldPos = Vector3.Scale(tree.position, terrainData.size) + terrain.transform.position;
                return Vector3.Distance(treeWorldPos, impactPoint) < explosionRadius;
            });
            terrainData.treeInstances = trees.ToArray();

            if (terrainData.detailWidth > 0 && terrainData.detailHeight > 0)
            {
                int detailMapRadius = (int)((explosionRadius / terrainData.size.x) * terrainData.detailWidth);
                int detailMapX = (int)(((impactPoint.x - terrain.transform.position.x) / terrainData.size.x) * terrainData.detailWidth);
                int detailMapZ = (int)(((impactPoint.z - terrain.transform.position.z) / terrainData.size.z) * terrainData.detailHeight);
                int areaSize = detailMapRadius * 2;
                int areaOffsetX = detailMapX - detailMapRadius;
                int areaOffsetZ = detailMapZ - detailMapRadius;

                for (int layerIndex = 0; layerIndex < terrainData.detailPrototypes.Length; layerIndex++)
                {
                    int[,] detailLayer = terrainData.GetDetailLayer(areaOffsetX, areaOffsetZ, areaSize, areaSize, layerIndex);
                    for (int x = 0; x < areaSize; x++)
                    {
                        for (int z = 0; z < areaSize; z++)
                        {
                            if (Vector2.Distance(new Vector2(x, z), new Vector2(detailMapRadius, detailMapRadius)) < detailMapRadius)
                            {
                                detailLayer[z, x] = 0;
                            }
                        }
                    }
                    terrainData.SetDetailLayer(areaOffsetX, areaOffsetZ, layerIndex, detailLayer);
                }
            }
            terrain.Flush();
        }
    }
}