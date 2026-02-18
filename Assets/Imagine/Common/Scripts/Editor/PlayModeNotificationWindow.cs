using System.Management.Instrumentation;
using UnityEditor;
using UnityEngine;

namespace Imagine.WebAR.Editor{
    [InitializeOnLoad]
    public static class PlayModeNotification
    {
        public static string PopUpProjectName{
            get{
                return "showLimitedFuncPopup" + Application.productName;
            }
        }

        static PlayModeNotification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            PlayModeNotificationWindow.HideWindow();

            if (state == PlayModeStateChange.EnteredPlayMode)
            { 
                if(EditorPrefs.GetString(PopUpProjectName,"true") == "true"){
                    PlayModeNotificationWindow.ShowWindow(); 
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode){
                PlayModeNotificationWindow.HideWindow();
            }
        }
    }

    public class PlayModeNotificationWindow : EditorWindow
    {
        // private Texture2D logoImage;
        private bool toggleValue = false;
        private static PlayModeNotificationWindow windowInstance;

        private void OnEnable()
        {
            // logoImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Imagine/Common/Textures/Editor/imagine-webar-logo-small.png");
        }

        public static void ShowWindow()
        {
            if(windowInstance == null){
                PlayModeNotificationWindow window = CreateInstance<PlayModeNotificationWindow>();
                windowInstance = window;//
            }
            var w = 380;
            var h = 270;
            windowInstance.position = new Rect(EditorGUIUtility.GetMainWindowPosition().width/2 - w/2, 140, w, h);
            windowInstance.ShowPopup(); 
        }

        public static void HideWindow(){
            if(windowInstance)
                windowInstance.Close();//
        }

        private void OnGUI()
        {
            Rect contentRect = new Rect(5, 5, position.width - 10, position.height - 10);
            GUI.Box(contentRect, GUIContent.none);

            GUILayout.Space(10); // Top padding
            GUILayout.BeginHorizontal();
            GUILayout.Space(10); // Left padding
            GUILayout.BeginVertical();

            // if (logoImage != null)
            // {
            //     GUILayout.Label(logoImage, GUILayout.Width(100), GUILayout.Height(100));
            // }

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 16;
            titleStyle.normal.textColor = Color.yellow;
            GUILayout.Label("⚠ Limited AR Functionality in the Editor ⚠", titleStyle, GUILayout.ExpandWidth(true));

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.wordWrap = true;
            subtitleStyle.richText = true;

            var message = "";
            message += "Imagine WebAR currently does not support AR testing in the Editor.\n\nTo test AR functionality:\n";
            message += "1. <b>Build-and-Run</b> to test in the web browser\n";
            message += "2. Or <b>upload a WebGL build</b> to your server, then access it on a mobile device.\n\n";
            message += "You can still debug using Editor Debug Mode and move the camera using keyboard controls.";
            GUILayout.Label(message, subtitleStyle);
            GUILayout.Space(10);

            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = new Color(0.5f, 0.6f, 1f); 
            linkStyle.wordWrap = true;

            if (GUILayout.Button("For support inquiries, click here to join our Discord server.", linkStyle))
            {
                Application.OpenURL("https://discord.com/invite/ypNARJJEbB");
            }
            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Got it", GUILayout.Width(100), GUILayout.Height(30)))
            {
                if(toggleValue){
                    EditorPrefs.SetString(PlayModeNotification.PopUpProjectName,"false");
                }

                this.Close();
            }

            GUILayout.Space(20);
            toggleValue = EditorGUILayout.Toggle(toggleValue, GUILayout.Width(15));
            GUILayout.Label("Don't show message again");
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(10); // Right padding
            GUILayout.EndHorizontal();
            GUILayout.Space(10); // Bottom padding
        }
    }
}
