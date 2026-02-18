using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace Imagine.WebAR.Editor{
    [InitializeOnLoad]
    public static class ReviewNotification
    {
        static ReviewNotification()
        {
            // lastDayPopupShown = tsNow;
            // dontShowAgain = false;

            UpdateDaysUsed();

            if (!SessionState.GetBool("FirstInitDone", false))
            {
                SessionState.SetBool("FirstInitDone", true);

                //delay call due to race condition
                float startTime = (float)EditorApplication.timeSinceStartup;

                EditorApplication.update += DelayAction;

                void DelayAction()
                {
                    if ((float)EditorApplication.timeSinceStartup - startTime >= 3f)
                    {
                        // Debug.Log("Called after 3 seconds");
                        CheckAndShow();
                        EditorApplication.update -= DelayAction; // Stop update
                    }
                }
            }
        }

        static void UpdateDaysUsed(){
            var tsLastDayUsed = lastDayUsed;
            if(!SameDay(tsLastDayUsed, tsNow)){
                daysUsedCounter++;
                lastDayUsed = tsNow;
            }
        }

        static void CheckAndShow(){

            if (dontShowAgain)
            {
                return;
            }

            long cooldown = 30;
            var remaining = tsNow - lastDayPopupShown;
            if( remaining < cooldown){
                // Debug.Log($"review popup cooldown for {(cooldown-remaining)}");
                return;
            }

            var minDaysUsed = 10;//10;
            var minBuildTimes = 10;//20;

            var daysUsed = daysUsedCounter;
            var buildTimes = buildCounter;
            // Debug.Log($"daysUsed = {daysUsed}, buildTimes = {buildTimes}");


            if(daysUsed >= minDaysUsed && buildTimes >= minBuildTimes){
                //Show Window
                var pluginName = CommonMenu.GetPlugins()[0];
                ReviewNotificationWindow.ShowWindow(pluginName);
            }
        }

        public static string ProjId{
            get{
                return "reviewNotifPopup" + Application.productName;
            }
        }

        public static long tsNow{
            get{
                System.DateTime now = System.DateTime.Now; 
                return (long)(now - new System.DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        public static bool dontShowAgain{
            get{
                var dontShowAgainKey = ProjId + "dontShowAgain";
                return EditorPrefs.GetString(dontShowAgainKey, "false") == "true";
            }
            set{
                var dontShowAgainKey = ProjId + "dontShowAgain";
                EditorPrefs.SetString(dontShowAgainKey, value ? "true":"false");
            }
        }

        public static int buildCounter{
            get{
                var buildCounterKey = ProjId + "buildCtr";
                return EditorPrefs.GetInt(buildCounterKey, 0);
            }
            set{
                var buildCounterKey = ProjId + "buildCtr";
                EditorPrefs.SetInt(buildCounterKey, value);
            }
        }

        [PostProcessBuild]
        public static void IncrementBuildCounter(BuildTarget target, string buildPath){
    #if UNITY_WEBGL
            // Debug.Log("Increment build counter");
            buildCounter++;
    #endif
        }

        public static long lastDayUsed{
            get{
                var lastDayUsedKey = ProjId + "lastDayUsed";
                if(!EditorPrefs.HasKey(lastDayUsedKey))
                {
                    EditorPrefs.SetString(lastDayUsedKey, tsNow.ToString());
                }
                return long.Parse(EditorPrefs.GetString(lastDayUsedKey));
            }
            set{
                var lastDayUsedKey = ProjId + "lastDayUsed";
                EditorPrefs.SetString(lastDayUsedKey, value.ToString());
            }
        }

        public static int daysUsedCounter{
            get{
                var daysUsedCounterKey = ProjId + "daysUsedCtr";
                return EditorPrefs.GetInt(daysUsedCounterKey, 0);
            }
            set{
                var daysUsedCounterKey = ProjId + "daysUsedCtr";
                EditorPrefs.SetInt(daysUsedCounterKey, value);
            }
        }

        public static long lastDayPopupShown{
            get{
                var lastDayPopupShownKey = ProjId + "lastDayPopupShown";
                if(!EditorPrefs.HasKey(lastDayPopupShownKey))
                {
                    EditorPrefs.SetString(lastDayPopupShownKey, tsNow.ToString());
                }
                return long.Parse(EditorPrefs.GetString(lastDayPopupShownKey));
            }
            set{
                var lastDayPopupShownKey = ProjId + "lastDayPopupShown";
                EditorPrefs.SetString(lastDayPopupShownKey, value.ToString());
            }
        }

        public static bool SameDay(long ts1, long ts2)
        {
            System.DateTime date1 = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts1);
            System.DateTime date2 = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts2);
            return date1.Date == date2.Date;
        }
    }

    public class ReviewNotificationWindow : EditorWindow
    {

        private static ReviewNotificationWindow windowInstance;

        private static string pluginName;

        private void OnEnable()
        {
            
        }

        public static void ShowWindow(string pluginName)
        {
            if(windowInstance == null){
                ReviewNotificationWindow window = CreateInstance<ReviewNotificationWindow>();////
                windowInstance = window;//
            }
            var w = 300;
            var h = 427;
            windowInstance.position = new Rect(EditorGUIUtility.GetMainWindowPosition().width/2 - w/2, 150, w, h);
            // Debug.Log("windowInstance.position = " + windowInstance.position);

            ReviewNotificationWindow.pluginName = pluginName;
            windowInstance.ShowPopup();
        }

        public static void HideWindow(){
            if(windowInstance != null){
                windowInstance.Close();
            }
        }

        private void OnGUI()
        {
            Rect contentRect = new Rect(5, 5, position.width - 10, position.height - 10);
            GUI.Box(contentRect, GUIContent.none);

            var padding = 10;
            GUILayout.Space(padding); // Top padding
            GUILayout.BeginHorizontal();
            GUILayout.Space(padding); // Left padding
            GUILayout.BeginVertical();

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 22;
            titleStyle.richText = true;
            titleStyle.wordWrap = true;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            // titleStyle.normal.textColor = Color.yellow;
            GUILayout.Label("Please review\nImagine WebAR\n" + pluginName, titleStyle, GUILayout.ExpandWidth(true));
            
            titleStyle.normal.textColor = new Color(1, 0.85f, 0);
            GUILayout.Label("<size=30>★★★★★</size>", titleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(10);

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.wordWrap = true;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            var message = "";

            message += "We hope Imagine WebAR has helped you create great WebAR apps with our easy-to-use tools and reliable tracking. ";
            message += "It also saves you a lot on hosting fees while giving you full ownership over your app.\n\n";
            message += "If you're happy with our asset, we’d really appreciate a review on the Asset Store because your feedback means a lot to us! ♥";
            GUILayout.Label(message, subtitleStyle);
            GUILayout.Space(15);

            // GUILayout.BeginHorizontal();
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.normal.textColor = Color.white; 
            var bWidth = position.width - padding*2;
            if (GUILayout.Button("Leave a review", buttonStyle, GUILayout.Width(bWidth), GUILayout.Height(40)))
            {
                var url = "https://assetstore.unity.com/publishers/9634#plugins";
                if(Common.pluginDatas.ContainsKey(pluginName)){
                    url = Common.pluginDatas[pluginName].url;
                }
                Application.OpenURL(url);

                ReviewNotification.lastDayPopupShown = ReviewNotification.tsNow + 7776000;
                this.Close();
            }
            GUI.color = new Color(0.65f,0.65f,0.65f);
            GUILayout.Space(5);
            if (GUILayout.Button("Maybe later", buttonStyle, GUILayout.Width(bWidth), GUILayout.Height(40)))
            {
                ReviewNotification.lastDayPopupShown = ReviewNotification.tsNow;
                this.Close();
            }
            GUILayout.Space(5);

            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            bool showConfirmation = false;

            if (GUILayout.Button("Don't show again", GUILayout.Width(bWidth), GUILayout.Height(30)))
            {
                this.Close();
                showConfirmation = true;
                
            }

            GUILayout.EndVertical();
            GUILayout.Space(padding); // Right padding
            GUILayout.EndHorizontal();
            GUILayout.Space(padding); // Bottom padding

            //we call this here to avoid layout errors
            if(showConfirmation){
                if(!EditorUtility.DisplayDialog("Confirmation", "Don't show this message again?", "Cancel", "Confirm")){
                    ReviewNotification.dontShowAgain = true;
                }
            }
        }

    }

}
