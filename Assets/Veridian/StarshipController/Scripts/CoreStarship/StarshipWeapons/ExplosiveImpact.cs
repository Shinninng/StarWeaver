using System.Collections.Generic;
using UnityEngine;
using Veridian.Starship.Core;

namespace Veridian.Starship.Weapons
{
    /// <summary>
    /// When it collides, it creates a crater,
    /// removes trees and details, deals AOE damage, and then destroys itself.
    /// </summary>
    public class ExplosiveImpact : MonoBehaviour
    {
        [Header("Explosion Settings")]
        public float effectRadius = 10f;
        public float craterDepth = 4f;
        public GameObject explosionVFX;

        [Header("Damage Settings")]
        [Tooltip("The base damage dealt at the center of the explosion.")]
        public float explosionDamage = 100f;
        [Tooltip("Layers that can receive damage. Set to -1 for Everything.")]
        public LayerMask damageLayerMask = -1; // Set to hit all layers

        /// <summary>
        /// This MUST be set by the system that spawns this bomb
        /// to prevent self-damage.
        /// </summary>
        [System.NonSerialized]
        public HealthComponent OwnerHealth;

        [Header("Terrain Interaction")]
        [Tooltip("If true, the explosion will attempt to deform the terrain to create a crater.")]
        public bool createCrater = false; // Default set to false
        [Tooltip("If true, the explosion will remove trees and terrain details (prototypes) within its radius.")]
        public bool clearVegetation = false; // Default set to false

        [Tooltip("The curve used to shape the crater.")]
        public AnimationCurve craterShape;

        [Header("Physics Settings")]
        public float linearDrag = 0.3f;
        public float angularDrag = 0.5f;
        private Collider[] _overlapResults = new Collider[32];
        private bool hasExploded = false;

        void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearDamping = linearDrag;
                rb.angularDamping = angularDrag;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (hasExploded)
            {
                return;
            }

            // Get the HealthComponent of the object we just hit
            HealthComponent hitHealth = collision.gameObject.GetComponentInParent<HealthComponent>();

            // Check if we hit our owner (the ship that spawned us)
            if (OwnerHealth != null && hitHealth != null && hitHealth == OwnerHealth)
            {
                return; // Ignore this collision and do not explode
            }


            // If we're here, we hit something valid (not ourselves).
            hasExploded = true;

            // Start the coroutine to handle the explosion effects.
            StartCoroutine(ExplosionCoroutine(collision.GetContact(0).point));
        }

        private System.Collections.IEnumerator ExplosionCoroutine(Vector3 impactPoint)
        {
            yield return new WaitForFixedUpdate();

            if (explosionVFX != null)
            {
                Instantiate(explosionVFX, impactPoint, Quaternion.identity);
            }

            // Apply AOE Damage
            ApplyAreaDamage(impactPoint);

            // Check if we should interact with terrain at all
            if (createCrater || clearVegetation)
            {
                Terrain terrain = Terrain.activeTerrain;
                // Check if terrain exists
                if (terrain != null)
                {
                    float terrainHeight = terrain.SampleHeight(impactPoint);
                    // Check if impact is close enough to the terrain surface
                    if (Mathf.Abs(impactPoint.y - terrainHeight) < effectRadius * 0.5f)
                    {
                        // Check if we should create a crater
                        if (createCrater)
                        {
                            CreateCrater(terrain, impactPoint);
                        }

                        // Check if we should clear vegetation
                        if (clearVegetation)
                        {
                            ClearVegetation(terrain, impactPoint);
                        }
                    }
                }
            }

            Destroy(gameObject);
        }

        private void ApplyAreaDamage(Vector3 impactPoint)
        {
            // Use the NonAlloc version, which fills our pre-defined array
            int hitCount = Physics.OverlapSphereNonAlloc(impactPoint, effectRadius, _overlapResults, damageLayerMask);

            HashSet<HealthComponent> damagedHealths = new();

            // Loop only up to the number of colliders we actually hit
            for (int i = 0; i < hitCount; i++)
            {
                // Get the collider from our re-usable array
                Collider collider = _overlapResults[i];

                HealthComponent health = collider.GetComponentInParent<HealthComponent>();

                if (health == null) continue;
                if (OwnerHealth != null && health == OwnerHealth) continue;
                if (damagedHealths.Contains(health)) continue;

                damagedHealths.Add(health);

                float distance = Vector3.Distance(impactPoint, collider.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / effectRadius);
                float damageToApply = explosionDamage * falloff;

                health.ApplyDamage(damageToApply);

                Rigidbody rb = collider.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(explosionDamage * 10f, impactPoint, effectRadius);
                }
            }

            // Optional: Clear the part of the array we used to prevent stale data
            System.Array.Clear(_overlapResults, 0, hitCount);
        }

        private void CreateCrater(Terrain terrain, Vector3 impactPoint)
        {
            TerrainData terrainData = terrain.terrainData;
            int heightmapX = (int)(((impactPoint.x - terrain.transform.position.x) / terrainData.size.x) * terrainData.heightmapResolution);
            int heightmapZ = (int)(((impactPoint.z - terrain.transform.position.z) / terrainData.size.z) * terrainData.heightmapResolution);
            int radiusInPixels = (int)((effectRadius / terrainData.size.x) * terrainData.heightmapResolution);
            int areaSize = radiusInPixels * 2;
            int areaOffsetX = heightmapX - radiusInPixels;
            int areaOffsetZ = heightmapZ - radiusInPixels;
            float[,] heights = terrainData.GetHeights(areaOffsetX, areaOffsetZ, areaSize, areaSize);
            float craterBottomHeight = (terrain.SampleHeight(impactPoint) - craterDepth) / terrainData.size.y;
            for (int x = 0; x < areaSize; x++)
            {
                for (int z = 0; z < areaSize; z++)
                {
                    float distanceFromCenter = Vector2.Distance(new Vector2(x, z), new Vector2(radiusInPixels, radiusInPixels));
                    float normalizedDistance = distanceFromCenter / radiusInPixels;
                    if (normalizedDistance > 1) continue;
                    float blend = craterShape.Evaluate(1 - normalizedDistance);
                    heights[z, x] = Mathf.Lerp(heights[z, x], craterBottomHeight, blend);
                }
            }
            terrainData.SetHeights(areaOffsetX, areaOffsetZ, heights);
        }

        private void ClearVegetation(Terrain terrain, Vector3 impactPoint)
        {
            TerrainData terrainData = terrain.terrainData;
            List<TreeInstance> trees = new(terrainData.treeInstances);
            trees.RemoveAll(tree =>
            {
                Vector3 treeWorldPos = Vector3.Scale(tree.position, terrainData.size) + terrain.transform.position;
                return Vector3.Distance(treeWorldPos, impactPoint) < effectRadius;
            });
            terrainData.treeInstances = trees.ToArray();
            if (terrainData.detailWidth > 0 && terrainData.detailHeight > 0)
            {
                int detailMapRadius = (int)((effectRadius / terrainData.size.x) * terrainData.detailWidth);
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