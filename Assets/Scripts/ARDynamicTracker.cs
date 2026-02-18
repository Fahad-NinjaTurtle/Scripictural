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
        // same as DataPreloader's DownloadTexture
        Debug.Log("Downloading image: " + imageUrl);
        UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return imageRequest.SendWebRequest();

        if (imageRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image Download Error: " + imageRequest.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);

        // create mutable library and add image
        MutableRuntimeReferenceImageLibrary mutableLibrary =
            trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        if (mutableLibrary == null)
        {
            Debug.LogError("Device does not support mutable image libraries.");
            yield break;
        }

        string imageName = System.Guid.NewGuid().ToString(); // unique id for this image
        imageVideoMap[imageName] = videoUrl;

        var jobHandle = mutableLibrary.ScheduleAddImageWithValidationJob(
            texture,
            imageName,
            0.1f // physical size in meters - adjust to your printed image size
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
            trackedImageManager.enabled = true; // now start tracking
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
                go.transform.position = trackedImage.transform.position;
                go.transform.rotation = trackedImage.transform.rotation;
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

        // same as WebAR - reusing your existing VidPlayerUrl script
        VidPlayerUrl vidScript = go.GetComponentInChildren<VidPlayerUrl>();
        if (vidScript != null)
            vidScript.SetVideoUrl(videoUrl);
        else
            Debug.LogWarning("VidPlayerUrl not found on artwork prefab.");

        spawnedArtworks[imageName] = go;
    }
}
