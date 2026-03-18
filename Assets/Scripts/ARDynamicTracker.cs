using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARDynamicTracker : MonoBehaviour
{
    [System.Serializable]
    public class RuntimeArtworkData
    {
        public string imageUrl;
        public string videoUrl;
        public float textureWidth;
        public float textureHeight;
        public float aspect;
        public Texture2D markerTexture;
        public string runtimeImageName;
    }

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private GameObject artworkPrefab;
    [SerializeField] private float physicalWidthMeters = 0.1f;
    [SerializeField] private bool hideOnLimitedTracking = true;

    private readonly Dictionary<string, RuntimeArtworkData> runtimeArtworkMap = new();
    private readonly Dictionary<string, GameObject> spawnedArtworks = new();
    private readonly Dictionary<string, string> artworkIdToRuntimeName = new();

    [SerializeField] GameObject imageDownloadingTextGO;

    private string baseUrl = "https://api.scripictural.tecshield.net/api/artworks/public/";

    private MutableRuntimeReferenceImageLibrary mutableLibrary;
    private readonly HashSet<string> processingArtworkIds = new();

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
        if (trackedImageManager == null)
        {
            Debug.LogError("TrackedImageManager is not assigned.");
            return;
        }

        mutableLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        if (mutableLibrary == null)
        {
            Debug.LogError("Device does not support mutable image libraries.");
            return;
        }

        trackedImageManager.referenceLibrary = mutableLibrary;
        trackedImageManager.enabled = false;
    }

    public void OnArtworkIdReceived(string artworkId)
    {
        if (string.IsNullOrWhiteSpace(artworkId))
        {
            Debug.LogError("Artwork ID is null or empty.");
            return;
        }

        Debug.Log($"OnArtworkIdReceived: {artworkId} | " +
                  $"AlreadyMapped: {artworkIdToRuntimeName.ContainsKey(artworkId)} | " +
                  $"Processing: {processingArtworkIds.Contains(artworkId)} | " +
                  $"LibraryNull: {mutableLibrary == null}");

        if (artworkIdToRuntimeName.ContainsKey(artworkId))
        {
            Debug.Log("Artwork already added: " + artworkId);
            return;
        }

        if (processingArtworkIds.Contains(artworkId))
        {
            Debug.Log("Already processing: " + artworkId);
            return;
        }

        string apiUrl = baseUrl + artworkId;
        StartCoroutine(GetApiResponse(apiUrl, artworkId));
    }

    private IEnumerator GetApiResponse(string apiUrl, string artworkId)
    {
        processingArtworkIds.Add(artworkId);
        imageDownloadingTextGO.SetActive(true);

        using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("User-Agent", "UnityPlayer");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("API Error: " + request.error);
            processingArtworkIds.Remove(artworkId);
            imageDownloadingTextGO.SetActive(false);
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("API Response: " + json);

        ApiResponse response = JsonUtility.FromJson<ApiResponse>(json);
        if (response == null || response.data == null)
        {
            Debug.LogError("Invalid API response.");
            processingArtworkIds.Remove(artworkId);
            imageDownloadingTextGO.SetActive(false);
            yield break;
        }

        yield return StartCoroutine(SetupARTarget(artworkId, response.data.imageURL, response.data.videoURL));

        processingArtworkIds.Remove(artworkId);
        imageDownloadingTextGO.SetActive(false);
    }

    private IEnumerator SetupARTarget(string artworkId, string imageUrl, string videoUrl)
    {
        if (mutableLibrary == null)
        {
            Debug.LogError("Mutable runtime image library is not initialized.");
            yield break;
        }

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

        string imageName = artworkId;
        float aspect = (float)texture.width / texture.height;

        runtimeArtworkMap[imageName] = new RuntimeArtworkData
        {
            imageUrl = imageUrl,
            videoUrl = videoUrl,
            textureWidth = texture.width,
            textureHeight = texture.height,
            aspect = aspect,
            markerTexture = texture,
            runtimeImageName = imageName
        };

        artworkIdToRuntimeName[artworkId] = imageName;

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
            runtimeArtworkMap.Remove(imageName);
            artworkIdToRuntimeName.Remove(artworkId);
            yield break;
        }

        if (!trackedImageManager.enabled)
        {
            trackedImageManager.enabled = true;
            Debug.Log($"Manager enabled for first image: {imageName}");
        }
        else
        {
            yield return StartCoroutine(RefreshLibraryReference());
        }

        Debug.Log($"Image added: {imageName} | Library count: {mutableLibrary.count}");
    }

    private IEnumerator RefreshLibraryReference()
    {
        var activeNames = new HashSet<string>(spawnedArtworks.Keys);
        trackedImageManager.referenceLibrary = mutableLibrary;
        yield return null;
        yield return null;
        Debug.Log($"Library reference refreshed. Active artworks: {activeNames.Count}");
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
        go.transform.position = trackedImage.transform.position;
        go.transform.rotation = trackedImage.transform.rotation;

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

        go.transform.position = trackedImage.transform.position;
        go.transform.rotation = trackedImage.transform.rotation;

        Vector2 detectedSize = trackedImage.size;
        if (detectedSize.x <= 0f || detectedSize.y <= 0f)
            return;

        float targetWidth, targetHeight;
        if (data.aspect >= 1f)
        {
            targetWidth = detectedSize.x;
            targetHeight = targetWidth / data.aspect;
        }
        else
        {
            targetHeight = detectedSize.y;
            targetWidth = targetHeight * data.aspect;
        }

        Canvas canvas = go.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return;

        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("Canvas render mode should be World Space for AR tracking.");
        }

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect == null)
            return;

        float ppu = canvas.referencePixelsPerUnit;
        if (ppu <= 0f) ppu = 100f;

        // sizeDelta in canvas pixel units. The canvas localScale must be (1/ppu) so
        // that these pixel units map 1:1 to meters. E.g. ppu=100 → scale=0.01.
        rect.sizeDelta = new Vector2(targetWidth * ppu, targetHeight * ppu);

        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // FIX 5: Ensure canvas scale converts ppu → meters correctly.
        float worldScale = 1.01f / ppu;
        rect.localScale = new Vector3(worldScale, worldScale, worldScale);
    }
}