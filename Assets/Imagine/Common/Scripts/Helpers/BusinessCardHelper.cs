using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace Imagine.WebAR{
    public class BusinessCardHelper : MonoBehaviour
    {
        [SerializeField] VideoPlayer playingVideo;
        [SerializeField] AudioSource playingAudio;

        public void OpenPhoneCall(string phone){
            if(playingVideo != null)
                playingVideo.Pause();
            if(playingAudio != null)
                playingAudio.Pause();
            
            StartCoroutine(WaitAndInvoke(0.2f, ()=>{
                Application.OpenURL("tel:" + phone);
            }));
        }

        public void OpenEmail(string emailData){
            if(playingVideo != null)
                playingVideo.Pause();
            if(playingAudio != null)
                playingAudio.Pause();
            
            StartCoroutine(WaitAndInvoke(0.2f, ()=>{
                Application.OpenURL("mailto:" + emailData);
            }));
        }

       void OnApplicationFocus(bool hasFocus)
        {
            if(hasFocus){
                playingAudio.Play();
                playingVideo.Play();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if(!pauseStatus){
                if(playingVideo != null)
                    playingAudio.Play();
                if(playingAudio != null)
                    playingVideo.Play();
            }
        }

        IEnumerator WaitAndInvoke(float delay, UnityAction action){
            yield return new WaitForSeconds(delay);
            action.Invoke();
        }
    }
}

