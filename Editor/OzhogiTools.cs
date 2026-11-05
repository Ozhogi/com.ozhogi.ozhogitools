using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace OzhogiTools.Editor
{
    public class FbxAnimationExtractor : EditorWindow
    {
        private Object selectedFbx;
        private List<AnimationClip> foundClips = new List<AnimationClip>();

        private string targetFolderPath;
        private AnimationPreset selectedPreset;
        private bool animationMode;
        private List<WatchEntry> watchlist;
        private Vector2 scrollPos;

        private const string LastPathKey = "FbxAnimExtractor_LastPath";
        private const string PresetPathKey = "FbxAnimExtractor_PresetPath";
        private const string AnimationModeKey = "FbxAnimExtractor_AnimationMode";
        private const string WatchlistKey = "FbxAnimExtractor_Watchlist";

        private struct WatchEntry
        {
            public string fbxPath;
            public string targetFolder;
        }

        [MenuItem("Tools/OzhogiTools/Extract Animations %#A")]
        public static void ShowWindow()
        {
            var window = GetWindow<FbxAnimationExtractor>("Export Animations");
            window.selectedFbx = Selection.activeObject;
            window.LoadClips();
            window.minSize = new Vector2(500, 400);

            window.targetFolderPath = EditorPrefs.GetString(LastPathKey, "");
            window.animationMode = EditorPrefs.GetBool(AnimationModeKey, false);

            string presetPath = EditorPrefs.GetString(PresetPathKey, "");
            if (!string.IsNullOrEmpty(presetPath))
            {
                window.selectedPreset = AssetDatabase.LoadAssetAtPath<AnimationPreset>(presetPath);
            }

            window.LoadWatchlist();

            if (string.IsNullOrEmpty(window.targetFolderPath) && window.selectedFbx != null)
            {
                window.targetFolderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(window.selectedFbx));
            }
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject == null) return;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;

            selectedFbx = Selection.activeObject;
            LoadClips();
            Repaint();
        }

        private void LoadClips()
        {
            foundClips.Clear();
            if (selectedFbx == null) return;

            string path = AssetDatabase.GetAssetPath(selectedFbx);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    foundClips.Add(clip);
                }
            }
        }

        private void LoadWatchlist()
        {
            watchlist = new List<WatchEntry>();
            string saved = EditorPrefs.GetString(WatchlistKey, "");
            if (string.IsNullOrEmpty(saved)) return;

            var entries = saved.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|');
                if (parts.Length == 2)
                {
                    watchlist.Add(new WatchEntry { fbxPath = parts[0], targetFolder = parts[1] });
                }
            }
        }

        private void SaveWatchlist()
        {
            var entries = watchlist.Select(e => $"{e.fbxPath}|{e.targetFolder}");
            EditorPrefs.SetString(WatchlistKey, string.Join(";", entries));
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Source FBX:", selectedFbx != null ? selectedFbx.name : "Not selected", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.TextField("Export Path:", targetFolderPath ?? "Not selected");
            GUI.enabled = true;

            if (GUILayout.Button("...", GUILayout.Width(30), GUILayout.Height(18)))
            {
                OpenFolderDialog();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Optimization:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            AnimationPreset newPreset = (AnimationPreset)EditorGUILayout.ObjectField("Animation Preset:", selectedPreset, typeof(AnimationPreset), false);
            if (newPreset != selectedPreset)
            {
                selectedPreset = newPreset;
                if (selectedPreset != null)
                {
                    string presetPath = AssetDatabase.GetAssetPath(selectedPreset);
                    EditorPrefs.SetString(PresetPathKey, presetPath);
                }
                else
                {
                    EditorPrefs.SetString(PresetPathKey, "");
                }
            }

            if (selectedPreset != null)
            {
                EditorGUILayout.LabelField($"Root bones: {string.Join(", ", selectedPreset.rootBones)}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            bool newAnimationMode = EditorGUILayout.Toggle("Animation Mode", animationMode);
            if (newAnimationMode != animationMode)
            {
                animationMode = newAnimationMode;
                EditorPrefs.SetBool(AnimationModeKey, animationMode);
                Repaint();
            }

            if (animationMode)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Watchlist:", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                int watchIndexToRemove = -1;

                for (int i = 0; i < watchlist.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"{i + 1}.", GUILayout.Width(25));
                    GUILayout.Label(Path.GetFileName(watchlist[i].fbxPath), GUILayout.Width(120));
                    GUILayout.Label("→", GUILayout.Width(20));
                    GUILayout.Label(watchlist[i].targetFolder);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        watchIndexToRemove = i;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (watchIndexToRemove >= 0)
                {
                    watchlist.RemoveAt(watchIndexToRemove);
                    SaveWatchlist();
                    Repaint();
                }

                EditorGUILayout.Space(5);
                EditorGUI.BeginDisabledGroup(selectedFbx == null || string.IsNullOrEmpty(targetFolderPath));
                if (GUILayout.Button("Add to Watchlist"))
                {
                    string fbxPath = AssetDatabase.GetAssetPath(selectedFbx);
                    if (!watchlist.Any(e => e.fbxPath == fbxPath))
                    {
                        watchlist.Add(new WatchEntry { fbxPath = fbxPath, targetFolder = targetFolderPath });
                        SaveWatchlist();
                        Repaint();
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Animations found: {foundClips.Count}");

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(targetFolderPath) || foundClips.Count == 0 || selectedPreset == null);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 14;
            btnStyle.fontStyle = FontStyle.Bold;

            if (GUILayout.Button("Export", btnStyle, GUILayout.Height(35)))
            {
                string fbxPath = AssetDatabase.GetAssetPath(selectedFbx);
                ExportAnimationsFromFbx(fbxPath, targetFolderPath);
                Close();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
        }

        private void OpenFolderDialog()
        {
            string absoluteDefault = "Assets";
            if (!string.IsNullOrEmpty(targetFolderPath))
            {
                absoluteDefault = Path.GetFullPath(targetFolderPath).Replace("\\", "/");
            }

            string selectedPath = EditorUtility.OpenFolderPanel("Select export folder", absoluteDefault, "");

            if (!string.IsNullOrEmpty(selectedPath))
            {
                string relativePath = GetRelativePath(selectedPath);

                if (relativePath != null)
                {
                    targetFolderPath = relativePath;
                    EditorPrefs.SetString(LastPathKey, targetFolderPath);
                    Repaint();
                }
            }
        }

        private string GetRelativePath(string absolutePath)
        {
            string dataPath = Application.dataPath;
            absolutePath = absolutePath.Replace("\\", "/");
            dataPath = dataPath.Replace("\\", "/");

            if (absolutePath.StartsWith(dataPath))
            {
                return "Assets" + absolutePath.Substring(dataPath.Length);
            }
            return null;
        }

        public static void ExportAnimationsFromFbx(string fbxPath, string targetFolder, bool fromPostprocessor = false)
        {
            if (string.IsNullOrEmpty(fbxPath) || string.IsNullOrEmpty(targetFolder)) return;

            string presetPath = EditorPrefs.GetString(PresetPathKey, "");
            AnimationPreset preset = null;
            if (!string.IsNullOrEmpty(presetPath))
            {
                preset = AssetDatabase.LoadAssetAtPath<AnimationPreset>(presetPath);
            }

            if (preset == null) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var clips = new List<AnimationClip>();

            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clips.Add(clip);
                }
            }

            if (clips.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var clip in clips)
                {
                    string safeName = SanitizeFileName(clip.name);
                    string targetPath = Path.Combine(targetFolder, safeName + ".anim").Replace("\\", "/");

                    AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);

                    if (existingClip != null)
                    {
                        EditorUtility.CopySerialized(clip, existingClip);
                        OptimizeClip(existingClip, preset.rootBones);
                        EditorUtility.SetDirty(existingClip);
                    }
                    else
                    {
                        AnimationClip newClip = Object.Instantiate(clip);
                        OptimizeClip(newClip, preset.rootBones);
                        AssetDatabase.CreateAsset(newClip, targetPath);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if (!fromPostprocessor)
                {
                    AssetDatabase.Refresh();
                }
            }
        }

        private static void OptimizeClip(AnimationClip clip, string[] rootBoneNames)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (var binding in bindings)
            {
                string prop = binding.propertyName.ToLowerInvariant();

                bool isPosition = prop.Contains("localposition") || prop.Contains("m_localposition") || prop.Contains("position");
                bool isScale = prop.Contains("localscale") || prop.Contains("m_localscale") || prop.Contains("scale");

                bool shouldRemove = false;

                if (isScale)
                {
                    shouldRemove = true;
                }
                else if (isPosition)
                {
                    bool isRootBone = false;
                    foreach (string rootBone in rootBoneNames)
                    {
                        if (binding.path == rootBone || binding.path.EndsWith("/" + rootBone))
                        {
                            isRootBone = true;
                            break;
                        }
                    }

                    if (!isRootBone)
                    {
                        shouldRemove = true;
                    }
                }

                if (shouldRemove)
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }

    public class FbxAnimationPostprocessor : AssetPostprocessor
    {
        private static List<string> pendingExports = new List<string>();

        void OnPostprocessModel(GameObject g)
        {
            if (!EditorPrefs.GetBool("FbxAnimExtractor_AnimationMode", false)) return;

            string watchlistStr = EditorPrefs.GetString("FbxAnimExtractor_Watchlist", "");
            if (string.IsNullOrEmpty(watchlistStr)) return;

            string fbxPath = assetPath;

            var entries = watchlistStr.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|');
                if (parts.Length == 2 && parts[0] == fbxPath)
                {
                    string targetFolder = parts[1];
                    if (!pendingExports.Contains(fbxPath))
                    {
                        pendingExports.Add(fbxPath);
                        EditorApplication.delayCall += () =>
                        {
                            FbxAnimationExtractor.ExportAnimationsFromFbx(fbxPath, targetFolder, true);
                            pendingExports.Remove(fbxPath);
                            AssetDatabase.Refresh();
                        };
                    }
                    break;
                }
            }
        }
    }

    public static class QuickPathCopier
    {
        [MenuItem("Tools/OzhogiTools/Copy Full Path %SPACE")]
        static void CopyFullPath()
        {
            if (Selection.activeObject != null)
            {
                string relativePath = AssetDatabase.GetAssetPath(Selection.activeObject);
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                GUIUtility.systemCopyBuffer = fullPath;
            }
        }
    }
}