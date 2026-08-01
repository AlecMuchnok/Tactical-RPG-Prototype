#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only convenience to wire the currently open scene for the
/// isometric grid slice, so nothing has to be hand-assembled in the Inspector.
/// Safe to re-run: each piece (camera, GameLoop, its components) is checked
/// and added independently, so it can repair a scene that's only partially set up.
/// </summary>
public static class SceneSetup
{
    [MenuItem("Tools/Tactical RPG/Setup Scene")]
    public static void SetupScene()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        Undo.RecordObject(cam, "Set Camera Orthographic");
        cam.orthographic = true;

        GridManager manager = Object.FindFirstObjectByType<GridManager>();
        GameObject go;
        if (manager == null)
        {
            go = new GameObject("GameLoop");
            Undo.RegisterCreatedObjectUndo(go, "Create GameLoop");
            manager = Undo.AddComponent<GridManager>(go);
        }
        else
        {
            go = manager.gameObject;
        }

        if (go.GetComponent<GridInputController>() == null)
        {
            Undo.AddComponent<GridInputController>(go);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("SceneSetup: scene wired. Press Play to test, or save the scene to persist GameLoop.");
    }
}
#endif
