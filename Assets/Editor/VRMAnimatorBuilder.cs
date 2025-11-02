#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

/// <summary>
/// Editor utility to create a simple AnimatorController for VRM humanoid models.
/// It builds a1D BlendTree driven by a float parameter "Speed" with three motions: Idle(0), Walk(0.5), Run(1.0).
/// You can assign your own humanoid AnimationClips or let it generate empty placeholder clips.
/// </summary>
public class VRMAnimatorBuilder : EditorWindow
{
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip runClip;
    private string controllerPath = "Assets/Animations/VRM_Locomotion.controller";

    [MenuItem("Tools/VRM/Generate Animator Controller...")]
    public static void Open()
    {
        GetWindow<VRMAnimatorBuilder>(true, "VRM Animator Builder");
    }

    [MenuItem("Tools/VRM/Quick Create Default Animator")]
    public static void QuickCreate()
    {
        EnsureFolder("Assets/Animations");
        var ctrl = CreateControllerWithClips(
        EnsureClip("Assets/Animations/VRM_Idle.anim"),
        EnsureClip("Assets/Animations/VRM_Walk.anim"),
        EnsureClip("Assets/Animations/VRM_Run.anim"),
        "Assets/Animations/VRM_Locomotion.controller");
        Selection.activeObject = ctrl;
        EditorUtility.DisplayDialog("VRM Animator", "Created default VRM_Locomotion.controller under Assets/Animations.\nReplace placeholder clips with your humanoid Idle/Walk/Run.", "OK");
    }

    private void OnGUI()
    {
        GUILayout.Label("Create VRM AnimatorController", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Builds a locomotion controller for Humanoid/VRM. It uses a float parameter 'Speed' to blend Idle/Walk/Run.", MessageType.Info);

        idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false);
        walkClip = (AnimationClip)EditorGUILayout.ObjectField("Walk Clip", walkClip, typeof(AnimationClip), false);
        runClip = (AnimationClip)EditorGUILayout.ObjectField("Run Clip", runClip, typeof(AnimationClip), false);
        controllerPath = EditorGUILayout.TextField("Save Path", controllerPath);

        GUILayout.Space(8);
        if (GUILayout.Button("Create Controller"))
        {
            EnsureFolder(Path.GetDirectoryName(controllerPath).Replace("\\", "/"));
            var ic = idleClip != null ? idleClip : EnsureClip("Assets/Animations/VRM_Idle.anim");
            var wc = walkClip != null ? walkClip : EnsureClip("Assets/Animations/VRM_Walk.anim");
            var rc = runClip != null ? runClip : EnsureClip("Assets/Animations/VRM_Run.anim");

            var ctrl = CreateControllerWithClips(ic, wc, rc, controllerPath);
            Selection.activeObject = ctrl;
        }

        GUILayout.Space(6);
        if (GUILayout.Button("Open Controller"))
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ctrl != null) Selection.activeObject = ctrl;
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static AnimationClip EnsureClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip();
        clip.name = Path.GetFileNameWithoutExtension(path);
        // Humanoid placeholder: leave empty curves. Works as idle.
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        return clip;
    }

    private static AnimatorController CreateControllerWithClips(AnimationClip idle, AnimationClip walk, AnimationClip run, string path)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var layer = controller.layers[0];
        var sm = layer.stateMachine;

        // Create a1D blend tree on Speed
        var blendTree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(blendTree, path);
        blendTree.AddChild(idle, 0.0f);
        blendTree.AddChild(walk, 0.5f);
        blendTree.AddChild(run, 1.0f);

        var state = sm.AddState("Locomotion");
        state.motion = blendTree;
        state.writeDefaultValues = true;

        // Make it default state
        sm.defaultState = state;
        controller.layers[0].stateMachine = sm;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return controller;
    }
}
#endif
