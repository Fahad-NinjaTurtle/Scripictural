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
    private void OnEnable()
    {
        TryPlayVideo();

        if(Application.platform == RuntimePlatform.Android)
        {
            videoDisplay.gameObject.transform.localScale = new Vector3(15, 19, 1);
            loading.gameObject.transform.localScale = new Vector3(15, 25, 1);
        }
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
