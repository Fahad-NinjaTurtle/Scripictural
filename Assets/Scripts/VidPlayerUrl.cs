using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VidPlayerUrl : MonoBehaviour
{
    [SerializeField] private VideoPlayer player;
    [SerializeField] RawImage videoDisplay;
    [SerializeField] GameObject loading;
    [SerializeField] RenderTexture renderTexture;
    private string videoUrl;

    private DebugText debugText;
    private void OnEnable()
    {
        TryPlayVideo();

        if (Application.platform == RuntimePlatform.Android || true)
        {
            videoDisplay.gameObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            loading.gameObject.transform.rotation = Quaternion.Euler(90, 0,0);
        }

        debugText = FindAnyObjectByType<DebugText>();

        UpdateDebugText();

    }

    private void UpdateDebugText()
    {
        if (debugText == null || videoDisplay == null)
            return;

        debugText.UpdateRotationTexts(transform, videoDisplay.transform);
    }
    private void Update()
    {
        videoDisplay.gameObject.transform.rotation = Quaternion.Euler(-360, 0, 0);
        loading.gameObject.transform.rotation = Quaternion.Euler(-360, 0, 0);
    }
    private void TryPlayVideo()
    {
        loading.SetActive(true);

        if(videoDisplay != null ) 
            videoDisplay.color = Color.clear;

        if (videoUrl == null)
            return;

        if (player != null)
        {
            player.url = videoUrl;
            player.playOnAwake = false;
            player.Prepare();

            player.prepareCompleted += OnVideoPrepared;
        }
    }
    private void OnVideoPrepared(VideoPlayer source)
    {
        source.Play();
        loading.SetActive(false);

        if(videoDisplay != null ) 
            videoDisplay.color = Color.white;
    }

    public void SetVideoUrl(string url)
    {
        videoUrl = url;
        print(videoUrl);
        TryPlayVideo();

    }
    public void ChangeRenderTextureSize(int x, int y)
    {
        renderTexture.Release();
        renderTexture.width = x;
        renderTexture.height = y;   
        renderTexture.Create();
    }
}
