﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Cinemachine.Utility;
using System.Reflection;

namespace Cinemachine.Editor
{
    /// <summary>
    /// Base class for virtual camera editors.
    /// Handles drawing the header and the basic properties.
    /// </summary>
    public class CinemachineVirtualCameraBaseEditor : UnityEditor.Editor
    {
        private CinemachineVirtualCameraBase Target { get { return target as CinemachineVirtualCameraBase; } }

        static Type[] sExtensionTypes;  // First entry is null
        static string[] sExtensionNames;

        protected virtual List<string> GetExcludedPropertiesInInspector()
        {
            return Target.m_ExcludedPropertiesInInspector == null
                ? new List<string>() : new List<string>(Target.m_ExcludedPropertiesInInspector);
        }

        protected virtual void OnEnable()
        {
            if (sExtensionTypes == null)
            {
                // Populate the extension list
                List<Type> exts = new List<Type>();
                List<string> names = new List<string>();
                exts.Add(null);
                names.Add("(select)");
                var allExtensions
                    = ReflectionHelpers.GetTypesInAllLoadedAssemblies(
                            (Type t) => t.IsSubclassOf(typeof(CinemachineExtension)));
                foreach (Type t in allExtensions)
                {
                    exts.Add(t);
                    names.Add(t.Name);
                }
                sExtensionTypes = exts.ToArray();
                sExtensionNames = names.ToArray();
            }
        }

        protected virtual void OnDisable()
        {
            if (CinemachineBrain.SoloCamera == (ICinemachineCamera)Target)
            {
                CinemachineBrain.SoloCamera = null;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        public override void OnInspectorGUI()
        {
            if (!Target.m_HideHeaderInInspector)
            {
                // Is the camera navel-gazing?
                CameraState state = Target.State;
                if (state.HasLookAt && (state.ReferenceLookAt - state.CorrectedPosition).AlmostZero())
                    EditorGUILayout.HelpBox(
                        "The camera is positioned on the same point at which it is trying to look.", 
                        MessageType.Warning);

                bool hasInconsistentAnimation, hadInconsistentAnimation;
                CinemachineCore.UpdateFilter updateMode;
                CinemachineCore.Instance.GetVcamUpdateStatus(
                    Target, out updateMode, out hasInconsistentAnimation, out hadInconsistentAnimation);
#if false // Not sure these warning are still valid or helpful
                if (hasInconsistentAnimation)
                    EditorGUILayout.HelpBox(
                        "This camera is currently looking at or following a target that is being moved in both FixedUpdate and Update.  This is a potential source of camera jitter.  One possible cause of this is the use of Interpolation in the RigidBody of the target.  Try turning that off.", 
                        MessageType.Error);
                else if (hadInconsistentAnimation)
                    EditorGUILayout.HelpBox(
                        "This camera was at some time looking at or following a target that was being moved in both FixedUpdate and Update.  This is a potential source of camera jitter.  One possible cause of this is the use of Interpolation in the RigidBody of the target.  Try turning that off.", 
                        MessageType.Warning);
#endif
                // Active status and Solo button
                Rect rect = EditorGUILayout.GetControlRect(true);
                Rect rectLabel = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
                rect.width -= rectLabel.width;
                rect.x += rectLabel.width;

                Color color = GUI.color;
                bool isSolo = (CinemachineBrain.SoloCamera == (ICinemachineCamera)Target);
                if (isSolo)
                    GUI.color = CinemachineBrain.GetSoloGUIColor();

                bool isLive = CinemachineCore.Instance.IsLive(Target);
                GUI.enabled = isLive;
                GUI.Label(rectLabel, isLive ? "Status: Live"
                    : (Target.isActiveAndEnabled ? "Status: Standby" : "Status: Disabled"));
                GUI.enabled = true;

                GUIContent updateText = new GUIContent(
                    updateMode == CinemachineCore.UpdateFilter.Fixed ? " Fixed Update" : " Late Update");
                var textDimensions = GUI.skin.label.CalcSize(updateText);
                float labelWidth = textDimensions.x;
                if (Application.isPlaying)
                    rect.width -= labelWidth;
                if (GUI.Button(rect, "Solo", "Button"))
                {
                    isSolo = !isSolo;
                    CinemachineBrain.SoloCamera = isSolo ? Target : null;
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
                GUI.color = color;
                if (isSolo)
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                if (Application.isPlaying)
                {
                    GUI.enabled = false;
                    rect.x += rect.width; rect.width = labelWidth;
                    GUI.Label(rect, updateText);
                    GUI.enabled = true;
                }                
                CinemachineSettings.CinemachineCoreSettings.ShowInGameGuides 
                    = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Game Window Guides", 
                            "Enable the display of overlays in the Game window.  You can adjust colours and opacity in Edit/Preferences/Cinemachine."), 
                        CinemachineSettings.CinemachineCoreSettings.ShowInGameGuides);

                SaveDuringPlay.SaveDuringPlay.Enabled 
                    = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Save During Play", 
                            "If checked, Virtual Camera settings changes made during Play Mode will be propagated back to the scene when Play Mode is exited."), 
                        SaveDuringPlay.SaveDuringPlay.Enabled);
                if (Application.isPlaying && SaveDuringPlay.SaveDuringPlay.Enabled)
                    EditorGUILayout.HelpBox(
                        " Virtual Camera settings changes made during Play Mode will be propagated back to the scene when Play Mode is exited.", 
                        MessageType.Info);
            }

            List<string> excluded = GetExcludedPropertiesInInspector();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, excluded.ToArray());
            serializedObject.ApplyModifiedProperties();
        }

        internal void DrawExtensionsWidgetInInspector()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Extensions", EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Add Extension"));

            int selection = EditorGUI.Popup(rect, 0, sExtensionNames);
            if (selection > 0)
            {
                Type extType = sExtensionTypes[selection];
                if (Target.GetComponent(extType) == null)
                    Undo.AddComponent(Target.gameObject, extType);
            }
        }

        [DrawGizmo(GizmoType.Active | GizmoType.InSelectionHierarchy | GizmoType.Pickable, typeof(CinemachineVirtualCameraBase))]
        internal static void DrawVirtualCameraBaseGizmos(CinemachineVirtualCameraBase vcam, GizmoType selectionType)
        {
            // Don't draw gizmos on hidden stuff
            if ((vcam.VirtualCameraGameObject.hideFlags & (HideFlags.HideInHierarchy | HideFlags.HideInInspector)) != 0)
                return;

            if (vcam.ParentCamera != null && (selectionType & GizmoType.Active) == 0)
                return;

            CameraState state = vcam.State;
            Gizmos.DrawIcon(state.FinalPosition, "Cinemachine/cm_logo_lg.png", true);

            CinemachineBrainEditor.DrawCameraFrustumGizmo(
                CinemachineCore.Instance.FindPotentialTargetBrain(vcam),
                state.Lens,
                Matrix4x4.TRS(
                    state.FinalPosition,
                    UnityQuaternionExtensions.Normalized(state.FinalOrientation), Vector3.one),
                CinemachineCore.Instance.IsLive(vcam)
                    ? CinemachineSettings.CinemachineCoreSettings.ActiveGizmoColour
                    : CinemachineSettings.CinemachineCoreSettings.InactiveGizmoColour);
        }
    }


    [CustomEditor(typeof(CinemachineVirtualCamera))]
    public class CinemachineVirtualCameraEditor : CinemachineVirtualCameraBaseEditor
    {
        private CinemachineVirtualCamera Target { get { return target as CinemachineVirtualCamera; } }

        // Static state and caches - Call UpdateStaticData() to refresh this
        struct StageData
        {
            string ExpandedKey { get { return "CNMCN_Core_Vcam_Expanded_" + Name; } }
            public bool IsExpanded 
            {
                get { return EditorPrefs.GetBool(ExpandedKey, false); }
                set { EditorPrefs.SetBool(ExpandedKey, value); }
            }
            public string Name;
            public Type[] types;   // first entry is null
            public GUIContent[] PopupOptions;
        }
        static StageData[] sStageData = null;

        // Instance data - call UpdateInstanceData() to refresh this
        int[] m_stageState = null;
        bool[] m_stageError = null;
        CinemachineComponentBase[] m_components;
        UnityEditor.Editor[] m_componentEditors;

        protected override void OnEnable()
        {
            // Build static menu arrays via reflection
            base.OnEnable();
            UpdateStaticData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Must destroy editors or we get exceptions
            if (m_componentEditors != null)
                foreach (UnityEditor.Editor e in m_componentEditors)
                    if (e != null)
                        UnityEngine.Object.DestroyImmediate(e);
        }

        private void OnSceneGUI()
        {
            if (Selection.Contains(Target.gameObject) && Tools.current == Tool.Move
                && Event.current.type == EventType.MouseDrag)
            {
                // User might be dragging our position handle
                Target.SuppressOrientationUpdate = true;
            }
            else if (GUIUtility.hotControl == 0 && Target.SuppressOrientationUpdate)
            {
                // We're not dragging anything now, but we were
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                Target.SuppressOrientationUpdate = false;
            }
        }

        public override void OnInspectorGUI()
        {
            // Ordinary properties
            base.OnInspectorGUI();

            // Pipeline - call this first
            UpdateInstanceData();

            foreach (CinemachineCore.Stage stage in Enum.GetValues(typeof(CinemachineCore.Stage)))
            {
                int index = (int)stage;

                // Skip pipeline stages that have no implementations
                if (sStageData[index].PopupOptions.Length <= 1)
                    continue;

                const float indentOffset = 6;

                GUIStyle stageBoxStyle = GUI.skin.box;
                EditorGUILayout.BeginVertical(stageBoxStyle);
                Rect rect = EditorGUILayout.GetControlRect(true);

                // Don't use PrefixLabel() because it will link the enabled status of field and label
                GUIContent label = new GUIContent(NicifyName(stage.ToString()));
                if (m_stageError[index])
                    label.image = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                float labelWidth = EditorGUIUtility.labelWidth - (indentOffset + EditorGUI.indentLevel * 15);
                Rect r = rect; r.width = labelWidth;
                EditorGUI.LabelField(r, label);
                r = rect; r.width -= labelWidth; r.x += labelWidth;
                GUI.enabled = !StageIsLocked(stage);
                int newSelection = EditorGUI.Popup(r, m_stageState[index], sStageData[index].PopupOptions);
                GUI.enabled = true;

                Type type = sStageData[index].types[newSelection];
                if (newSelection != m_stageState[index])
                {
                    SetPipelineStage(stage, type);
                    if (newSelection != 0)
                        sStageData[index].IsExpanded = true;
                    UpdateInstanceData(); // because we changed it
                    return;
                }
                if (type != null)
                {
                    Rect stageRect = new Rect(
                        rect.x - indentOffset, rect.y, rect.width + indentOffset, rect.height);
                    sStageData[index].IsExpanded = EditorGUI.Foldout(
                            stageRect, sStageData[index].IsExpanded, GUIContent.none);
                    if (sStageData[index].IsExpanded)
                    {
                        // Make the editor for that stage
                        UnityEditor.Editor e = GetEditorForPipelineStage(stage);
                        if (e != null)
                        {
                            ++EditorGUI.indentLevel;
                            EditorGUILayout.Separator();
                            e.OnInspectorGUI();
                            EditorGUILayout.Separator();
                            --EditorGUI.indentLevel;
                        }
                    }
                }
                EditorGUILayout.EndVertical();
            }

            // Extensions
            if (Array.IndexOf(Target.m_ExcludedPropertiesInInspector, "Extensions") < 0)
                DrawExtensionsWidgetInInspector();
        }

        bool StageIsLocked(CinemachineCore.Stage stage)
        {
            CinemachineCore.Stage[] locked = Target.m_LockStageInInspector;
            if (locked != null)
                for (int i = 0; i < locked.Length; ++i)
                    if (locked[i] == stage)
                        return true;
            return false;
        }

        UnityEditor.Editor GetEditorForPipelineStage(CinemachineCore.Stage stage)
        {
            foreach (UnityEditor.Editor e in m_componentEditors)
            {
                if (e != null)
                {
                    CinemachineComponentBase c = e.target as CinemachineComponentBase;
                    if (c != null && c.Stage == stage)
                        return e;
                }
            }
            return null;
        }

        /// <summary>
        /// Register with CinemachineVirtualCamera to create the pipeline in an undo-friendly manner
        /// </summary>
        [InitializeOnLoad]
        class CreatePipelineWithUndo
        {
            static CreatePipelineWithUndo()
            {
                CinemachineVirtualCamera.CreatePipelineOverride =
                    (CinemachineVirtualCamera vcam, string name, CinemachineComponentBase[] copyFrom) =>
                    {
                        // Create a new pipeline
                        GameObject go =  new GameObject(name);
                        Undo.RegisterCreatedObjectUndo(go, "created pipeline");
                        Undo.SetTransformParent(go.transform, vcam.transform, "parenting pipeline");
                        Undo.AddComponent<CinemachinePipeline>(go);

                        // If copying, transfer the components
                        if (copyFrom != null)
                        {
                            foreach (Component c in copyFrom)
                            {
                                Component copy = Undo.AddComponent(go, c.GetType());
                                Undo.RecordObject(copy, "copying pipeline");
                                ReflectionHelpers.CopyFields(c, copy);
                            }
                        }
                        return go.transform;
                    };
                CinemachineVirtualCamera.DestroyPipelineOverride = (GameObject pipeline) =>
                    {
                        Undo.DestroyObjectImmediate(pipeline);
                    };
            }
        }

        void SetPipelineStage(CinemachineCore.Stage stage, Type type)
        {
            Undo.SetCurrentGroupName("Cinemachine pipeline change");

            // Get the existing components
            Transform owner = Target.GetComponentOwner();

            CinemachineComponentBase[] components = owner.GetComponents<CinemachineComponentBase>();
            if (components == null)
                components = new CinemachineComponentBase[0];

            // Find an appropriate insertion point
            int numComponents = components.Length;
            int insertPoint = 0;
            for (insertPoint = 0; insertPoint < numComponents; ++insertPoint)
                if (components[insertPoint].Stage >= stage)
                    break;

            // Remove the existing components at that stage
            for (int i = numComponents - 1; i >= 0; --i)
            {
                if (components[i].Stage == stage)
                {
                    Undo.DestroyObjectImmediate(components[i]);
                    components[i] = null;
                    --numComponents;
                    if (i < insertPoint)
                        --insertPoint;
                }
            }

            // Add the new stage
            if (type != null)
            {
                MonoBehaviour b = Undo.AddComponent(owner.gameObject, type) as MonoBehaviour;
                while (numComponents-- > insertPoint)
                    UnityEditorInternal.ComponentUtility.MoveComponentDown(b);
            }
        }

        // This code dynamically discovers eligible classes and builds the menu
        // data for the various component pipeline stages.
        void UpdateStaticData()
        {
            if (sStageData != null)
                return;
            sStageData = new StageData[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];

            var stageTypes = new List<Type>[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];
            for (int i = 0; i < stageTypes.Length; ++i)
            {
                sStageData[i].Name = ((CinemachineCore.Stage)i).ToString();
                stageTypes[i] = new List<Type>();
            }

            // Get all ICinemachineComponents
            var allTypes
                = ReflectionHelpers.GetTypesInAllLoadedAssemblies(
                        (Type t) => t.IsSubclassOf(typeof(CinemachineComponentBase)));

            // Create a temp game object so we can instance behaviours
            GameObject go = new GameObject("Cinemachine Temp Object");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            foreach (Type t in allTypes)
            {
                MonoBehaviour b = go.AddComponent(t) as MonoBehaviour;
                CinemachineComponentBase c = b != null ? (CinemachineComponentBase)b : null;
                if (c != null)
                {
                    CinemachineCore.Stage stage = c.Stage;
                    stageTypes[(int)stage].Add(t);
                }
            }
            GameObject.DestroyImmediate(go);

            // Create the static lists
            for (int i = 0; i < stageTypes.Length; ++i)
            {
                stageTypes[i].Insert(0, null);  // first item is "none"
                sStageData[i].types = stageTypes[i].ToArray();
                GUIContent[] names = new GUIContent[sStageData[i].types.Length];
                for (int n = 0; n < names.Length; ++n)
                {
                    if (n == 0)
                    {
                        bool useSimple
                            = (i == (int)CinemachineCore.Stage.Aim)
                                || (i == (int)CinemachineCore.Stage.Body);
                        names[n] = new GUIContent((useSimple) ? "Hard constraint" : "none");
                    }
                    else
                        names[n] = new GUIContent(NicifyName(sStageData[i].types[n].Name));
                }
                sStageData[i].PopupOptions = names;
            }
        }

        string NicifyName(string name)
        {
            if (name.StartsWith("Cinemachine"))
                name = name.Substring(11); // Trim the prefix
            return ObjectNames.NicifyVariableName(name);
        }

        void UpdateInstanceData()
        {
            // Invalidate the target's cache - this is to support Undo
            Target.InvalidateComponentPipeline();
            UpdateComponentEditors();
            UpdateStageState(m_components);
        }

        // This code dynamically builds editors for the pipeline components.
        // Expansion state is cached statically to preserve foldout state.
        void UpdateComponentEditors()
        {
            CinemachineComponentBase[] components = Target.GetComponentPipeline();
            int numComponents = components != null ? components.Length : 0;
            if (m_components == null || m_components.Length != numComponents)
                m_components = new CinemachineComponentBase[numComponents];
            bool dirty = (numComponents == 0);
            for (int i = 0; i < numComponents; ++i)
            {
                if (components[i] != m_components[i])
                {
                    dirty = true;
                    m_components[i] = components[i];
                }
            }
            if (dirty)
            {
                // Destroy the subeditors
                if (m_componentEditors != null)
                    foreach (UnityEditor.Editor e in m_componentEditors)
                        if (e != null)
                            UnityEngine.Object.DestroyImmediate(e);

                // Create new editors
                m_componentEditors = new UnityEditor.Editor[numComponents];
                for (int i = 0; i < numComponents; ++i)
                {
                    MonoBehaviour b = components[i] as MonoBehaviour;
                    if (b != null)
                        CreateCachedEditor(b, null, ref m_componentEditors[i]);
                }
            }
        }

        void UpdateStageState(CinemachineComponentBase[] components)
        {
            m_stageState = new int[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];
            m_stageError = new bool[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];
            foreach (var c in components)
            {
                CinemachineCore.Stage stage = c.Stage;
                int index = 0;
                for (index = sStageData[(int)stage].types.Length - 1; index > 0; --index)
                    if (sStageData[(int)stage].types[index] == c.GetType())
                        break;
                m_stageState[(int)stage] = index;
                m_stageError[(int)stage] = !c.IsValid;
            }
        }

        // Because the cinemachine components are attached to hidden objects, their
        // gizmos don't get drawn by default.  We have to do it explicitly.
        [InitializeOnLoad]
        static class CollectGizmoDrawers
        {
            static CollectGizmoDrawers()
            {
                m_GizmoDrawers = new Dictionary<Type, MethodInfo>();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        bool added = false;
                        foreach (var method in type.GetMethods(
                                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            if (added)
                                break;
                            if (!method.IsStatic)
                                continue;
                            var attributes = method.GetCustomAttributes(typeof(DrawGizmo), true) as DrawGizmo[];
                            foreach (var a in attributes)
                            {
                                if (typeof(CinemachineComponentBase).IsAssignableFrom(a.drawnType))
                                {
                                    m_GizmoDrawers.Add(a.drawnType, method);
                                    added = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            public static Dictionary<Type, MethodInfo> m_GizmoDrawers;
        }

        [DrawGizmo(GizmoType.Active | GizmoType.InSelectionHierarchy, typeof(CinemachineVirtualCamera))]
        internal static void DrawVirtualCameraGizmos(CinemachineVirtualCamera vcam, GizmoType selectionType)
        {
            var pipeline = vcam.GetComponentPipeline();
            foreach (var c in pipeline)
            {
                MethodInfo method;
                if (CollectGizmoDrawers.m_GizmoDrawers.TryGetValue(c.GetType(), out method))
                    method.Invoke(null, new object[] { c, selectionType });
            }
        }
    }
}
