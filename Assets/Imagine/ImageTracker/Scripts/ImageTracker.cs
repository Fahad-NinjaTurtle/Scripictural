using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
// #if IMAGINE_URP
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
// #endif
using System.Runtime.InteropServices;
using Unity.VisualScripting;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace Imagine.WebAR
{
    [System.Serializable]
    public class ImageTarget
    {
        public string id;
        public Transform transform;
        [HideInInspector] public Vector3 targetPos;
        [HideInInspector] public Quaternion targetRot;
    }
    [System.Serializable]
    public class RuntimeImageTarget
    {
        public string id;
        public string imageBase64;
    }
    public class ImageTracker : MonoBehaviour
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        [DllImport("__Internal")] private static extern void StartWebGLiTracker(string ids, string name);
        [DllImport("__Internal")] private static extern void StopWebGLiTracker();
        [DllImport("__Internal")] private static extern float SetWebGLiTrackerSettings(string settings);
        [DllImport("__Internal")] private static extern bool IsWebGLiTrackerReady();
        [DllImport("__Internal")] private static extern float DebugImageTarget(string id);
        [DllImport("__Internal")] private static extern bool IsWebGLImageTracked(string id);
#endif

        [SerializeField] private ARCamera trackerCam;
        [SerializeField] private List<ImageTarget> imageTargets;
        private Dictionary<string, ImageTarget> targets = new Dictionary<string, ImageTarget>();

        private enum TrackerOrigin { CAMERA_ORIGIN, FIRST_TARGET_ORIGIN }
        [SerializeField] private TrackerOrigin trackerOrigin;
        [SerializeField] private List<string> trackedIds = new List<string>();
        private string serializedIds = "";

        // [SerializeField] private bool overrideTrackerSettings = false;
        [SerializeField] private TrackerSettings trackerSettings;
        [SerializeField] private ImageTrackerGlobalSettings imageTrackerGlobalSettings;

        [SerializeField] private bool dontDeactivateOnLost = false;

        [SerializeField] private UnityEvent<string> OnImageFound, OnImageLost;

        [SerializeField] [Range(1f, 5f)] private float debugCamMoveSensitivity = 2f;
        [SerializeField] [Range(10f, 50f)] private float debugCamTiltSensitivity = 30f;

        private Vector3 firstTargetFinalPos, firstTargetCurrentPos;
        private Quaternion firstTargetFinalRot, firstTargetCurrentRot;
        private Transform dummyCamTransform;

        private int debugImageTargetIndex = 0;

        private bool isTrackerStopped = false;
        [Space][SerializeField] private bool startStopOnEnableDisable = false;
        [SerializeField] private bool stopOnDestroy = true;

        private Vector3 forward, up, right, pos;
        private Vector3 flippedScale = new Vector3(-1, 1, 1);
        private Quaternion rot;

        [SerializeField] Mesh artworkMesh;
        [SerializeField] GameObject artworkCanvas;
        IEnumerator Start()
        {

            if(transform.parent != null) {
                Debug.LogError("ImageTracker should be a root transform to receive Javascript messages");
            }

            if(trackerCam == null)
            {
                trackerCam = GameObject.FindFirstObjectByType<ARCamera>();
            }

            foreach (var i in imageTargets)
            {
                targets.Add(i.id, i);
                i.transform.GetComponent<Renderer>().enabled = false;
                i.transform.gameObject.SetActive(false);

                serializedIds += i.id;
                if (i != imageTargets[imageTargets.Count - 1])
                {
                    serializedIds += ",";
                }
            }
            Debug.Log(serializedIds);


            Application.targetFrameRate = (int)this.trackerSettings.targetFrameRate;
            

#if !UNITY_EDITOR && UNITY_WEBGL
            // while (!IsWebGLiTrackerReady())
            // {
            //     Debug.Log("waiting for tracker ready");
            //     yield return new WaitForSeconds(0.1f);
            // }

            StartWebGLiTracker(serializedIds, name);
            Debug.Log(trackerSettings.Serialize());
            SetWebGLiTrackerSettings(trackerSettings.Serialize());

#endif

            if( trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN && trackerSettings.maxSimultaneousTargets > 1){
                dummyCamTransform = (new GameObject("Dummy Cam Transform")).transform;
            }

            Debug.Log("tracker started!");

            yield break;
        }

        private void OnEnable(){
            if(startStopOnEnableDisable)
                StartTracker();
        }
        private void OnDisable(){
            if(startStopOnEnableDisable)
                StopTracker();
        }

        private void OnDestroy()
        {
            if(stopOnDestroy)
                StopTracker();
            //SetWebGLiTrackerSettings(trackerSettings.Serialize());
        }

        public void StartTracker()
        {
            if(!isTrackerStopped)
                return;

            Debug.Log("Starting Tracker...");
#if !UNITY_EDITOR && UNITY_WEBGL
            if (IsWebGLiTrackerReady())
            {
                StartWebGLiTracker(serializedIds, name);
            }
#endif
            isTrackerStopped = false;
        }

        public void StopTracker()
        {
            if(isTrackerStopped)
                return;

            Debug.Log("Stopping Tracker...");
#if !UNITY_EDITOR && UNITY_WEBGL
            if (IsWebGLiTrackerReady())
            {
                StopWebGLiTracker();
            }
#endif
            isTrackerStopped = true;
        }

        public void CreateRuntimeImageTarget(Texture2D texture, string videoUrl)
        {
            print("Creating target image");

            GameObject newImageGo = new GameObject("TargetArtwork");
            newImageGo.transform.SetParent(this.transform);
            newImageGo.transform.localPosition = Vector3.zero;
            newImageGo.transform.localRotation = Quaternion.identity;
            newImageGo.transform.localScale = new Vector3(1, 1, 1);

            MeshFilter filter = newImageGo.AddComponent<MeshFilter>();
            filter.mesh = artworkMesh;

            MeshRenderer meshRenderer = newImageGo.AddComponent<MeshRenderer>();
            Material newMat = new Material(Shader.Find("Unlit/Texture"));

            newMat.mainTexture = texture;
            meshRenderer.material = newMat;

            ImageTarget newTarget = new ImageTarget();
            newTarget.id = "runtimeArtwork";
            newTarget.transform = newImageGo.transform;

            var info = new ImageTargetInfo();
            imageTargets.Add(newTarget);
            targets.Add(newTarget.id, newTarget);
            
            info.id = newTarget.id;
            info.texture = texture;
            imageTrackerGlobalSettings.imageTargetInfos.Add(info);
            serializedIds += "," + newTarget.id;

            GameObject go = Instantiate(artworkCanvas, newImageGo.transform);
            go.GetComponent<VidPlayerUrl>().SetVideoUrl(videoUrl);
            StartCoroutine(Setup());

        }

        private IEnumerator Setup()
        {
            print("image tracker started");
            if (transform.parent != null)
            {
                Debug.LogError("ImageTracker should be a root transform to receive Javascript messages");
            }

            if (trackerCam == null)
            {
                trackerCam = GameObject.FindObjectOfType<ARCamera>();
            }
            foreach (var i in imageTargets)
            {
                if (i == null || i.transform == null)
                {
                    Debug.LogWarning($"ImageTarget is null or missing transform. Skipping.");
                    continue;
                }

                //targets.Add(i.id, i);

                Renderer rend = i.transform.GetComponent<Renderer>();
                if (rend != null)
                    rend.enabled = false;
                else
                    Debug.LogWarning($"Renderer not found on target '{i.id}'.");

                i.transform.gameObject.SetActive(false);

                //serializedIds += i.id;
                //if (i != imageTargets[imageTargets.Count - 1])
                //{
                //    serializedIds += ",";
                //}
            }

            Debug.Log("Log from Custom setup");
            Debug.Log(serializedIds);


            Application.targetFrameRate = (int)this.trackerSettings.targetFrameRate;


#if !UNITY_EDITOR && UNITY_WEBGL
            // while (!IsWebGLiTrackerReady())
            // {
            //     Debug.Log("waiting for tracker ready");
            //     yield return new WaitForSeconds(0.1f);
            // }

            StartWebGLiTracker(serializedIds, name);
            Debug.Log(trackerSettings.Serialize());
            SetWebGLiTrackerSettings(trackerSettings.Serialize());

#endif

            if (trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN && trackerSettings.maxSimultaneousTargets > 1)
            {
                dummyCamTransform = (new GameObject("Dummy Cam Transform")).transform;
            }

            Debug.Log("tracker started!");
            StartTracker();
            yield break;
        }
        public bool IsImageTracked(string id)
        {
            bool b = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            b =  IsWebGLImageTracked(id);
#endif
            return b;
        }

        void OnTrackingFound(string id)
        {
            if (!targets.ContainsKey(id))
                return;

            targets[id].transform.gameObject.SetActive(true);
            
            if(!trackedIds.Contains(id))
                trackedIds.Add(id);
            else
                Debug.LogError("Found an already tracked id - " + id);

            OnImageFound?.Invoke(id);
        }

        void OnTrackingLost(string id)
        {
            if (!targets.ContainsKey(id))
                return;

            targets[id].transform.gameObject.SetActive(false || dontDeactivateOnLost);

            var index = trackedIds.FindIndex(t => t == id);
            if (index > -1)
            {
                trackedIds.RemoveAt(index);
            }
            else{
                Debug.LogError("Lost an untracked id - " + id);
            }

            OnImageLost?.Invoke(id);
        }

        void OnTrack(string data)
        {
            ParseData(data);
        }

        void ParseData(string data)
        {
            string[] values = data.Split(new char[] { ',' });

            string id = values[0];
            if (!targets.ContainsKey(id))
                return;

            forward.x = float.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture);
            forward.y = float.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture);
            forward.z = float.Parse(values[6], System.Globalization.CultureInfo.InvariantCulture);

            up.x = float.Parse(values[7], System.Globalization.CultureInfo.InvariantCulture);
            up.y = float.Parse(values[8], System.Globalization.CultureInfo.InvariantCulture);
            up.z = float.Parse(values[9], System.Globalization.CultureInfo.InvariantCulture);

            right.x = float.Parse(values[10], System.Globalization.CultureInfo.InvariantCulture);
            right.y = float.Parse(values[11], System.Globalization.CultureInfo.InvariantCulture);
            right.z = float.Parse(values[12], System.Globalization.CultureInfo.InvariantCulture);

            rot = Quaternion.LookRotation(forward, up);

            pos.x = float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);
            pos.y = float.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture);
            pos.z = float.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture);

            var target = targets[id].transform;
            
            if(trackerCam.isFlipped){
                rot.eulerAngles = new Vector3(rot.eulerAngles.x, rot.eulerAngles.y * -1, rot.eulerAngles.z * -1);
                pos.x *= -1;
                target.localScale = flippedScale;
            }
            else{
                target.localScale = Vector3.one;
            }

            if (trackerOrigin == TrackerOrigin.CAMERA_ORIGIN)
            {
                if(!trackerSettings.useExtraSmoothing){
                    target.position = trackerCam.transform.TransformPoint(pos);
                    target.rotation = trackerCam.transform.rotation * rot;
                }
                else{
                    targets[id].targetPos = trackerCam.transform.TransformPoint(pos);
                    targets[id].targetRot = trackerCam.transform.rotation * rot;
                }
                
            }

            else if (trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN)
            {
                if (trackedIds[0] == id)
                {
                    //first target in origin
                    target.position = Vector3.zero;
                    target.rotation = Quaternion.identity;

                    if(!trackerSettings.useExtraSmoothing){
                        trackerCam.transform.position = Quaternion.Inverse(rot) * -pos;
                        trackerCam.transform.rotation = Quaternion.Inverse(rot);
                    }
                    else{
                        firstTargetFinalPos = pos;
                        firstTargetFinalRot = rot;
                        dummyCamTransform.position = Quaternion.Inverse(rot) * -pos;
                        dummyCamTransform.rotation = Quaternion.Inverse(rot);
                    }
                    
                }
                else
                {
                    //succeeding targets relative to camera
                    if(!trackerSettings.useExtraSmoothing){
                        target.position = trackerCam.transform.TransformPoint(pos);
                        target.rotation = trackerCam.transform.rotation * rot;
                    }
                    else{
                        targets[id].targetPos = dummyCamTransform.TransformPoint(pos);
                        targets[id].targetRot = dummyCamTransform.transform.rotation * rot;
                    }
                    
                    
                }
            }
        }

        private void Update()
        {
            if(trackerSettings.useExtraSmoothing){
                foreach(var target in imageTargets){
                    if(target.transform.gameObject.activeSelf){
                        target.transform.position = Vector3.Lerp(target.transform.position, target.targetPos, Time.deltaTime * trackerSettings.smoothenFactor);
                        target.transform.rotation = Quaternion.Slerp(target.transform.rotation, target.targetRot, Time.deltaTime * trackerSettings.smoothenFactor);
                    }
                }
                if(trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN){
                    firstTargetCurrentPos = Vector3.Lerp(firstTargetCurrentPos, firstTargetFinalPos, Time.deltaTime * trackerSettings.smoothenFactor);
                    firstTargetCurrentRot = Quaternion.Slerp(firstTargetCurrentRot, firstTargetFinalRot, Time.deltaTime * trackerSettings.smoothenFactor);
                    trackerCam.transform.position = Quaternion.Inverse(firstTargetCurrentRot) * -firstTargetCurrentPos;
                    trackerCam.transform.rotation = Quaternion.Inverse(firstTargetCurrentRot);
                }
            }

            if (trackerSettings.debugMode)
            {
#if ENABLE_INPUT_SYSTEM  
                if(Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
#else
                if (Input.GetKeyDown(KeyCode.I))
#endif
                {
                    if(debugImageTargetIndex >= imageTargets.Count)
                    {
                        debugImageTargetIndex = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
                        DebugImageTarget("");
#endif
                    }
                    else
                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                        DebugImageTarget(imageTargets[debugImageTargetIndex].id);
#endif
                        debugImageTargetIndex++;
                    }
                }
            }
#if UNITY_EDITOR
            Update_Debug();
#endif
        }

        private void Update_Debug()
        {
#if ENABLE_INPUT_SYSTEM  
            var x_left    = Keyboard.current != null && Keyboard.current.aKey.isPressed;
            var x_right   = Keyboard.current != null && Keyboard.current.dKey.isPressed;
            var z_forward = Keyboard.current != null && Keyboard.current.wKey.isPressed;
            var z_back    = Keyboard.current != null && Keyboard.current.sKey.isPressed;
            var y_up      = Keyboard.current != null && Keyboard.current.rKey.isPressed;
            var y_down    = Keyboard.current != null && Keyboard.current.fKey.isPressed;
#else
            var x_left = Input.GetKey(KeyCode.A);
            var x_right = Input.GetKey(KeyCode.D);
            var z_forward = Input.GetKey(KeyCode.W); ;
            var z_back = Input.GetKey(KeyCode.S); ;
            var y_up = Input.GetKey(KeyCode.R);
            var y_down = Input.GetKey(KeyCode.F);
#endif

            float speed = debugCamMoveSensitivity * Time.deltaTime;
            float dx = (x_right ? speed : 0) + (x_left ? -speed : 0);
            float dy = (y_up ? speed : 0) + (y_down ? -speed : 0);
            //float dsca = 1 + (z_forward ? speed : 0) + (z_back ? -speed : 0);
            float dz = (z_forward ? speed : 0) + (z_back ? -speed : 0);

#if ENABLE_INPUT_SYSTEM  
            var y_rot_left  = Keyboard.current != null && Keyboard.current.leftArrowKey.isPressed;
            var y_rot_right = Keyboard.current != null && Keyboard.current.rightArrowKey.isPressed;
            var x_rot_up    = Keyboard.current != null && Keyboard.current.upArrowKey.isPressed;
            var x_rot_down  = Keyboard.current != null && Keyboard.current.downArrowKey.isPressed;
            var z_rot_cw    = Keyboard.current != null && Keyboard.current.commaKey.isPressed;
            var z_rot_ccw   = Keyboard.current != null && Keyboard.current.periodKey.isPressed;
#else
            var y_rot_left = Input.GetKey(KeyCode.LeftArrow);
            var y_rot_right = Input.GetKey(KeyCode.RightArrow);
            var x_rot_up = Input.GetKey(KeyCode.UpArrow);
            var x_rot_down = Input.GetKey(KeyCode.DownArrow);
            var z_rot_cw = Input.GetKey(KeyCode.Comma);
            var z_rot_ccw = Input.GetKey(KeyCode.Period);
#endif

            var angularSpeed = debugCamTiltSensitivity * Time.deltaTime; //degrees per frame
            var d_rotx = (x_rot_up ? angularSpeed : 0) + (x_rot_down ? -angularSpeed : 0);
            var d_roty = (y_rot_right ? angularSpeed : 0) + (y_rot_left ? -angularSpeed : 0);
            var d_rotz = (z_rot_ccw ? angularSpeed : 0) + (z_rot_cw ? -angularSpeed : 0);

            var w = trackerCam.transform.rotation.w;
            var i = trackerCam.transform.rotation.x;
            var j = trackerCam.transform.rotation.y;
            var k = trackerCam.transform.rotation.z;

            var rot = new Quaternion(i, j, k, w);

            var dq = Quaternion.Euler(d_rotx, d_roty, d_rotz);
            rot *= dq;
            
            var dp = Vector3.right * dx + Vector3.up * dy + Vector3.forward * dz;
            trackerCam.transform.Translate(dp);
            trackerCam.transform.rotation = rot;
        }
    }
}

