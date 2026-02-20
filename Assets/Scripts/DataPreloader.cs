using Imagine.WebAR;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
public class DataPreloader : MonoBehaviour
{
    //[SerializeField] private string apiUrl = "https://api.scripictural.tecshield.net//api/artworks/public/698f0ecd52abbdb60de402f1";
    [SerializeField] private ImageTracker tracker;
    private string baseUrl = "https://api.scripictural.tecshield.net//api/artworks/public/";
    private string apiId;
    private string apiUrl;
    [SerializeField] GameObject txtGo;
    private void Start()
    {
        //StartCoroutine(GetApiResponse());
        txtGo.gameObject.SetActive(true);
        //OnArtworkIdReceived("6996b3ee10215ae8f4cd72ca");
        //OnArtworkIdReceived("6996c4e23df07136b93b0e24");
    }
    public void OnArtworkIdReceived(string id)
    {
        apiId = id;
        Debug.Log("Artwork ID: " + id);
        apiUrl = baseUrl+ id;
        Debug.Log("Api Url is " + apiUrl);

        StartCoroutine(GetApiResponse());
    }
    private IEnumerator GetApiResponse()
    {
        print("Hitting Api");
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("User-Agent", "UnityPlayer");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Response Code: " + request.responseCode);
            Debug.Log("Full Response: " + request.downloadHandler.text);
            Debug.LogError("API Error: " + request.error);
        }
        else
        {
            string json = request.downloadHandler.text;
            Debug.Log("API Response: " + json);

            ApiResponse response = JsonUtility.FromJson<ApiResponse>(json);

            if (response != null && response.data != null)
            {
                StartCoroutine(DownloadTexture(response.data.imageURL, response.data.videoURL));
            }
        }
    }

    private IEnumerator DownloadTexture(string imageUrl, string videoUrl)
    {
        UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl);

        yield return imageRequest.SendWebRequest();

        Debug.Log("Image URL: " + imageUrl);

        if (imageRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image Download Error: " + imageRequest.error);
            yield break;
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);
            tracker.CreateRuntimeImageTarget(texture, videoUrl);
            txtGo.SetActive(false);
        }
    }
}


[Serializable]
public class ApiData
{
    public string imageURL;
    public string videoURL;
}

[Serializable]
public class ApiResponse
{
    public ApiData data;
}