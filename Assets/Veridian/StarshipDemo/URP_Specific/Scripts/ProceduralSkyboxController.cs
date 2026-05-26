// Filename: ProceduralSkyboxController.cs
using UnityEngine;

namespace Veridian.Demo.Scene
{

    [ExecuteAlways]
    public class ProceduralSkyboxController : MonoBehaviour
    {
        [Header("Sky Settings")]
        [Tooltip("The main color of the upper sky.")]
        public Color SkyTint = new(0.3f, 0.5f, 0.7f);

        [Tooltip("The color of the sky at the horizon, which will blend with the water.")]
        public Color GroundColor = new(0.05f, 0.1f, 0.15f);

        [Tooltip("The size of the sun disk.")]
        [Range(0, 1)]
        public float SunSize = 0.04f;

        [Tooltip("How hazy the atmosphere is. Higher values create a softer horizon.")]
        [Range(0, 5)]
        public float AtmosphereThickness = 1.0f;

        [Tooltip("Overall brightness of the skybox.")]
        [Range(0, 8)]
        public float Exposure = 1.3f;


        // --- Internal Logic ---
        private Material _dynamicSkyMaterial;
        private Material _originalSkybox;
        private bool _isOriginalSkyboxStored = false;

        private void OnEnable()
        {
            if (!_isOriginalSkyboxStored)
            {
                _originalSkybox = RenderSettings.skybox;
                _isOriginalSkyboxStored = true;
            }

            // Use Unity's built-in Procedural Skybox shader
            var proceduralShader = Shader.Find("Skybox/Procedural");
            if (proceduralShader == null)
            {
                Debug.LogError("Could not find the 'Skybox/Procedural' shader. Please ensure it is included in the project's Graphics Settings.", this);
                return;
            }
            _dynamicSkyMaterial = new Material(proceduralShader);

            RenderSettings.skybox = _dynamicSkyMaterial;
            UpdateSkyProperties();
        }

        private void OnDisable()
        {
            if (_isOriginalSkyboxStored)
            {
                RenderSettings.skybox = _originalSkybox;
                _isOriginalSkyboxStored = false;
            }

            if (_dynamicSkyMaterial != null)
            {
                DestroyImmediate(_dynamicSkyMaterial);
            }
        }

        // This is called whenever you change a value in the inspector
        private void OnValidate()
        {
            if (_dynamicSkyMaterial != null && isActiveAndEnabled)
            {
                UpdateSkyProperties();
            }
        }

        private void UpdateSkyProperties()
        {
            // Set the properties on our dynamic material
            // Note: These property names (_SkyTint, _GroundColor etc.) are defined by Unity's shader
            _dynamicSkyMaterial.SetColor("_SkyTint", SkyTint);
            _dynamicSkyMaterial.SetColor("_GroundColor", GroundColor);
            _dynamicSkyMaterial.SetFloat("_SunSize", SunSize);
            _dynamicSkyMaterial.SetFloat("_AtmosphereThickness", AtmosphereThickness);
            _dynamicSkyMaterial.SetFloat("_Exposure", Exposure);
        }
    }
}