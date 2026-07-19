using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shows the round map as a non-interactive, orbiting menu background.</summary>
public sealed class MainMenuRoundBackground : MonoBehaviour
{
    [SerializeField] private string roundSceneName = "PrototypeRoundScene";
    [SerializeField, Min(0f)] private float orbitSpeed = 3f;
    [SerializeField, Range(15f, 75f)] private float elevationAngle = 48f;
    [SerializeField, Min(0.1f)] private float distanceMultiplier = 0.8f;
    [SerializeField] private float targetHeightOffset = 1.5f;

    private Scene backgroundScene;
    private Vector3 target;
    private float orbitDistance = 35f;
    private float orbitAngle;
    private bool ready;

    private IEnumerator Start()
    {
        Scene existing = SceneManager.GetSceneByName(roundSceneName);
        if (!existing.IsValid() || !existing.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(roundSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[MainMenu] Could not load background scene '{roundSceneName}'.");
                yield break;
            }

            yield return load;
        }

        backgroundScene = SceneManager.GetSceneByName(roundSceneName);
        if (!backgroundScene.IsValid() || !backgroundScene.isLoaded)
            yield break;

        PrepareBackgroundScene();
        FrameMap();
        ready = true;
    }

    private void LateUpdate()
    {
        if (!ready)
            return;

        orbitAngle = Mathf.Repeat(orbitAngle + orbitSpeed * Time.unscaledDeltaTime, 360f);
        float elevation = elevationAngle * Mathf.Deg2Rad;
        float horizontalDistance = orbitDistance * Mathf.Cos(elevation);
        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 offset = new(
            Mathf.Sin(radians) * horizontalDistance,
            orbitDistance * Mathf.Sin(elevation),
            -Mathf.Cos(radians) * horizontalDistance);

        transform.position = target + offset;
        transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
    }

    private void PrepareBackgroundScene()
    {
        foreach (GameObject root in backgroundScene.GetRootGameObjects())
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
                sceneCamera.enabled = false;
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                source.enabled = false;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);
            foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                networkObject.gameObject.SetActive(false);
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
        }
    }

    private void FrameMap()
    {
        bool found = false;
        Bounds bounds = default;

        foreach (GameObject root in backgroundScene.GetRootGameObjects())
        foreach (Renderer mapRenderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!mapRenderer.enabled || mapRenderer is ParticleSystemRenderer ||
                mapRenderer.bounds.size.sqrMagnitude <= 0.001f)
                continue;

            if (!found)
            {
                bounds = mapRenderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(mapRenderer.bounds);
        }

        if (found)
        {
            target = bounds.center + Vector3.up * targetHeightOffset;
            float mapSpan = Mathf.Max(bounds.size.x, bounds.size.z);
            orbitDistance = Mathf.Max(10f, mapSpan * distanceMultiplier);
        }
        else
            target = Vector3.up * targetHeightOffset;

        Camera menuCamera = GetComponent<Camera>();
        if (menuCamera != null)
        {
            menuCamera.nearClipPlane = 0.1f;
            menuCamera.farClipPlane = Mathf.Max(1000f, orbitDistance * 4f);
        }
    }
}
