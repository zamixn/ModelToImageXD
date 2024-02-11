using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static ModelScreenShotter;
using static ModelToImageSettings;

public class ModelToImageEditor : EditorWindow
{
    [MenuItem("Tools/ModelToImage")]
    private static void ShowWindow()
    {
        ModelToImageEditor window = (ModelToImageEditor)EditorWindow.GetWindow(typeof(ModelToImageEditor));
        window.minSize = new Vector2(400, 400);
        window.Show();
    }
    private const int ModelsPerPage = 5;
    private int MaxPages => Models.Count / ModelsPerPage;
    public bool RenderSceneLoaded;
    private ModelToImageSettings Settings;

    private Vector2 ModelsScrollPosition;

    private class ModelInfo
    {
        public AssetImporter Importer;
        public string Path;
        public GameObject Model;
        public Vector3 EulerRotation;

        public void RefreshMetaData()
        {
            Path = AssetDatabase.GetAssetPath(Model);
            Importer = AssetImporter.GetAtPath(Path);
        }
    }

    private List<ModelInfo> Models;
    private List<ModelInfo> ModelsToUnload;
    private Dictionary<int, ModelInfo> ModelsToInsert;
    private int CurrentPage;

    private void Init()
    {
        Models = new List<ModelInfo>();
        ModelsToUnload = new List<ModelInfo>();
        ModelsToInsert = new Dictionary<int, ModelInfo>();
        Settings = ModelToImageSettings.GetInstance();
        LoadModelsFromSettings();
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


        if (GUILayout.Button("Load All Models In Project"))
            LoadModels();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Models In Folder"))
            LoadModelsInFolder();
        if (GUILayout.Button("Load Single Model"))
            LoadSingleModel();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Load Saved Models"))
            LoadModelsFromSettings();
        if (GUILayout.Button("Save Loaded Models"))
            TrySavingLoadedModels();


        GUILayout.Label("Take Screenshots", subHeadStyle);
        if (GUILayout.Button("Take Screenshots"))
        {
            TakeModelImages();
        }
        GUILayout.Space(20);

        Settings.LoadedModelsShown = EditorGUILayout.Foldout(Settings.LoadedModelsShown, $"Loaded models ({Models.Count}):");
        if (Settings.LoadedModelsShown)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Current page: ");
            CurrentPage = Mathf.Clamp(EditorGUILayout.IntField(CurrentPage, GUILayout.MaxWidth(40)), 0, MaxPages);
            EditorGUILayout.LabelField($"/ {MaxPages}", GUILayout.MaxWidth(30));
            if (GUILayout.Button("Prev", GUILayout.MaxWidth(40)) && CurrentPage > 0)
                CurrentPage--;
            if (GUILayout.Button("Next", GUILayout.MaxWidth(40)) && CurrentPage < MaxPages)
                CurrentPage++;

            GUILayout.EndHorizontal();

            ModelsScrollPosition = EditorGUILayout.BeginScrollView(ModelsScrollPosition);
            for (int i = CurrentPage * ModelsPerPage; i < (CurrentPage + 1) * ModelsPerPage && i < Models.Count; i++)
            {
                var modelInfo = Models[i];
                GUILayout.BeginHorizontal();
                GUIStyle indexStyle = new GUIStyle(GUI.skin.label);
                indexStyle.alignment = TextAnchor.MiddleRight;
                GUILayout.Label($"{i+1}", indexStyle, GUILayout.MaxWidth(50));

                modelInfo.Model = (GameObject)EditorGUILayout.ObjectField(modelInfo.Model, typeof(GameObject), false, GUILayout.MaxWidth(200));

                modelInfo.EulerRotation = EditorGUILayout.Vector3Field("Rot", modelInfo.EulerRotation, GUILayout.MaxWidth(150));

                if (GUILayout.Button("-", GUILayout.MaxWidth(20)))
                    ModelsToUnload.Add(modelInfo);
                if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
                    ModelsToInsert.Add(i+1, new ModelInfo());


                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add New"))
                ModelsToInsert.Add(Models.Count, new ModelInfo());

            if (GUILayout.Button("Clear"))
                ModelsToUnload.AddRange(Models);
        }

        foreach (var item in ModelsToInsert)
        {
            Models.Insert(item.Key, item.Value);
        }
        ModelsToInsert.Clear();
        foreach (var model in ModelsToUnload)
        {
            Models.Remove(model);
        }
        ModelsToUnload.Clear();
    }

    private void TrySavingLoadedModels()
    {
        if (EditorUtility.DisplayDialog("Save loaded models", "This will replace current saved models with the current loaded models. Continue?", "Yes", "Cancel"))
        {
            List<SavedModelData> modelData = new List<SavedModelData>();
            foreach (var model in Models)
            {
                if (model.Model != null && (model.Path == null || model.Path == ""))
                    model.RefreshMetaData();
                if (model.Path != null && model.Path != "")
                    modelData.Add(new SavedModelData() { Path = model.Path, EulerRotation = model.EulerRotation });
            }
            Settings.SaveLoadedModels(modelData);
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
            if (model.Model == null)
                continue;
            if (model.Path == null || model.Path == "" || model.Importer == null)
                model.RefreshMetaData();
            var screenshotPath = $"{Settings.ScreenshotRootPath}/{Settings.ImageNamePrefix}{model.Model.name}{Settings.ImageNameSuffix}.png";
            screenshotCamera.TakeScreenShot(model.Model, screenshotPath, model.EulerRotation);
        }
        screenshotCamera.Cleanup();
    }

    private void LoadSingleModel()
    {
        var path = EditorUtility.OpenFilePanelWithFilters("Select model", "", new string[] { "obj", "fbx" });
        if (path == null || path == "")
            return;

        var indexOfAssets = path.IndexOf("Assets");
        if (indexOfAssets == -1)
            return;
        path = path.Substring(indexOfAssets);

        var importer = AssetImporter.GetAtPath(path);
        if (importer == null)
            return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
            return;
        Models.Add(new ModelInfo() { Path = path, Importer = importer, Model = model, EulerRotation = Vector3.zero });
    }

    private void LoadModelsInFolder()
    {
        var folderPath = EditorUtility.OpenFolderPanel("Select model folder", Settings.ScreenshotRootPath, "");
        if (folderPath == null || folderPath == "")
            return;
        var indexOfAssets = folderPath.IndexOf("Assets");
        if (indexOfAssets == -1)
            return;
        folderPath = folderPath.Substring(indexOfAssets);
        var guids = AssetDatabase.FindAssets("t:model", new string[] { folderPath });
        LoadModelsByGUIDS(guids);
    }

    private void LoadModels()
    {
        Models.Clear();
        ModelsToUnload.Clear();
        ModelsToInsert.Clear();
        CurrentPage = 0;
        var guids = AssetDatabase.FindAssets("t:model");
        LoadModelsByGUIDS(guids);
    }

    private void LoadModelsByGUIDS(string[] guids)
    {
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
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                continue;
            Models.Add(new ModelInfo() { Path = path, Importer = importer, Model = model, EulerRotation = Vector3.zero });
        }
    }

    private void LoadModelsFromSettings()
    {
        Models.Clear();
        ModelsToUnload.Clear();
        ModelsToInsert.Clear();
        var savedModelData = Settings.SavedModels;
        if (savedModelData == null)
            return;
        for (int i = 0; i < savedModelData.Count; i++)
        {
            var modelData = savedModelData[i];
            var path = modelData.Path;
            var rot = modelData.EulerRotation;
            if (path == "")
                continue;
            if (!path.StartsWith("Assets"))
                continue;
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                continue;
            Models.Add(new ModelInfo() { Path = path, Importer = importer, Model = model, EulerRotation = rot });
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
