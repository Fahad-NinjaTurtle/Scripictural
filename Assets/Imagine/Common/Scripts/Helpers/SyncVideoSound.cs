using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Imagine.WebAR.Samples
{
    public class SyncVideoSound : MonoBehaviour
    {
        [SerializeField] VideoPlayer video;
        [SerializeField] AudioSource sound;

        public float lastPos = 0;

        [Range(0.5f, 1.0f)]public float unsyncMaxVal = 0.5f;

        void Awake(){
            
        }
        void OnEnable(){
            StartCoroutine("SyncRoutine");
        }

        void OnDisable(){
            StopCoroutine("SyncRoutine");
        }
        
        
        IEnumerator SyncRoutine()
        {
            var videoRenderer = video.GetComponent<Renderer>();
            videoRenderer.enabled = false;

            while(!video.isPrepared){
                Debug.Log("Waiting video preparation");
                yield return null;
            }

            video.Play();
            sound.Play();
            sound.mute = true;

            video.time = lastPos;
            sound.time = lastPos;

            float lastUnsyncTime = unsyncMaxVal;

            while(true){
                if(video.time > 0.01f)
                {
                    videoRenderer.enabled = true;
                }
                // else if(!sound.isPlaying){
                //     sound.time = (float)video.time;
                //     sound.Play();
                // }
                    

                if(Mathf.Abs(sound.time - (float)video.time) > 0.1){
                    Debug.Log(sound.time + ", " + sound.clip.length);
                    
                    sound.time = (float)video.time;
                    Debug.Log(sound.time + "=>" + video.time);
                    Debug.Log("unsync - mute audio");

                    // sound.Stop();
                    sound.mute = true;
                    lastUnsyncTime = Time.time;
                }
                else{
                    // sound.Play();
                    if(Time.time - lastUnsyncTime > unsyncMaxVal){
                        sound.mute = false;
                        Debug.Log("sync - unmute audio");
                    }
                    else{
                        Debug.Log("Waiting to sync...  " + (Time.time - lastUnsyncTime) + "s");
                    }

                }
               

                lastPos = (float)video.time;
                //yield return new WaitForSeconds(0.05f);
                yield return null;
            }
        }

        
    }
}
