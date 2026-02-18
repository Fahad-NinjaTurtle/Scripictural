using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;


namespace Imagine.WebAR.Editor
{
    [InitializeOnLoad]
    public static class Common{

        public static Dictionary<string, PluginData> pluginDatas;

        public class PluginData{
            public string url { get; set; }
            public string folder { get; set; }

            public PluginData(string folder, string url) => (this.folder, this.url) = (folder, url);
            
        }

        static Common(){
            pluginDatas = new Dictionary<string, PluginData>
            {
                { "ImageTracker", new PluginData(
                    "iTracker", 
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-image-tracker-240128#reviews"
                    )},
                { "WorldTracker", new PluginData(
                    "wTracker",
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-world-tracker-248561#reviews"
                    )},
                { "CurvedTracker", new PluginData(
                    "cTracker",
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-curved-tracker-262113#reviews"
                    )},
                { "FaceTracker",  new PluginData(
                    "fTracker",
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-face-tracker-279828#reviews"
                    )},
                { "HandTracker",  new PluginData(
                    "hTracker",
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-hand-tracker-305062#reviews"
                    )},
                { "BodyTracker",  new PluginData(
                    "bTracker",
                    "https://assetstore.unity.com/packages/tools/camera/imagine-webar-hand-tracker-305062#reviews" 
                    )},
            };
        }
    }

    public class CommonMenu
    {
        [MenuItem("Assets/Imagine WebAR/Create/AR Camera", false, 1120)]
		public static void CreateTrackerCamera()
		{
			GameObject prefab = (GameObject)AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imagine/Common/Prefabs/ARCamera.prefab");
			GameObject gameObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			PrefabUtility.UnpackPrefabInstance(gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			Selection.activeGameObject = gameObject;
			gameObject.name = "ARCamera";
		}

		[MenuItem("Assets/Imagine WebAR/Update Plugin to URP", false, 1200)]
		public static void SetURP()
		{
			if (EditorUtility.DisplayDialog(
				"Update Imagine WebAR Plugin to URP",
				"Please make sure that the Universal RP package is already installed before doing this step.",
				"Proceed",
				"Cancel"))
			{
                var folders = Directory.GetDirectories(Application.dataPath + "/Imagine/");
                var matFolders = new List<string>();
                foreach (var folder in folders){
                    matFolders.Add(folder + "/Materials");
                    matFolders.Add(folder + "/Demos/Materials");
                }

                foreach (var folder in matFolders){
                    
                    if(Directory.Exists(folder)){
                        // Debug.Log("processing materials in " + folder);

                        // string[] files = Directory.GetFiles(Application.dataPath + "/Imagine/ImageTracker/Demos/Materials", "*.mat", SearchOption.TopDirectoryOnly);
                        string[] files = Directory.GetFiles(folder, "*.mat", SearchOption.TopDirectoryOnly);
                        
                        foreach (var file in files)
                        {
                            Debug.Log("processing " + file.Replace(Application.dataPath,""));
                            var path = file.Replace(Application.dataPath, "Assets");
                            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (mat.shader.name == "Standard" || mat.shader.name == "Hidden/InternalErrorShader")
                            {
                                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                            }
                            else if (mat.shader.name == "Imagine/ARShadow")
                            {
                                mat.shader = Shader.Find("Imagine/ARShadowURP");
                            }
                        }
                    }
                }
				AddDefineSymbol("IMAGINE_URP");
				EditorUtility.DisplayDialog("Completed", "Imagine WebAR Plugin is now set to URP. \n\nSome URP features such as HDR and Post-Processing may be partially/fully unsupported.", "Ok");
			}
		}


		[MenuItem("Assets/Imagine WebAR/Roll-back Plugin to Built-In RP", false, 1201)]
		public static void SetBuiltInRP ()
		{
			if (EditorUtility.DisplayDialog(
				"Roll-back Imagine WebAR Plugin to Built-In RP",
				"Plese confirm.",
				"Proceed",
				"Cancel"))
			{

                var folders = Directory.GetDirectories(Application.dataPath + "/Imagine/");
                var matFolders = new List<string>();
                foreach (var folder in folders){
                    matFolders.Add(folder + "/Materials");
                    matFolders.Add(folder + "/Demos/Materials");
                }

                foreach (var folder in matFolders){
                    
                    if(Directory.Exists(folder)){
                        // Debug.Log("processing materials in " + folder.Replace(Application.dataPath,""));
                        // string[] files = Directory.GetFiles(Application.dataPath + "/Imagine/ImageTracker/Demos/Materials", "*.mat", SearchOption.TopDirectoryOnly);
                        string[] files = Directory.GetFiles(folder, "*.mat", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            Debug.Log("processing " + file.Replace(Application.dataPath,""));
                            var path = file.Replace(Application.dataPath, "Assets");
                            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (mat.shader.name == "Universal Render Pipeline/Lit" || mat.shader.name == "Hidden/InternalErrorShader")
                            {
                                mat.shader = Shader.Find("Standard");
                            }
                            else if (mat.shader.name == "Imagine/ARShadowURP")
                            {
                                mat.shader = Shader.Find("Imagine/ARShadow");
                            }
                        }
                    }
                }
				RemoveDefineSymbol("IMAGINE_URP");
				EditorUtility.DisplayDialog("Completed", "Imagine WebAR Plugin is now set to Built-In RP. Some edited materials may still require manual shader change","Ok");
			}
		}

        [MenuItem("Assets/Imagine WebAR/Uninstall", false, 2000)]
		public static void UnInstall ()
		{
            var templateFolders = GetTemplateFolders();
            var message = "This process, will delete the folders below. Please make sure you have a backup.\n\n";
            message += "Assets/Imagine\n";
            foreach (var folder in templateFolders){
                if(!Directory.Exists(folder)){
                    Debug.Log($"cant find {folder}");
                    continue;
                }
                message += folder.Replace(Application.dataPath, "Assets");
                message += "\n";
            }
            if(!EditorUtility.DisplayDialog ("Confirm Uninstall", 
                message,
                "Abort",
                "Uninstall"
            )){
                //delete here
                var imagineFolder = Path.Combine(Application.dataPath, "Imagine");
                Debug.LogWarning($"Deleting {imagineFolder}...");
                if(Directory.Exists(imagineFolder)){
                    Directory.Delete(imagineFolder, true);
                    File.Delete(imagineFolder + ".meta");
                    Debug.LogWarning($"Deleted {imagineFolder}");
                }
                foreach(var folder in templateFolders){
                    Debug.LogWarning($"Deleting {folder}...");

                    if(Directory.Exists(folder)){
                        Directory.Delete(folder, true);
                        File.Delete(folder + ".meta");
                        Debug.LogWarning($"Deleted {imagineFolder}");
                    }
                }
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Uninstall Complete", "Imagine WebAR was uninstalled successfully", "Okay");
            }
		}

        public static List<string> GetPlugins(){
            var folders = Directory.GetDirectories(Path.Combine(Application.dataPath, "Imagine"));
            var plugins = new List<string>();
            foreach(var folder in folders){
                var folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
                if(Common.pluginDatas.ContainsKey(folderName)){
                    var pluginName = folderName;//folder.Replace(Path.GetDirectoryName(folder) + Path.DirectorySeparatorChar, "");
                    // Debug.Log($"Detected {pluginName}");
                    plugins.Add(pluginName);
                }
            }
            return plugins;
        }

        public static List<string> GetTemplateFolders(){
            var templateFolders = new List<string>();
            foreach(var plugin in GetPlugins()){
                templateFolders.Add(Path.Combine(Application.dataPath,"WebGLTemplates", Common.pluginDatas[plugin].folder));
            }
            return templateFolders;
        }

		public static void AddDefineSymbol(string symbol)
        {
			// string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
			string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
            List<string> allDefines = definesString.Split(';').ToList();
			if (!allDefines.Contains(symbol))
				allDefines.Add(symbol);

            // PlayerSettings.SetScriptingDefineSymbolsForGroup(
            //      EditorUserBuildSettings.selectedBuildTargetGroup,
            //      string.Join(";", allDefines.ToArray()));

            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
                allDefines.ToArray()
            );

                 
			AssetDatabase.RefreshSettings();
		}

		public static void RemoveDefineSymbol(string symbol)
        {
			// string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
			string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
            List<string> allDefines = definesString.Split(';').ToList();
            allDefines.RemoveAll(s => s == symbol);
            
            // PlayerSettings.SetScriptingDefineSymbolsForGroup(
            //      EditorUserBuildSettings.selectedBuildTargetGroup,
            //      string.Join(";", allDefines.ToArray()));

            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
                allDefines.ToArray()
            );
                 
			AssetDatabase.RefreshSettings();

		}
    }
}
