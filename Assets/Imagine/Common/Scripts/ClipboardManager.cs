using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;


namespace Imagine.WebAR
{
    public class ClipboardManager : MonoBehaviour
    {
        public static ClipboardManager Instance;
        UnityAction<string> OnReceivedClipboardText;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLRequestClipboardText();
#endif
        public void Awake(){
            Instance = this;
        }

        public void RequestClipboardText(UnityAction<string> OnReceivedClipboardText){
#if !UNITY_EDITOR && UNITY_WEBGL
            this.OnReceivedClipboardText = OnReceivedClipboardText;
            WebGLRequestClipboardText();
#else
            string clipboardText = GUIUtility.systemCopyBuffer;
            OnReceivedClipboardText?.Invoke(clipboardText);
#endif
        }

        public void ReceiveClipboardText(string text){
            OnReceivedClipboardText?.Invoke(text);
            OnReceivedClipboardText = null;
        }
    }
}

