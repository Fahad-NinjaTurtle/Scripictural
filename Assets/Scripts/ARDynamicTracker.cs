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
        public string artworkId;
        public string imageUrl;
        public string videoUrl;
        public float textureWidth;
        public float textureHeight;
        public float aspect;
        public Texture2D markerTexture;
        public string runtimeImageName;
        public string referenceImageGuid;
    }

    [System.Serializable]
    private class ApiResponse
    {
        public ArtworkData data;
    }

    [System.Serializable]
    private class ArtworkData
    {
        public string imageURL;
        public string videoURL;
    }

    [Header("AR")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private ARSession arSession;               // assign in Inspector
    [SerializeField] private GameObject artworkPrefab;
    [SerializeField] private float physicalWidthMeters = 0.1f;
    [SerializeField] private bool hideOnLimitedTracking = true;

    [Header("UI")]
    [SerializeField] private GameObject imageDownloadingTextGO;

    [Header("API")]
    [SerializeField] private string baseUrl = "https://api.scripictural.tecshield.net/api/artworks/public/";

    private readonly Dictionary<string, RuntimeArtworkData> runtimeArtworkMap = new();
    private readonly Dictionary<string, RuntimeArtworkData> runtimeArtworkByGuid = new();
    private readonly Dictionary<string, string> artworkIdToRuntimeName = new();
    private readonly HashSet<string> processingArtworkIds = new();
    private readonly Dictionary<TrackableId, GameObject> spawnedArtworks = new();
    private readonly List<ARTrackedImage> pendingTrackedImages = new();

    private MutableRuntimeReferenceImageLibrary mutableLibrary;
    private bool isLibraryReady;

    private readonly Queue<(string artworkId, string imageUrl, string videoUrl, Texture2D texture)> addImageQueue = new();
    private bool isAddingImage;

    // ──────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        // Do NOT call InitializeLibrary() in Awake.
        // On Android, CreateRuntimeLibrary() returns null if called before the AR
        // subsystem has started — resulting in a NullReferenceException crash when
        // ScheduleAddImageWithValidationJob is later called on a null library.
        // Library creation is deferred to Start → InitializeLibraryWhenReady().
    }

    private void Start()
    {
        StartCoroutine(InitializeLibraryWhenReady());
    }

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

    private void OnApplicationFocus(bool hasFocus)
    {
        // After the app is backgrounded and resumed, the AR subsystem can be
        // rebuilt by the OS. Re-assigning the library ref keeps tracking alive.
        if (hasFocus && mutableLibrary != null && trackedImageManager != null)
        {
            trackedImageManager.referenceLibrary = mutableLibrary;
            Debug.Log("AR library re-assigned on app focus.");
        }
    }

    private void Update()
    {
        // Retry tracked images whose artwork data wasn't registered yet
        for (int i = pendingTrackedImages.Count - 1; i >= 0; i--)
        {
            ARTrackedImage img = pendingTrackedImages[i];
            if (img == null) { pendingTrackedImages.RemoveAt(i); continue; }

            if (TryGetRuntimeArtworkData(img, out _))
            {
                pendingTrackedImages.RemoveAt(i);
                SpawnArtwork(img);
                UpdateArtworkState(img);
                UpdateArtworkTransform(img);
            }
        }

        // Drain the sequential add-image queue — one job at a time
        if (!isAddingImage && addImageQueue.Count > 0)
        {
            var entry = addImageQueue.Dequeue();
            StartCoroutine(AddImageToLibrary(entry.artworkId, entry.imageUrl, entry.videoUrl, entry.texture));
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Library Initialisation

    /// <summary>
    /// Waits until the AR session subsystem is running before creating the library.
    /// Calling CreateRuntimeLibrary() too early returns null on device.
    /// </summary>
    private IEnumerator InitializeLibraryWhenReady()
    {
        if (arSession != null)
        {
            // Wait until the session is at least initialising so the subsystem exists
            yield return new WaitUntil(() =>
                ARSession.state >= ARSessionState.SessionInitializing);
        }
        else
        {
            // No ARSession ref assigned — wait two frames as a safety buffer
            yield return null;
            yield return null;
        }

        InitializeLibrary();
    }

    private void InitializeLibrary()
    {
        if (trackedImageManager == null)
        {
            Debug.LogError("TrackedImageManager is not assigned.");
            return;
        }

        if (mutableLibrary != null)
        {
            isLibraryReady = true;
            return;
        }

        mutableLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        if (mutableLibrary == null)
        {
            Debug.LogError("Device does not support MutableRuntimeReferenceImageLibrary.");
            isLibraryReady = false;
            return;
        }

        trackedImageManager.referenceLibrary = mutableLibrary;
        trackedImageManager.enabled = true;
        isLibraryReady = true;

        Debug.Log("Mutable runtime image library initialised.");
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Public API

    public void OnArtworkIdReceived(string artworkId)
    {
        if (string.IsNullOrWhiteSpace(artworkId))
        {
            Debug.LogError("Artwork ID is null or empty.");
            return;
        }

        if (!isLibraryReady)
        {
            // Library is still initialising — wait then process
            StartCoroutine(WaitForLibraryThenReceive(artworkId));
            return;
        }

        ProcessArtworkId(artworkId);
    }

    private IEnumerator WaitForLibraryThenReceive(string artworkId)
    {
        yield return new WaitUntil(() => isLibraryReady);
        ProcessArtworkId(artworkId);
    }

    private void ProcessArtworkId(string artworkId)
    {
        if (artworkIdToRuntimeName.ContainsKey(artworkId))
        {
            Debug.Log("Artwork already registered: " + artworkId);
            return;
        }

        if (processingArtworkIds.Contains(artworkId))
        {
            Debug.Log("Artwork already being fetched: " + artworkId);
            return;
        }

        StartCoroutine(FetchArtworkAndEnqueue(artworkId));
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Network & Image Loading

    private IEnumerator FetchArtworkAndEnqueue(string artworkId)
    {
        processingArtworkIds.Add(artworkId);

        if (imageDownloadingTextGO != null)
            imageDownloadingTextGO.SetActive(true);

        // ── 1. Fetch API metadata ──────────────────────────────
        string apiUrl = baseUrl + artworkId;
        using UnityWebRequest apiReq = UnityWebRequest.Get(apiUrl);
        apiReq.SetRequestHeader("Accept", "application/json");
        apiReq.SetRequestHeader("Content-Type", "application/json");
        apiReq.SetRequestHeader("User-Agent", "UnityPlayer");

        yield return apiReq.SendWebRequest();

        if (apiReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"API error for '{artworkId}': {apiReq.error}");
            Cleanup(artworkId);
            yield break;
        }

        ApiResponse response = JsonUtility.FromJson<ApiResponse>(apiReq.downloadHandler.text);
        if (response?.data == null || string.IsNullOrWhiteSpace(response.data.imageURL))
        {
            Debug.LogError($"Invalid API response for '{artworkId}'.");
            Cleanup(artworkId);
            yield break;
        }

        string imageUrl = response.data.imageURL;
        string videoUrl = response.data.videoURL;

        // ── 2. Download marker texture — MUST be readable ──────
        // CRASH FIX: GetTexture() with nonReadable=true (the default) returns a
        // GPU-only texture. ScheduleAddImageWithValidationJob needs CPU pixel access
        // and calls GetRawTextureData() internally — on Android this throws a native
        // exception that bypasses C# try/catch and hard-crashes the app.
        // Passing nonReadable: false forces Unity to keep a CPU copy.
        using UnityWebRequest imgReq = UnityWebRequestTexture.GetTexture(imageUrl, false);
        yield return imgReq.SendWebRequest();

        if (imgReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Image download error for '{artworkId}': {imgReq.error}");
            Cleanup(artworkId);
            yield break;
        }

        Texture2D downloaded = DownloadHandlerTexture.GetContent(imgReq);
        if (downloaded == null)
        {
            Debug.LogError($"Null texture for '{artworkId}'.");
            Cleanup(artworkId);
            yield break;
        }

        // ── 3. Convert to readable RGBA32 ──────────────────────
        // ScheduleAddImageWithValidationJob requires RGBA32 or RGB24 specifically.
        // Downloaded textures may arrive as DXT1/DXT5/ETC2 depending on the CDN
        // or device. Convert unconditionally to be safe.
        Texture2D texture = ConvertToRGBA32Readable(downloaded);
        if (downloaded != texture)
            Destroy(downloaded);   // free the intermediate copy

        if (imageDownloadingTextGO != null)
            imageDownloadingTextGO.SetActive(false);

        // ── 4. Queue for sequential library addition ───────────
        addImageQueue.Enqueue((artworkId, imageUrl, videoUrl, texture));
        // processingArtworkIds stays set until AddImageToLibrary finishes
    }

    /// <summary>
    /// Converts any texture format into a CPU-readable RGBA32 texture via a
    /// RenderTexture blit. This is the only reliable way to handle all source
    /// formats including compressed ones (DXT, ETC2, ASTC) on Android.
    /// </summary>
    private Texture2D ConvertToRGBA32Readable(Texture2D source)
    {
        if (source.format == TextureFormat.RGBA32 && source.isReadable)
            return source;

        RenderTexture rt = RenderTexture.GetTemporary(
            source.width, source.height, 0, RenderTextureFormat.ARGB32);

        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        // false, false = no mipmaps, keep readable (do NOT pass makeNoLongerReadable=true)
        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        result.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    /// <summary>
    /// Adds one image to the mutable AR library.
    /// Polls job status with per-frame yields — avoids busy-spinning the main thread
    /// which can starve the job worker threads on low-end Android devices.
    /// All lookup maps are written only after confirmed job success.
    /// </summary>
    private IEnumerator AddImageToLibrary(
        string artworkId, string imageUrl, string videoUrl, Texture2D texture)
    {
        isAddingImage = true;

        string imageName = artworkId;
        float aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;

        var jobHandle = mutableLibrary.ScheduleAddImageWithValidationJob(
            texture, imageName, physicalWidthMeters);

        // Yield one frame at a time instead of WaitUntil — keeps main thread cooperative
        while (jobHandle.status == AddReferenceImageJobStatus.Pending)
            yield return null;

        if (jobHandle.status != AddReferenceImageJobStatus.Success)
        {
            Debug.LogError(
                $"Add image job failed. ArtworkId: '{artworkId}' Status: {jobHandle.status}");
            processingArtworkIds.Remove(artworkId);
            isAddingImage = false;
            yield break;
        }

        // Scan all library entries to find the GUID for this image by name
        string foundGuid = string.Empty;
        for (int i = 0; i < mutableLibrary.count; i++)
        {
            XRReferenceImage refImg = mutableLibrary[i];
            if (refImg.name == imageName)
            {
                foundGuid = refImg.guid.ToString();
                break;
            }
        }

        var artworkData = new RuntimeArtworkData
        {
            artworkId = artworkId,
            imageUrl = imageUrl,
            videoUrl = videoUrl,
            textureWidth = texture.width,
            textureHeight = texture.height,
            aspect = aspect,
            markerTexture = texture,
            runtimeImageName = imageName,
            referenceImageGuid = foundGuid
        };

        runtimeArtworkMap[imageName] = artworkData;
        artworkIdToRuntimeName[artworkId] = imageName;

        if (!string.IsNullOrWhiteSpace(foundGuid))
        {
            runtimeArtworkByGuid[foundGuid] = artworkData;
            Debug.Log($"✓ Image registered. Id: '{artworkId}' | GUID: '{foundGuid}' | Library count: {mutableLibrary.count}");
        }
        else
        {
            Debug.LogWarning($"Image registered without GUID for '{imageName}' — using name-based fallback.");
        }

        processingArtworkIds.Remove(artworkId);
        isAddingImage = false;
    }

    private void Cleanup(string artworkId)
    {
        processingArtworkIds.Remove(artworkId);
        if (imageDownloadingTextGO != null)
            imageDownloadingTextGO.SetActive(false);
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region AR Tracked Image Callbacks

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (ARTrackedImage img in args.added)
        {
            Debug.Log($"Tracked image ADDED | name: '{img.referenceImage.name}' | guid: {img.referenceImage.guid}");
            HandleTrackedImage(img);
        }

        foreach (ARTrackedImage img in args.updated)
            HandleTrackedImage(img);

        foreach (ARTrackedImage img in args.removed)
        {
            if (spawnedArtworks.TryGetValue(img.trackableId, out GameObject go))
            {
                Destroy(go);
                spawnedArtworks.Remove(img.trackableId);
            }
            pendingTrackedImages.Remove(img);
        }
    }

    private void HandleTrackedImage(ARTrackedImage img)
    {
        if (!TryGetRuntimeArtworkData(img, out _))
        {
            if (!pendingTrackedImages.Contains(img))
            {
                Debug.Log($"Artwork data not yet ready for '{img.referenceImage.name}' — queued for retry.");
                pendingTrackedImages.Add(img);
            }
            return;
        }

        SpawnArtwork(img);
        UpdateArtworkState(img);
        UpdateArtworkTransform(img);
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Artwork Spawn & Update

    private bool TryGetRuntimeArtworkData(ARTrackedImage trackedImage, out RuntimeArtworkData data)
    {
        data = null;
        if (trackedImage == null) return false;

        string guidKey = trackedImage.referenceImage.guid.ToString();
        if (!string.IsNullOrWhiteSpace(guidKey) && runtimeArtworkByGuid.TryGetValue(guidKey, out data))
            return true;

        string imageName = trackedImage.referenceImage.name;
        if (!string.IsNullOrWhiteSpace(imageName) && runtimeArtworkMap.TryGetValue(imageName, out data))
            return true;

        return false;
    }

    private void SpawnArtwork(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;

        TrackableId trackableId = trackedImage.trackableId;
        if (spawnedArtworks.ContainsKey(trackableId)) return;

        if (!TryGetRuntimeArtworkData(trackedImage, out RuntimeArtworkData data))
        {
            Debug.LogWarning($"SpawnArtwork: no data for '{trackedImage.referenceImage.name}'");
            return;
        }

        if (artworkPrefab == null)
        {
            Debug.LogError("Artwork prefab is not assigned.");
            return;
        }

        // Instantiate at world pose — NOT parented to trackedImage.transform.
        // Parenting causes one-frame pose lag and jump artefacts on tracking state
        // changes because AR Foundation updates tracked image transforms on its own
        // update cycle. UpdateArtworkTransform syncs position/rotation each callback.
        GameObject go = Instantiate(
            artworkPrefab,
            trackedImage.transform.position,
            trackedImage.transform.rotation);

        VidPlayerUrl vidScript = go.GetComponentInChildren<VidPlayerUrl>(true);
        if (vidScript != null)
        {
            vidScript.SetVideoUrl(data.videoUrl);
            vidScript.ChangeRenderTextureSize(
                Mathf.Max(256, Mathf.RoundToInt(data.textureWidth / 4f)),
                Mathf.Max(256, Mathf.RoundToInt(data.textureHeight / 4f)));
        }
        else
        {
            Debug.LogWarning("VidPlayerUrl not found on artwork prefab.");
        }

        spawnedArtworks[trackableId] = go;
        Debug.Log($"Spawned artwork '{data.artworkId}' at trackableId {trackableId}");
    }

    private void UpdateArtworkState(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;
        if (!spawnedArtworks.TryGetValue(trackedImage.trackableId, out GameObject go)) return;

        bool shouldShow = trackedImage.trackingState == TrackingState.Tracking ||
                          (!hideOnLimitedTracking && trackedImage.trackingState == TrackingState.Limited);
        go.SetActive(shouldShow);
    }

    private void UpdateArtworkTransform(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;
        if (!spawnedArtworks.TryGetValue(trackedImage.trackableId, out GameObject go)) return;
        if (!TryGetRuntimeArtworkData(trackedImage, out RuntimeArtworkData data)) return;

        // Always follow the tracked image's world pose
        go.transform.position = trackedImage.transform.position;
        go.transform.rotation = trackedImage.transform.rotation;

        Vector2 detectedSize = trackedImage.size;
        if (detectedSize.x <= 0f || detectedSize.y <= 0f) return;

        float targetWidth, targetHeight;
        if (data.aspect >= 1f)
        {
            targetWidth = detectedSize.x;
            targetHeight = targetWidth / Mathf.Max(0.0001f, data.aspect);
        }
        else
        {
            targetHeight = detectedSize.y;
            targetWidth = targetHeight * data.aspect;
        }

        Canvas canvas = go.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect == null) return;

        float ppu = canvas.referencePixelsPerUnit > 0f ? canvas.referencePixelsPerUnit : 100f;
        rect.sizeDelta = new Vector2(targetWidth * ppu, targetHeight * ppu);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
    }

    #endregion

    // ──────────────────────────────────────────────────────────
    #region Public Helpers

    public Texture2D GetMarkerTextureByArtworkId(string artworkId)
    {
        if (string.IsNullOrWhiteSpace(artworkId)) return null;
        if (!artworkIdToRuntimeName.TryGetValue(artworkId, out string name)) return null;
        if (!runtimeArtworkMap.TryGetValue(name, out RuntimeArtworkData data)) return null;
        return data.markerTexture;
    }

    public RuntimeArtworkData GetArtworkDataByArtworkId(string artworkId)
    {
        if (string.IsNullOrWhiteSpace(artworkId)) return null;
        if (!artworkIdToRuntimeName.TryGetValue(artworkId, out string name)) return null;
        if (!runtimeArtworkMap.TryGetValue(name, out RuntimeArtworkData data)) return null;
        return data;
    }

    #endregion
}