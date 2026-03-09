using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARDynamicTracker : MonoBehaviour
{
    [System.Serializable]
    private class RuntimeArtworkData
    {
        public string videoUrl;
        public float textureWidth;
        public float textureHeight;
        public float aspect;
    }

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private GameObject artworkPrefab;
    [SerializeField] private float physicalWidthMeters = 0.1f;
    [SerializeField] private bool hideOnLimitedTracking = true;

    private readonly Dictionary<string, RuntimeArtworkData> runtimeArtworkMap = new();
    private readonly Dictionary<string, GameObject> spawnedArtworks = new();

    private string baseUrl = "https://api.scripictural.tecshield.net/api/artworks/public/";

    private void OnEnable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void Start()
    {
        trackedImageManager.enabled = false;
        //OnArtworkIdReceived("69a134b95a210c8963b39f79");
        //OnArtworkIdReceived("699e5f4ffcb260f3f402589d");
        //OnArtworkIdReceived("699e98ac2b4731e78c3f1ff4");
    }

    public void OnArtworkIdReceived(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("Artwork ID is null or empty.");
            return;
        }

        string apiUrl = baseUrl + id;
        StartCoroutine(GetApiResponse(apiUrl));
    }

    private IEnumerator GetApiResponse(string apiUrl)
    {
        using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("User-Agent", "UnityPlayer");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("API Error: " + request.error);
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("API Response: " + json);

        ApiResponse response = JsonUtility.FromJson<ApiResponse>(json);
        if (response == null || response.data == null)
        {
            Debug.LogError("Invalid API response.");
            yield break;
        }

        yield return StartCoroutine(SetupARTarget(response.data.imageURL, response.data.videoURL));
    }

    private IEnumerator SetupARTarget(string imageUrl, string videoUrl)
    {
        using UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return imageRequest.SendWebRequest();

        if (imageRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image Download Error: " + imageRequest.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);
        if (texture == null)
        {
            Debug.LogError("Downloaded texture is null.");
            yield break;
        }

        MutableRuntimeReferenceImageLibrary mutableLibrary =
            trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        if (mutableLibrary == null)
        {
            Debug.LogError("Device does not support mutable image libraries.");
            yield break;
        }

        string imageName = System.Guid.NewGuid().ToString();
        float aspect = (float)texture.width / texture.height;

        runtimeArtworkMap[imageName] = new RuntimeArtworkData
        {
            videoUrl = videoUrl,
            textureWidth = texture.width,
            textureHeight = texture.height,
            aspect = aspect
        };

        var jobHandle = mutableLibrary.ScheduleAddImageWithValidationJob(
            texture,
            imageName,
            physicalWidthMeters
        );

        yield return new WaitUntil(() =>
            jobHandle.status == AddReferenceImageJobStatus.Success ||
            jobHandle.status == AddReferenceImageJobStatus.ErrorUnknown ||
            jobHandle.status == AddReferenceImageJobStatus.ErrorInvalidImage
        );

        if (jobHandle.status != AddReferenceImageJobStatus.Success)
        {
            Debug.LogError("Failed to add image to AR library. Status: " + jobHandle.status);
            yield break;
        }

        trackedImageManager.referenceLibrary = mutableLibrary;
        trackedImageManager.enabled = true;

        Debug.Log("Image added to AR library successfully.");
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (ARTrackedImage trackedImage in args.added)
        {
            SpawnArtwork(trackedImage);
            UpdateArtworkTransform(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in args.updated)
        {
            UpdateArtworkState(trackedImage);
            UpdateArtworkTransform(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in args.removed)
        {
            string imageName = trackedImage.referenceImage.name;
            if (spawnedArtworks.TryGetValue(imageName, out GameObject go))
            {
                Destroy(go);
                spawnedArtworks.Remove(imageName);
            }
        }
    }

    private void SpawnArtwork(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        if (spawnedArtworks.ContainsKey(imageName))
            return;

        if (!runtimeArtworkMap.TryGetValue(imageName, out RuntimeArtworkData data))
        {
            Debug.LogWarning("No runtime artwork data found for image: " + imageName);
            return;
        }

        GameObject go = Instantiate(artworkPrefab, trackedImage.transform);
        go.transform.localPosition = Vector3.zero;

        VidPlayerUrl vidScript = go.GetComponentInChildren<VidPlayerUrl>(true);
        if (vidScript != null)
        {
            vidScript.SetVideoUrl(data.videoUrl);
            vidScript.ChangeRenderTextureSize(
                Mathf.Max(256, Mathf.RoundToInt(data.textureWidth / 4f)),
                Mathf.Max(256, Mathf.RoundToInt(data.textureHeight / 4f))
            );
        }
        else
        {
            Debug.LogWarning("VidPlayerUrl not found on artwork prefab.");
        }

        spawnedArtworks[imageName] = go;
    }

    private void UpdateArtworkState(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        if (!spawnedArtworks.TryGetValue(imageName, out GameObject go))
            return;

        bool shouldShow = trackedImage.trackingState == TrackingState.Tracking;
        if (!shouldShow && !hideOnLimitedTracking)
            shouldShow = true;

        go.SetActive(shouldShow);
    }

    private void UpdateArtworkTransform(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        if (!spawnedArtworks.TryGetValue(imageName, out GameObject go))
            return;

        if (!runtimeArtworkMap.TryGetValue(imageName, out RuntimeArtworkData data))
            return;

        Vector2 detectedSize = trackedImage.size;
        if (detectedSize.x <= 0f || detectedSize.y <= 0f)
            return;

        float targetWidth = detectedSize.x;
        float targetHeight = targetWidth / data.aspect;

        if (data.aspect < 1f)
        {
            targetHeight = detectedSize.y;
            targetWidth = targetHeight * data.aspect;
        }

        Canvas canvas = go.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect == null)
            return;

        float ppu = canvas.referencePixelsPerUnit;
        if (ppu <= 0f)
            ppu = 100f;

        rect.sizeDelta = new Vector2(targetWidth * ppu, targetHeight * ppu);
        rect.localPosition = Vector3.zero;
        //rect.localRotation = Quaternion.identity;
    }
}