using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VidPlayerUrl : MonoBehaviour
{
    [SerializeField] private VideoPlayer player;
    [SerializeField] RawImage videoDisplay;
    [SerializeField] GameObject loading;
    private string videoUrl;
    private void OnEnable()
    {
        TryPlayVideo();
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
}
