using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace PlayFreelyXiYou.GameEditor
{
    [Serializable]
    public sealed class CodeTemplateLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class TemplateInfo
        {
            [Tooltip("功能名字会展示在左侧列表按钮上的")]
            public string TemplateName = "新建功能";

            [Tooltip("模板对应的代码内容")]
            [TextArea(10, 40)]
            public string CodeContent = "// 在此输入模板代码";
        }

        [SerializeField]
        [FormerlySerializedAs("Templates")]
        private List<TemplateInfo> templates = new List<TemplateInfo>();

        public List<TemplateInfo> Templates => templates;

        public void EnsureInitialized()
        {
            if (templates == null)
            {
                templates = new List<TemplateInfo>();
            }
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureInitialized();
        }
        
        public static CodeTemplateLibrary LoadExistingLibrary(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            assetPath = assetPath.Replace('\\', '/');
            var library = AssetDatabase.LoadAssetAtPath<CodeTemplateLibrary>(assetPath);
            if (library == null)
            {
                return null;
            }

            library.EnsureInitialized();
            return library;
        }

        public static CodeTemplateLibrary LoadOrCreateLibrary(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');

            var library = AssetDatabase.LoadAssetAtPath<CodeTemplateLibrary>(assetPath);
            if (library != null)
            {
                library.EnsureInitialized();
                return library;
            }

            string[] guids = AssetDatabase.FindAssets("t:CodeTemplateLibrary");
            CodeTemplateLibrary fallback = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var loaded = AssetDatabase.LoadAssetAtPath<CodeTemplateLibrary>(path);
                if (loaded == null)
                {
                    continue;
                }

                loaded.EnsureInitialized();
                if (string.Equals(path, assetPath, System.StringComparison.Ordinal))
                {
                    return loaded;
                }

                if (fallback == null)
                {
                    fallback = loaded;
                }
            }

            if (fallback != null)
            {
                return fallback;
            }

            EnsureAssetFolderExists(Path.GetDirectoryName(assetPath));
            var created = CreateInstance<CodeTemplateLibrary>();
            created.EnsureInitialized();
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            library = AssetDatabase.LoadAssetAtPath<CodeTemplateLibrary>(assetPath);
            if (library != null)
            {
                library.EnsureInitialized();
            }
            return library;
        }

        private static void EnsureAssetFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentFolder = Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                EnsureAssetFolderExists(parentFolder);
            }

            string folderName = Path.GetFileName(folderPath);
            string parentPath = string.IsNullOrEmpty(parentFolder) ? "Assets" : parentFolder.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folderName) && !AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
#endif
    }
}
