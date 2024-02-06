using UnityEditor;
using UnityEngine;

public class ModelScreenShotterEditorFunctionality : IModelScreenShotterFunctionality
{
    public GameObject InstantiatePrefab(GameObject prefabModel, Transform modelRoot)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(prefabModel, modelRoot);
    }
}
