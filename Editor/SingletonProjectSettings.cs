using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector.Editor;
using Peg.AutoCreate;
using System.Linq;
using Sirenix.Utilities;
using Peg.Collections;

namespace Peg.ToolboxEditor
{
    /// <summary>
    /// Provides a way to set values for monobehviours that
    /// derive from GlobalSingletonMonoBehaviour<> in the editor.
    /// </summary>
    public class SingletonProjectSettings : OdinEditorWindow
    {
        static OdinEditorWindow SingletonWindow;
        static readonly string SerializedFilesPath = "Resources/Serialization/Singletons";

        [MenuItem("Edit/Singletons")]
        static void InitWindow()
        {
            var window = GetWindow<SingletonProjectSettings>();
            window.Show();
        }


        Vector2 ScrollPos;
        HashMap<Type, object> EditCache;
        Dictionary<Type, bool> FoldFlags;
        
        /// <summary>
        /// 
        /// </summary>
        protected override void OnEnable()
        {
            EditCache = new(10);
            FoldFlags = new(10);
            ToolboxEditorUtility.ConfirmAssetDirectory(SerializedFilesPath, false);
            var singletonTypes = TypeHelper.GetTypesWithAttribute(typeof(AutoCreateAttribute))
                .Where(x => x.GetAttribute<AutoCreateAttribute>().ActionOnCreation == CreationActions.DeserializeSingletonData);
            foreach (var singletonType in singletonTypes)
            {
                
                var obj = Deserialize(BuildSavePath(PathType.Nativepath, singletonType));
                EditCache[singletonType] = obj ?? Activator.CreateInstance(singletonType);
                FoldFlags[singletonType] = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDisable()
        {
            var all = EditCache.SimpleKeys;
            for (int i = 0; i < EditCache.Count; i++)
            {
                var singletonObj = EditCache[all[i]];
                if (!TypeHelper.IsReferenceNull(singletonObj))
                {
                    Type type = singletonObj.GetType();
                    Serialize(singletonObj, BuildSavePath(PathType.Nativepath, type));
                }
            }

            EditCache.Clear();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnImGUI()
        {
            base.OnImGUI();
            ProcessUI();
        }

        void ProcessUI()
        {
            if (Application.isPlaying) this.Close();
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            GUILayout.Space(20);
            foreach (var kvp in EditCache)
            {
                var type = kvp.Key;
                var obj = kvp.Value;
                if (FoldFlags.TryGetValue(type, out bool fold))
                {
                    if (ClickableLayoutHeader(type.Name))
                    {
                        if (SingletonWindow != null)
                            SingletonWindow.Close();

                        SingletonWindow = InspectObjectInDropDown(obj, 1000);
                        SingletonWindow.OnClose += HandleCloseMiniInspector;
                    }
                }//end check for fold flag type
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 
        /// </summary>
        void HandleCloseMiniInspector()
        {
            SingletonWindow.OnClose -= HandleCloseMiniInspector;
            SingletonWindow = null;
        }

        /// <summary>
        /// Helper for displaying the header before a singleton section.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool ClickableLayoutHeader(string name)
        {
            var headerStyle = new GUIStyle(EditorStyles.toolbar);
            headerStyle.normal.textColor = Color.magenta;// Color.blue;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.fontSize = 16;
            headerStyle.fixedHeight = 45;
            bool result = GUILayout.Button(name, headerStyle, GUILayout.ExpandWidth(true));
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="singletonType"></param>
        /// <param name="dataPath"></param>
        /// <returns></returns>
        object Deserialize(string filePath)
        {
            if (!File.Exists(filePath + AutoCreator.SerialBytesExtension)) return null;

            var assetPath = ToolboxEditorUtility.NativePathToAssetPath(filePath);
            var refsAsset = AssetDatabase.LoadAssetAtPath<SingletonScriptableReferences>($"Assets{assetPath}.asset");

            var bytes = File.ReadAllBytes(filePath + AutoCreator.SerialBytesExtension);
            if (bytes == null)
            {
                Debug.LogError($"Null byte stream in Singleton Deserializer while loading {filePath}.");
                return null;
            }
            if(refsAsset == null)
                Debug.LogWarning($"Missing objects ref asset. UnityEngine.Objects will not be deserialized and linked properly while loading {filePath}.");

            var obj = Sirenix.Serialization.SerializationUtility.DeserializeValue<object>(bytes, Sirenix.Serialization.DataFormat.Binary, refsAsset != null ? refsAsset.SerializedRefs : null);
            Resources.UnloadAsset(refsAsset);
            return obj;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="singleton"></param>
        /// <param name="dataPath"></param>
        void Serialize(object singleton, string filePath)
        {
            var assetPath = $"Assets{ToolboxEditorUtility.NativePathToAssetPath(filePath)}.asset";
            //Debug.Log($"Saving singleton asset: '{assetPath}'");

            AssetDatabase.DeleteAsset(assetPath);
            var asset = ScriptableObjectUtility.CreateAsset<SingletonScriptableReferences>(assetPath);
            var bytes = Sirenix.Serialization.SerializationUtility.SerializeValue(singleton, Sirenix.Serialization.DataFormat.Binary, out asset.SerializedRefs);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            File.WriteAllBytes(filePath+AutoCreator.SerialBytesExtension, bytes);
            Resources.UnloadAsset(asset);
        }

        /// <summary>
        /// Generates a path for serializing a singleton in the project based on a unique file marker and the name of the type being serialized.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        string BuildSavePath(PathType pathType, Type type)
        {
            string path = (pathType == PathType.Nativepath ? Application.dataPath + "/" : String.Empty);
            return path + $"{SerializedFilesPath}/{type.Name}";
        }


    }
}
