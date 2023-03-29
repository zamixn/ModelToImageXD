using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static ModelScreenShotter;

[CreateAssetMenu(fileName = "ModelToImageSettings", menuName = "ModelToImageXD/ModelToImageSettings")]
public class ModelToImageSettings : ScriptableObject
{
    [HideInInspector] public bool LoadedModelsShown;
    public SceneAsset RenderScene;
    public string ScreenshotRootPath;
    public string ImageNamePrefix;
    public string ImageNameSuffix;
    public RenderTextureSettings RenderTextureSettings;
    [Range(0f, 1f)] public float ModelSizeInImage;
    public bool RenderBackground;


    private void InitDefaults()
    {
        RenderTextureSettings.ImageSize = new Vector2Int(256, 256);
        RenderTextureSettings.ImageAntialiasing = AntialisingMode.Sample4;
        RenderTextureSettings.ImageColorFormat = GraphicsFormat.R8G8B8A8_UNorm;
        RenderTextureSettings.ImageDepthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
        RenderTextureSettings.ImageWrapMode = TextureWrapMode.Clamp;
        RenderTextureSettings.ImageFilterMode = FilterMode.Bilinear;
        ModelSizeInImage = 0.75f;
    }


    public static ModelToImageSettings GetInstance()
    {
        var guids = AssetDatabase.FindAssets("t:ModelToImageSettings");
        if (guids.Length == 0)
        {
            var settings = (ModelToImageSettings)ScriptableObject.CreateInstance(typeof(ModelToImageSettings));
            settings.InitDefaults();
            var settingsPath = "Assets/Settings/Editor/ModelToImage";
            var setttingsName = "ModelToImageSettings.asset";
            TryCreateSettingsPath(settingsPath);
            AssetDatabase.CreateAsset(settings, $"{settingsPath}/{setttingsName}");
            return GetInstance();
        }
        else if (guids.Length > 1)
        {
            Debug.LogWarning("More than one instance of ModelToImageSettings");
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var instance = AssetDatabase.LoadAssetAtPath<ModelToImageSettings>(path);
        return instance;
    }

    private static void TryCreateSettingsPath(string path)
    {
        var pathParts = path.Split("/");
        var parentFolder = pathParts[0];

        for (int i = 1; i < pathParts.Length; i++)
        {
            var temp = $"{parentFolder}/{pathParts[i]}";
            if (!AssetDatabase.IsValidFolder(temp))
                AssetDatabase.CreateFolder(parentFolder, pathParts[i]);
            parentFolder = temp;
        }
    }
}
