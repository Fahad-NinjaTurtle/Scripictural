using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARDynamicTracker : MonoBehaviour
{

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private GameObject artworkPrefab;

    private Dictionary<string, string> imageVideoMap = new Dictionary<string, string>();
    private Dictionary<string , GameObject> spawnedArtworks = new Dictionary<string , GameObject>();

    private string baseUrl = "https://api.scripictural.tecshield.net//api/artworks/public/";

    private void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void Start()
    {
        // disable manager until library is ready
        trackedImageManager.enabled = false;
        OnArtworkIdReceived("698f0ecd52abbdb60de402f1");
    }
    public void OnArtworkIdReceived(string id)
    {
        Debug.Log("Artwork ID: " + id);
        string apiUrl = baseUrl + id;
        Debug.Log("Api Url: " + apiUrl);
        StartCoroutine(GetApiResponse(apiUrl));
    }

    private IEnumerator GetApiResponse(string apiUrl)
    {
        Debug.Log("Hitting Api");
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
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
        if (response != null && response.data != null)
        {
            yield return StartCoroutine(SetupARTarget(response.data.imageURL, response.data.videoURL));
        }
    }
    private IEnumerator SetupARTarget(string imageUrl, string videoUrl)
    {
        Debug.Log("Downloading image: " + imageUrl);
        UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return imageRequest.SendWebRequest();

        if (imageRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image Download Error: " + imageRequest.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);

        MutableRuntimeReferenceImageLibrary mutableLibrary =
            trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        if (mutableLibrary == null)
        {
            Debug.LogError("Device does not support mutable image libraries.");
            yield break;
        }

        string imageName = System.Guid.NewGuid().ToString();
        imageVideoMap[imageName] = videoUrl;

        // calculate physical size based on aspect ratio
        // set the width in meters to match your real printed image width
        float physicalWidth = 0.1f; // adjust this to your real image width in meters
        float aspect = (float)texture.height / texture.width;
        // for portrait: height > width, so we pass width as the physical size
        // AR Foundation uses the smaller dimension as reference
        Vector2 physicalSize = new Vector2(physicalWidth, physicalWidth * aspect);

        var jobHandle = mutableLibrary.ScheduleAddImageWithValidationJob(
            texture,
            imageName,
            physicalSize.x // AR Foundation uses width as the reference dimension
        );

        yield return new WaitUntil(() =>
            jobHandle.status == AddReferenceImageJobStatus.Success ||
            jobHandle.status == AddReferenceImageJobStatus.ErrorUnknown ||
            jobHandle.status == AddReferenceImageJobStatus.ErrorInvalidImage
        );

        if (jobHandle.status == AddReferenceImageJobStatus.Success)
        {
            Debug.Log("Image added to AR library successfully.");
            trackedImageManager.referenceLibrary = mutableLibrary;
            trackedImageManager.enabled = true;
        }
        else
        {
            Debug.LogError("Failed to add image to AR library. Status: " + jobHandle.status);
        }
    }
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var trackedImage in args.added)
        {
            SpawnArtwork(trackedImage);
        }

        foreach (var trackedImage in args.updated)
        {
            if (!spawnedArtworks.TryGetValue(trackedImage.referenceImage.name, out GameObject go))
                continue;

            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                go.SetActive(true);

                Vector2 detectedSize = trackedImage.size;
                RectTransform canvasRect = go.GetComponentInChildren<RectTransform>();
                if (canvasRect != null)
                {
                    float scaleX = detectedSize.x / canvasRect.rect.width;
                    float scaleY = detectedSize.y / canvasRect.rect.height;
                    go.transform.localScale = new Vector3(scaleX, scaleY, scaleX);
                }
            }
            else
            {
                go.SetActive(false);
            }
        }

        foreach (var trackedImage in args.removed)
        {
            if (spawnedArtworks.TryGetValue(trackedImage.referenceImage.name, out GameObject go))
            {
                Destroy(go);
                spawnedArtworks.Remove(trackedImage.referenceImage.name);
            }
        }
    }

    private void SpawnArtwork(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        if (!imageVideoMap.TryGetValue(imageName, out string videoUrl))
        {
            Debug.LogWarning("No video URL found for image: " + imageName);
            return;
        }

        GameObject go = Instantiate(artworkPrefab, trackedImage.transform.position, trackedImage.transform.rotation);
        go.transform.SetParent(trackedImage.transform);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localPosition = Vector3.zero;

        Vector2 detectedSize = trackedImage.size;
        Debug.Log($"DetectedSize: {detectedSize}");

        // get canvas and log everything
        Canvas canvas = go.GetComponentInChildren<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;

        if (canvasRect != null)
        {
            Debug.Log($"Canvas RenderMode: {canvas.renderMode}");
            Debug.Log($"CanvasRect size: {canvasRect.rect.width} x {canvasRect.rect.height}");
            Debug.Log($"Canvas localScale before: {canvasRect.localScale}");
        }

        // scale the spawned GO to match detected image size
        go.transform.localScale = new Vector3(detectedSize.x, detectedSize.y, detectedSize.x);

        VidPlayerUrl vidScript = go.GetComponentInChildren<VidPlayerUrl>();
        if (vidScript != null)
            vidScript.SetVideoUrl(videoUrl);
        else
            Debug.LogWarning("VidPlayerUrl not found on artwork prefab.");

        spawnedArtworks[imageName] = go;
    }
}
