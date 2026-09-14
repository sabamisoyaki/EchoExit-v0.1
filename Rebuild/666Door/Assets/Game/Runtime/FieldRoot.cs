using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Door666.Runtime
{
    /// <summary>The authored room in Field.unity. Screen scenes load it additively and rebuild only <see cref="Placements"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldRoot : MonoBehaviour
    {
        public Transform Placements;
        public DoorTarget ForwardDoor;
        public DoorTarget BackDoor;
        public NavMeshSurface Navigation;
        [Tooltip("Ceiling and fixtures hidden by the top-down stage editor.")]
        public GameObject[] Overhead = new GameObject[0];
        [Tooltip("Remote service-bay lamp that briefly drops out; the player route stays legible.")]
        public Light FlickerLamp;

        private float lightClock;

        public static IEnumerator EnsureLoaded()
        {
            if (SceneManager.GetSceneByName(GameConstants.FieldScene).isLoaded) yield break;
            var loading = SceneManager.LoadSceneAsync(GameConstants.FieldScene, LoadSceneMode.Additive);
            while (loading != null && !loading.isDone) yield return null;
        }

        /// <summary>Screen scenes are the active scene, so the field's own lighting settings are applied at runtime.</summary>
        public static void ApplyAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.21f, .23f, .16f);
            RenderSettings.ambientEquatorColor = new Color(.11f, .12f, .076f);
            RenderSettings.ambientGroundColor = new Color(.048f, .041f, .026f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = .18f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.075f, .082f, .055f);
            RenderSettings.fogDensity = .024f;
        }

        private void Update()
        {
            if (FlickerLamp == null) return;
            lightClock += Time.deltaTime;
            float phase = lightClock % 17.9f;
            FlickerLamp.intensity = phase > 16.9f && phase < 17.28f ? .12f : 1.2f;
        }
    }
}
