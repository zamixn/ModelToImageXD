using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
public class ModelScreenShotter : MonoBehaviour
{
    public enum AntialisingMode
    { 
        None = 0,
        Smaple2 = 1,
        Sample4 = 2,
        Sample8 = 3
    }

    private Color BGColor = new Color(0, 0, 0, 0);

    [System.Serializable]
    public struct RenderTextureSettings
    {
        public Vector2Int ImageSize;
        public AntialisingMode ImageAntialiasing;
        public UnityEngine.Experimental.Rendering.GraphicsFormat ImageColorFormat;
        public UnityEngine.Experimental.Rendering.GraphicsFormat ImageDepthStencilFormat;
        public TextureWrapMode ImageWrapMode;
        public FilterMode ImageFilterMode;
    }

    [SerializeField] private Transform ModelRoot;
    [SerializeField] private Camera Camera;
    [SerializeField] private GameObject model;

    private RenderTextureSettings CurrentRenderTextureSettings;
    private float TargetSizeInImage;

    private RenderTexture ScreenshotRenderTexture;

    public void ApplySettings(RenderTextureSettings renderTextureSettings, float targetSizeInImage, bool renderBackground)
    {
        ScreenshotRenderTexture = new RenderTexture(renderTextureSettings.ImageSize.x, renderTextureSettings.ImageSize.y, 
                                                    renderTextureSettings.ImageColorFormat, renderTextureSettings.ImageDepthStencilFormat);
        ScreenshotRenderTexture.antiAliasing = (int)renderTextureSettings.ImageAntialiasing;
        ScreenshotRenderTexture.wrapMode = renderTextureSettings.ImageWrapMode;
        ScreenshotRenderTexture.filterMode = renderTextureSettings.ImageFilterMode;
        TargetSizeInImage = targetSizeInImage;
        Camera.targetTexture = ScreenshotRenderTexture;
        Camera.backgroundColor = BGColor;
        Camera.clearFlags = CameraClearFlags.SolidColor;
    }

    public void TakeScreenShot(GameObject modelPrefab, string outputPath, Vector3 rotation)
    {
        if (ScreenshotRenderTexture == null)
        {
            Debug.LogError("Init not called");
            return;
        }

        Debug.Log($"taking screen shot of: {modelPrefab.name}");
        var spawnedModel = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, ModelRoot);
        spawnedModel.transform.position = Vector3.zero;
        RotateModel(spawnedModel, rotation);
        ScaleModel(spawnedModel);
        PositionModel(spawnedModel);
        Camera.Render();
        var prevActiveRenderTexture = RenderTexture.active;
        RenderTexture.active = ScreenshotRenderTexture;
        Texture2D tex = new Texture2D(ScreenshotRenderTexture.width, ScreenshotRenderTexture.height, TextureFormat.RGBA32, false);

        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        var texData = tex.EncodeToPNG();
        File.WriteAllBytes(outputPath, texData);

        RenderTexture.active = prevActiveRenderTexture;
        DestroyImmediate(spawnedModel);
    }

    private void PositionModel(GameObject model)
    {
        var modelBounds = GetModelBounds(model);
        var worldSpaceCorners = GetWorldFrustumCorners(modelBounds);
        var imageBounds = GetImageBonds(worldSpaceCorners);

        var modelOffset = imageBounds.center - modelBounds.center;
        model.transform.position += modelOffset;
    }

    private void ScaleModel(GameObject model)
    {
        var modelBounds = GetModelBounds(model);
        var worldSpaceCorners = GetWorldFrustumCorners(modelBounds);
        var imageBounds = GetImageBonds(worldSpaceCorners);

        var scaledImageBounds = GetScaledImageBounds(imageBounds);
        var modelSize = modelBounds.size;
        var scaledImageBoundsSize = scaledImageBounds.size;

        float scaleFactor;
        if (modelSize.y > modelSize.x)
            scaleFactor = scaledImageBoundsSize.y / modelSize.y;
        else
            scaleFactor = scaledImageBoundsSize.x / modelSize.x;

        model.transform.localScale *= scaleFactor;
    }

    private void RotateModel(GameObject model, Vector3 eulerRotation)
    {
        model.transform.eulerAngles = eulerRotation;
    }

    private Bounds GetScaledImageBounds(Bounds imageBounds)
    {
        Bounds b = new Bounds(imageBounds.center, new Vector3(0.1f, 0.1f, 0.1f));
        b.Encapsulate(imageBounds.center + Vector3.left * imageBounds.extents.x * TargetSizeInImage);
        b.Encapsulate(imageBounds.center + Vector3.right * imageBounds.extents.x * TargetSizeInImage);
        b.Encapsulate(imageBounds.center + Vector3.up * imageBounds.extents.y * TargetSizeInImage);
        b.Encapsulate(imageBounds.center + Vector3.down * imageBounds.extents.y * TargetSizeInImage);
        return b;
    }

    private Bounds GetModelBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private Vector3[] GetWorldFrustumCorners(Bounds modelBounds)
    {
        Vector3[] frustumCorners = new Vector3[4];
        Vector3[] worldSpaceCorners = new Vector3[4];
        var modelRelativePosition = modelBounds.center - Camera.transform.position;
        var z_depth = Vector3.Dot(Camera.transform.forward, modelRelativePosition);
        Camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), z_depth, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
        for (int i = 0; i < frustumCorners.Length; i++)
        {
            worldSpaceCorners[i] = Camera.transform.position + Camera.transform.TransformVector(frustumCorners[i]);
        }

        //var boundCenterObj = new GameObject("bound center");
        //boundCenterObj.transform.position = modelBounds.center;
        //for (int i = 0; i < worldSpaceCorners.Length; i++)
        //{
            //var obj = new GameObject($"corner_{i}");
            //obj.transform.position = worldSpaceCorners[i];
        //}
        Debug.DrawRay(Camera.transform.position, modelRelativePosition, Color.red, 5f);
        Debug.DrawRay(Camera.transform.position, Camera.transform.forward * z_depth, Color.blue, 5f);

        return worldSpaceCorners;
    }

    private Bounds GetImageBonds(Vector3[] worldSpaceCorners)
    {
        var bounds = new Bounds(worldSpaceCorners[0], new Vector3(0.1f, 0.1f, 0.1f));
        for (int i = 1; i < worldSpaceCorners.Length; i++)
            bounds.Encapsulate(worldSpaceCorners[i]);
        return bounds;
    }

    private float GetCurrentScreenAngleRatio(Bounds bounds)
    {
        float distance = Vector3.Distance(Camera.transform.position, bounds.center);

        var boundsMaxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float screenAngle = Mathf.Atan2(boundsMaxSize, distance);
        float screenAngleRatio = Mathf.Abs(screenAngle * Mathf.Rad2Deg) / Camera.fieldOfView;
        return screenAngleRatio;
    }

    public void Cleanup()
    {
        Camera.targetTexture = null;
        ScreenshotRenderTexture.DiscardContents();
        ScreenshotRenderTexture = null;
    }
}
#else
public class ModelScreenShotter : MonoBehaviour
{ }
#endif
