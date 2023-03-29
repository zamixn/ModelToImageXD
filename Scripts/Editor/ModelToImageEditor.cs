using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static ModelScreenShotter;

public class ModelToImageEditor : EditorWindow
{
    [MenuItem("Tools/ModelToImage")]
    private static void ShowWindow()
    {
        ModelToImageEditor window = (ModelToImageEditor)EditorWindow.GetWindow(typeof(ModelToImageEditor));
        window.minSize = new Vector2(400, 400);
        window.Show();
    }
    private const int ModelsPerPage = 10;
    public bool RenderSceneLoaded;
    private ModelToImageSettings Settings;

    private class ModelInfo
    {
        public ModelImporter Importer;
        public string Path;
        public GameObject Model;
        public Vector3 EulerRotation;
    }

    private List<ModelInfo> Models;
    private List<ModelInfo> ModelsToUnload;
    private int CurrentPage;

    private void Init()
    {
        Models = new List<ModelInfo>();
        ModelsToUnload = new List<ModelInfo>();
        Settings = ModelToImageSettings.GetInstance();

    }

    private void OnGUI()
    {
        if (Models == null)
            Init();

        GUIStyle headStyle = new GUIStyle(EditorStyles.boldLabel);
        headStyle.fontSize = 30;
        GUILayout.Label("Model To Image", headStyle);
        GUILayout.Space(20);


        GUIStyle subHeadStyle = new GUIStyle(EditorStyles.boldLabel);
        subHeadStyle.fontSize = 20;
        GUILayout.Label("Settings", subHeadStyle);
        Settings.RenderScene = (SceneAsset)EditorGUILayout.ObjectField("Render Scene", Settings.RenderScene, typeof(SceneAsset), false);

        GUILayout.BeginHorizontal();
        Settings.ScreenshotRootPath = EditorGUILayout.TextField("Screenshot root path:", Settings.ScreenshotRootPath);
        if(GUILayout.Button("...", GUILayout.MaxWidth(40)))
            Settings.ScreenshotRootPath = EditorUtility.OpenFolderPanel("Select screenshot path", Settings.ScreenshotRootPath, "");
        GUILayout.EndHorizontal();

        EditorGUILayout.LabelField("   Generated image name settings");
        GUILayout.BeginHorizontal();
        Settings.ImageNamePrefix = EditorGUILayout.TextField(Settings.ImageNamePrefix);
        EditorGUILayout.LabelField("{model name}", GUILayout.MaxWidth(80));
        Settings.ImageNameSuffix = EditorGUILayout.TextField(Settings.ImageNameSuffix);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        Settings.ModelSizeInImage = EditorGUILayout.Slider("Model size in image", Settings.ModelSizeInImage, 0f, 1f);
        Settings.RenderBackground = EditorGUILayout.Toggle("Render Background", Settings.RenderBackground);
        GUILayout.Space(10);

        GUILayout.Label("   Image Settings");
        Settings.RenderTextureSettings.ImageSize = EditorGUILayout.Vector2IntField("Size", Settings.RenderTextureSettings.ImageSize);
        Settings.RenderTextureSettings.ImageAntialiasing = (AntialisingMode)EditorGUILayout.EnumPopup("Antialiasing", Settings.RenderTextureSettings.ImageAntialiasing);
        Settings.RenderTextureSettings.ImageColorFormat = (GraphicsFormat)EditorGUILayout.EnumPopup("Color format", Settings.RenderTextureSettings.ImageColorFormat);
        Settings.RenderTextureSettings.ImageDepthStencilFormat = (GraphicsFormat)EditorGUILayout.EnumPopup("Depth stencil format", Settings.RenderTextureSettings.ImageDepthStencilFormat);
        Settings.RenderTextureSettings.ImageWrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap mode", Settings.RenderTextureSettings.ImageWrapMode);
        Settings.RenderTextureSettings.ImageFilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter mode", Settings.RenderTextureSettings.ImageFilterMode);


        GUILayout.Space(20);


        GUI.enabled = Settings.RenderScene != null;
        GUILayout.BeginHorizontal();
        GUILayout.Label("");
        GUILayout.Label("");
        if (GUILayout.Button("Load Render Scene"))
            LoadRenderScene();
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        GUILayout.Space(20);


        if (GUILayout.Button("Load Models"))
            LoadModels();

        Settings.LoadedModelsShown = EditorGUILayout.Foldout(Settings.LoadedModelsShown, $"Loaded models ({Models.Count}):");
        if (Settings.LoadedModelsShown)
        {
            for (int i = CurrentPage * ModelsPerPage; i < (CurrentPage + 1) * ModelsPerPage && i < Models.Count; i++)
            {
                var modelInfo = Models[i];
                GUILayout.BeginHorizontal();
                GUIStyle indexStyle = new GUIStyle(GUI.skin.label);
                indexStyle.alignment = TextAnchor.MiddleRight;
                GUILayout.Label($"{i}", indexStyle, GUILayout.MaxWidth(50));

                GUI.enabled = false;
                EditorGUILayout.ObjectField(modelInfo.Model, typeof(GameObject), false);
                GUI.enabled = true;

                modelInfo.EulerRotation = EditorGUILayout.Vector3Field("Rot", modelInfo.EulerRotation, GUILayout.MaxWidth(120));

                if (GUILayout.Button("Remove", GUILayout.MaxWidth(60)))
                    ModelsToUnload.Add(modelInfo);

                GUILayout.EndHorizontal();
            }
        }

        foreach (var model in ModelsToUnload)
        {
            Models.Remove(model);
        }
        ModelsToUnload.Clear();
        GUILayout.Space(20);

        GUILayout.Label("Take Screenshots", subHeadStyle);
        if (GUILayout.Button("Take Screenshots"))
        {
            TakeModelImages();
        }

    }

    private void TakeModelImages()
    {
        if (Settings.ScreenshotRootPath == "" || !Directory.Exists(Settings.ScreenshotRootPath))
        {
            Debug.LogError("ScreenshotRootPath is invalid");
            return;
        }
        if (Settings.RenderScene == null)
        {
            Debug.LogError("RenderScene is not set");
            return;
        }

        if (!RenderSceneLoaded)
            LoadRenderScene();

        var screenshotCamera = FindObjectOfType<ModelScreenShotter>();
        if (screenshotCamera == null)
        {
            Debug.LogError("Could not find ModelScreenShotter in render scene");
            return;
        }
        screenshotCamera.ApplySettings(Settings.RenderTextureSettings, Settings.ModelSizeInImage, Settings.RenderBackground);
        foreach (var model in Models)
        {
            var screenshotPath = $"{Settings.ScreenshotRootPath}/{Settings.ImageNamePrefix}{model.Model.name}{Settings.ImageNameSuffix}.png";
            screenshotCamera.TakeScreenShot(model.Model, screenshotPath, model.EulerRotation);
        }
        screenshotCamera.Cleanup();
    }

    private void LoadModels()
    {
        Models.Clear();
        ModelsToUnload.Clear();
        var guids = AssetDatabase.FindAssets("t:model");
        foreach (var guid in guids)
        {
            if (guid == "")
                continue;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == "")
                continue;
            if (!path.StartsWith("Assets"))
                continue;
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                continue;
            var modelImporter = (ModelImporter)importer;
            if (modelImporter == null)
                continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                continue;
            Models.Add(new ModelInfo() { Path = path, Importer = modelImporter, Model = model, EulerRotation = Vector3.zero });
        }
        CurrentPage = 0;
    }

    private void LoadRenderScene()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        var path = AssetDatabase.GetAssetPath(Settings.RenderScene);
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        RenderSceneLoaded = true;
    }
}
