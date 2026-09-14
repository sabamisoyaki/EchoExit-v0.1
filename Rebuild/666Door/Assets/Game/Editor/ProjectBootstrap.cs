using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Door666.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Door666.Editor
{
    /// <summary>Reproducible asset generation; no scene or prefab YAML is authored by hand.</summary>
    public static class ProjectBootstrap
    {
        private const string RenderingFolder = "Assets/Resources/Rendering";
        private const string RendererPath = RenderingFolder + "/FieldRenderer.asset";
        private const string PipelinePath = RenderingFolder + "/FieldPipeline.asset";
        private const string FontPath = "Assets/Resources/Fonts/Japanese.asset";

        [MenuItem("666号扉/プロジェクトを初期化")]
        public static void Setup()
        {
            if (!File.Exists("Assets/Game/Runtime/SessionCoordinator.cs"))
                throw new InvalidOperationException("新規 666Door プロジェクトから実行してください。");

            EnsureFolder("Assets/Scenes");
            EnsureFolder(RenderingFolder);
            EnsureFolder("Assets/Resources/Fonts");
            EnsureFolder("Assets/Resources/Visuals");
            ConfigurePlayer();
            ConfigureRendering();
            ConfigureJapaneseFont();
            EnsureSettings();
            ExportBearVisual();
            CreateBootstrapScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("666Door bootstrap complete. Scene: " + GameConstants.ScenePath);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "666号扉";
            PlayerSettings.companyName = "Door666";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var handler = playerSettings.FindProperty("activeInputHandler");
            if (handler == null) handler = playerSettings.FindProperty("m_ActiveInputHandler");
            if (handler == null) throw new InvalidOperationException("Input System の ProjectSettings 項目が見つかりません。");
            handler.intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowDistance = 22;
        }

        private static void ConfigureRendering()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "666Door field renderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            renderer.renderingMode = RenderingMode.ForwardPlus;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "666Door lighting";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1;
            pipeline.shadowDistance = 22;
            pipeline.shadowCascadeCount = 1;
            pipeline.shadowDepthBias = .4f;
            pipeline.shadowNormalBias = .25f;
            pipeline.maxAdditionalLightsCount = 8;
            pipeline.additionalLightsShadowmapResolution = 2048;
            var serializedPipeline = new SerializedObject(pipeline);
            SetBoolean(serializedPipeline, "m_AdditionalLightShadowsSupported", true);
            SetBoolean(serializedPipeline, "m_SoftShadowsSupported", true);
            SetBoolean(serializedPipeline, "m_AnyShadowsSupported", true);
            SetInteger(serializedPipeline, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;

            int quality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(quality, true);
            // URP 17 keeps the settings type internal; invoke its own version-aware initialization.
            var globalType = typeof(UniversalRenderPipeline).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            var ensureGlobal = globalType == null ? null : globalType.GetMethod("Ensure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (ensureGlobal == null || ensureGlobal.Invoke(null, new object[] { true }) == null)
                throw new InvalidOperationException("URP Global Settings を作成できません。");

            // Resource materials keep shaders used by runtime factories present in standalone builds.
            EnsureMaterial("WorldLit", "Universal Render Pipeline/Lit", new Color(.65f, .60f, .36f), false);
            EnsureMaterial("Fluorescent", "Universal Render Pipeline/Lit", new Color(.90f, .95f, .76f), true);
            EnsureMaterial("AnomalyShadow", "Universal Render Pipeline/Unlit", Color.black, false);
        }

        private static void ConfigureJapaneseFont()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansJP.ttf");
            if (source == null) throw new InvalidOperationException("NotoSansJP.ttf が見つかりません。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("日本語フォントの生成に失敗しました。");
                font.name = "Japanese";
                AssetDatabase.CreateAsset(font, FontPath);
                font.material.name = "Japanese SDF";
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures)
                    if (atlas != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlas))) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            var serialized = new SerializedObject(font);
            SetBoolean(serialized, "m_ClearDynamicDataOnBuild", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            font.TryAddCharacters(CollectVisibleCharacters(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("日本語フォントに未収録の文字: " + missing);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlas))) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);

            var settings = TMP_Settings.instance;
            if (settings == null) throw new InvalidOperationException("TMP Essential Resources のインポートが完了していません。");
            TMP_Settings.defaultFontAsset = font;
            if (TMP_Settings.fallbackFontAssets == null) TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
            if (!TMP_Settings.fallbackFontAssets.Contains(font)) TMP_Settings.fallbackFontAssets.Add(font);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static string CollectVisibleCharacters()
        {
            var characters = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) characters.Add(c);
            foreach (var path in Directory.GetFiles("Assets/Game/Runtime", "*.cs", SearchOption.AllDirectories))
                foreach (var c in File.ReadAllText(path, Encoding.UTF8)) if (!char.IsControl(c)) characters.Add(c);
            foreach (var path in Directory.GetFiles("Assets/Resources", "*.json", SearchOption.AllDirectories))
                foreach (var c in File.ReadAllText(path, Encoding.UTF8)) if (!char.IsControl(c)) characters.Add(c);
            foreach (char c in "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン０１２３４５６７８９、。！？「」『』［］（）・ー…↑↓←→") characters.Add(c);
            var result = new StringBuilder(characters.Count);
            foreach (var c in characters) result.Append(c);
            return result.ToString();
        }

        private static void EnsureSettings()
        {
            string path = "Assets/Resources/" + GameConstants.SettingsResource + ".asset";
            if (AssetDatabase.LoadAssetAtPath<GameSettings>(path) != null) return;
            var settings = ScriptableObject.CreateInstance<GameSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        private static void ExportBearVisual()
        {
            const string prefabPath = "Assets/Resources/Visuals/bears.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/bears.fbx");
            if (model == null)
            {
                Debug.Log("Bear FBX is unavailable; the procedural bear will be used.");
                return;
            }
            var visual = Object.Instantiate(model);
            try
            {
                visual.name = "bears";
                visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Copy only geometry, transforms and the skin hierarchy, never legacy gameplay components.
                foreach (var component in visual.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || component is MeshFilter || component is Renderer) continue;
                    Object.DestroyImmediate(component);
                }
                var fur = EnsureMaterial("BearFur", "Universal Render Pipeline/Lit", new Color(.31f, .20f, .10f), false);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = fur;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                PrefabUtility.SaveAsPrefabAsset(visual, prefabPath);
            }
            finally { Object.DestroyImmediate(visual); }
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("666号扉").AddComponent<SessionCoordinator>();
            EditorSceneManager.SaveScene(scene, GameConstants.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameConstants.ScenePath, true) };
        }

        private static Material EnsureMaterial(string name, string shaderName, Color color, bool emission)
        {
            string path = RenderingFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("描画シェーダーが見つかりません: " + shaderName);
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .05f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 3.5f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetBoolean(SerializedObject target, string property, bool value)
        {
            var field = target.FindProperty(property);
            if (field != null) field.boolValue = value;
        }

        private static void SetInteger(SerializedObject target, string property, int value)
        {
            var field = target.FindProperty(property);
            if (field != null) field.intValue = value;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        [MenuItem("666号扉/Windows ビルド")]
        public static void BuildWindows()
        {
            Setup();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameConstants.ScenePath },
                locationPathName = "Builds/Windows/666Door.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log("666Door Windows build: " + report.summary.result + ", " + report.summary.totalSize + " bytes, " + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows ビルドに失敗しました: " + report.summary.totalErrors + " errors.");
        }
    }
}
