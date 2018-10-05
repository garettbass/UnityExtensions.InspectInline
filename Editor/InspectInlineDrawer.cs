using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    [CustomPropertyDrawer(typeof(InspectInlineAttribute))]
    public class InspectInlineDrawer : PropertyDrawer
    {
        private static readonly int s_controlIdHash =
            nameof(InspectInlineDrawer).GetHashCode();

        private class GUIResources
        {
            public readonly GUIStyle
            inDropDownStyle = new GUIStyle("IN DropDown");

            public readonly GUIContent
            selectContent = new GUIContent("Select..."),
            createSubassetContent = new GUIContent("CREATE SUBASSET"),
            deleteSubassetContent = new GUIContent("Delete Subasset");
        }

        private static GUIResources s_gui;
        private static GUIResources gui
        {
            get
            {
                if (s_gui == null)
                    s_gui = new GUIResources();
                return s_gui;
            }
        }

        //----------------------------------------------------------------------

        private static readonly Dictionary<Type, Type[]>
        s_concreteTypes = new Dictionary<Type, Type[]>();

        private static Type[] GetConcreteTypes(Type type)
        {
            var concreteTypes = default(Type[]);
            if (s_concreteTypes.TryGetValue(type, out concreteTypes))
                return concreteTypes;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = assemblies.SelectMany(a => a.GetTypes());
            concreteTypes =
                types
                .Where(t =>
                    t.IsAbstract == false &&
                    t.IsGenericTypeDefinition == false &&
                    type.IsAssignableFrom(t))
                .OrderBy(t => t.FullName.ToLower())
                .ToArray();

            s_concreteTypes.Add(type, concreteTypes);
            return concreteTypes;
        }

        //----------------------------------------------------------------------

        public new InspectInlineAttribute attribute
        {
            get { return (InspectInlineAttribute)base.attribute; }
        }

        //----------------------------------------------------------------------

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return false;
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
            {
                var serializedObject = property.serializedObject;
                var asset = serializedObject.targetObject;
                var target = property.objectReferenceValue;
                var targetExists = target != null;
                using (new ObjectScope(asset))
                {
                    if (targetExists && !ObjectScope.Contains(target))
                    {
                        var inlineEditor = GetInlineEditor(target);
                        height += inlineEditor.GetHeight();
                    }
                }
            }
            return height;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            var propertyRect = position;
            propertyRect.height = EditorGUIUtility.singleLineHeight;

            var target = property.objectReferenceValue;
            var targetExists = target != null;

            DoContextMenuGUI(propertyRect, property, targetExists);
            DoObjectFieldGUI(propertyRect, property, label);
            DoFoldoutGUI(propertyRect, property, targetExists);

            if (property.isExpanded)
            {
                var serializedObject = property.serializedObject;
                var asset = serializedObject.targetObject;
                using (new ObjectScope(asset))
                {
                    if (targetExists && !ObjectScope.Contains(target))
                    {
                        var inlineEditor = GetInlineEditor(target);
                        DoInlineEditorGUI(property, inlineEditor);
                    }
                }
            }

            DisposeObsoleteInlineEditorsOnNextEditorUpdate();
        }

        //----------------------------------------------------------------------

        private int GetControlID(Rect position)
        {
            var hint = s_controlIdHash;
            var focus = FocusType.Keyboard;
            return GUIUtility.GetControlID(hint, focus, position);
        }

        //----------------------------------------------------------------------

        private void DoContextMenuGUI(
            Rect position,
            SerializedProperty property,
            bool targetExists)
        {
            if (attribute.canCreateSubasset == false)
                return;

            var controlID = GetControlID(position);
            ObjectSelector.DoGUI(controlID, property);

            var buttonRect = position;
            buttonRect.xMin = buttonRect.xMax - 16;
            var buttonStyle = EditorStyles.label;

            var isRepaint = Event.current.type == EventType.Repaint;
            if (isRepaint)
            {
                var dropDownStyle = gui.inDropDownStyle;
                var rect = buttonRect;
                rect.x += 2;
                rect.y += 6;
                dropDownStyle.Draw(rect, false, false, false, false);
            }

            var noLabel = GUIContent.none;
            if (GUI.Button(buttonRect, noLabel, buttonStyle))
            {
                var types = GetConcreteTypes(fieldInfo.FieldType);
                ShowContextMenu(
                    buttonRect,
                    controlID,
                    property,
                    targetExists,
                    types);
            }
        }

        //----------------------------------------------------------------------

        private bool AllowSceneObjects(SerializedProperty property)
        {
            var asset = property.serializedObject.targetObject;
            return asset != null && !EditorUtility.IsPersistent(asset);
        }

        //----------------------------------------------------------------------

        private void DoObjectFieldGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            var objectType = fieldInfo.FieldType;
            var oldTarget = property.objectReferenceValue;
            var newTarget =
                EditorGUI.ObjectField(
                    position,
                    label,
                    oldTarget,
                    objectType,
                    AllowSceneObjects(property));

            EditorGUI.EndProperty();
            if (!ReferenceEquals(newTarget, oldTarget))
            {
                RemoveTarget(property);
                property.objectReferenceValue = newTarget;
                property.isExpanded = true;
            }
        }

        //----------------------------------------------------------------------

        private void DoFoldoutGUI(
            Rect position,
            SerializedProperty property,
            bool targetExists)
        {
            var foldoutRect = position;
            foldoutRect.width = EditorGUIUtility.labelWidth;

            var isExpanded = targetExists && property.isExpanded;

            var noLabel = GUIContent.none;
            isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, noLabel);

            if (targetExists)
            {
                property.isExpanded = isExpanded;
            }
        }

        //----------------------------------------------------------------------

        private void ShowContextMenu(
            Rect position,
            int controlID,
            SerializedProperty property,
            bool targetExists,
            Type[] types)
        {
            var menu = new GenericMenu();

            menu.AddItem(
                gui.selectContent,
                on: false,
                func: () => ShowObjectSelector(controlID, property));

            menu.AddSeparator("");

            if (targetExists && TargetIsSubassetOf(property))
                menu.AddItem(
                    gui.deleteSubassetContent,
                    on: false,
                    func: () => RemoveTarget(property));
            else
                menu.AddDisabledItem(gui.deleteSubassetContent);

            if (types.Length > 0)
            {
                menu.AddSeparator("");

                menu.AddDisabledItem(gui.createSubassetContent);

                var typeIndex = 0;
                var useTypeFullName = types.Length > 16;
                foreach (var type in types)
                {
                    var menuPath =
                        useTypeFullName
                        ? type.FullName.Replace('.', '/')
                        : type.Name;
                    var menuTypeIndex = typeIndex++;
                    menu.AddItem(
                        new GUIContent(menuPath),
                        on: false,
                        func: () =>
                            AddSubasset(property, types, menuTypeIndex));
                }
            }

            menu.DropDown(position);
        }

        //----------------------------------------------------------------------

        private void ShowObjectSelector(
            int controlID,
            SerializedProperty property)
        {
            var target = property.objectReferenceValue;
            var objectType = fieldInfo.FieldType;
            var allowSceneObjects = AllowSceneObjects(property);
            ObjectSelector.Show(
                controlID,
                target,
                objectType,
                property,
                allowSceneObjects);
        }

        //----------------------------------------------------------------------

        private void DoInlineEditorGUI(
            SerializedProperty property,
            InlineEditor inlineEditor)
        {
            var inlineEditorHeight = inlineEditor.GetHeight();
            GUILayoutUtility.GetRect(0, -inlineEditorHeight);
            var canEditTarget =
                attribute.canEditRemoteTarget ||
                TargetIsSubassetOf(property);
            EditorGUI.BeginDisabledGroup(!canEditTarget);
            inlineEditor.OnGUI();
            EditorGUI.EndDisabledGroup();
        }

        //----------------------------------------------------------------------

        private class NoScriptPropertyEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                var property = serializedObject.GetIterator();
                if (property.NextVisible(enterChildren: true))
                    while (property.NextVisible(enterChildren: false))
                        TryPropertyField(property);

                serializedObject.ApplyModifiedProperties();
            }

            private static void TryPropertyField(SerializedProperty property)
            {
                try
                {
                    EditorGUILayout
                    .PropertyField(property, includeChildren: true);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        //----------------------------------------------------------------------

        private class InlineEditor : IDisposable
        {

            private static readonly Type
            GenericInspectorType =
                typeof(Editor)
                .Assembly
                .GetType("UnityEditor.GenericInspector");

            private readonly GUIStyle
            inlineBackgroundStyle = new GUIStyle("TL SelectionButton");

            private readonly Editor m_editor;

            private float m_height;

            public InlineEditor(Editor editor)
            {
                var editorType = editor.GetType();
                if (editorType == GenericInspectorType)
                {
                    var target = editor.target;
                    TryDestroyImmediate(editor);
                    Editor.CreateCachedEditor(
                        target,
                        typeof(NoScriptPropertyEditor),
                        ref editor);
                }
                m_editor = editor;
            }

            public void Dispose()
            {
                TryDestroyImmediate(m_editor);
            }

            public float GetHeight()
            {
                return m_height;
            }

            public void OnGUI()
            {
                try
                {
                    EditorGUILayout.BeginVertical(inlineBackgroundStyle);
                    EditorGUI.indentLevel += 1;

                    var rectBefore = GUILayoutUtility.GetRect(0, 2);

                    m_editor.OnInspectorGUI();

                    var rectAfter = GUILayoutUtility.GetRect(0, 1);
                    GUILayoutUtility.GetRect(0, -1);

                    m_height = rectAfter.yMax - rectBefore.yMin;

                    // increase the contrast of the bottom edge background
                    var shadowRect = rectAfter;
                    shadowRect.xMin += 2;
                    shadowRect.xMax -= 2;
                    EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.025f));
                    shadowRect.y += 1;
                    EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.075f));
                    shadowRect.xMin += 1;
                    shadowRect.xMax -= 1;
                    shadowRect.y += 1;
                    EditorGUI.DrawRect(shadowRect, new Color(1, 1, 1, 0.025f));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    EditorGUI.indentLevel -= 1;
                    EditorGUILayout.EndVertical();
                }
            }

        }

        //----------------------------------------------------------------------

        private readonly Dictionary<Object, InlineEditor>
        m_inlineEditorMap = new Dictionary<Object, InlineEditor>();

        private InlineEditor GetInlineEditor(Object target)
        {
            Debug.Assert(target != null);
            var inlineEditor = default(InlineEditor);
            if (m_inlineEditorMap.TryGetValue(target, out inlineEditor))
                return inlineEditor;

            var editor = default(Editor);
            Editor.CreateCachedEditor(target, null, ref editor);
            Debug.Assert(!ReferenceEquals(editor, null));
            Debug.Assert(editor != null);
            inlineEditor = new InlineEditor(editor);
            m_inlineEditorMap.Add(target, inlineEditor);
            return inlineEditor;
        }

        private void DisposeObsoleteInlineEditors()
        {
            var map = m_inlineEditorMap;
            var destroyedObjects = map.Keys.Where(key => key == null);
            if (destroyedObjects.Any())
            {
                foreach (var @object in destroyedObjects.ToArray())
                {
                    map[@object].Dispose();
                    map.Remove(@object);
                }
            }
        }

        private void DisposeObsoleteInlineEditorsOnNextEditorUpdate()
        {
            EditorApplication.delayCall -= DisposeObsoleteInlineEditors;
            EditorApplication.delayCall += DisposeObsoleteInlineEditors;
        }

        //----------------------------------------------------------------------

        private static Object CreateInstance(Type type)
        {
            Debug.Assert(typeof(Object).IsAssignableFrom(type));
            return
                typeof(ScriptableObject).IsAssignableFrom(type)
                ? ScriptableObject.CreateInstance(type)
                : (Object)Activator.CreateInstance(type);
        }

        //----------------------------------------------------------------------

        private static bool TargetIsSubassetOf(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var asset = serializedObject.targetObject;
            var target = property.objectReferenceValue;
            return TargetIsSubassetOf(asset, target);
        }

        private static bool TargetIsSubassetOf(
            Object asset,
            Object target)
        {
            if (asset == null)
                return false;

            if (asset == target)
                return false;

            if (target == null)
                return false;

            var objectPath = AssetDatabase.GetAssetPath(asset);
            if (objectPath == null)
                return false;

            var targetPath = AssetDatabase.GetAssetPath(target);
            if (targetPath == null)
                return false;

            return objectPath == targetPath;
        }

        //----------------------------------------------------------------------

        private static bool CanAddSubasset(Object obj)
        {
            var hideFlags = obj.hideFlags;
            var dontSaveInBuild = HideFlags.DontSaveInBuild;
            if ((hideFlags & dontSaveInBuild) == dontSaveInBuild)
                return false;

            var dontSaveInEditor = HideFlags.DontSaveInEditor;
            if ((hideFlags & dontSaveInEditor) == dontSaveInEditor)
                return false;

            return true;
        }

        private void AddSubasset(
            SerializedProperty property,
            Type[] types,
            int typeIndex)
        {
            var type = types[typeIndex];

            RemoveTarget(property);

            var subasset = CreateInstance(type);
            if (subasset == null)
            {
                Debug.LogErrorFormat(
                    "Failed to create subasset of type {0}",
                    type.FullName);
                return;
            }

            if (!CanAddSubasset(subasset))
            {
                Debug.LogErrorFormat(
                    "Cannot save subasset of type {0}",
                    type.FullName);
                TryDestroyImmediate(subasset);
                return;
            }

            subasset.name = property.displayName;

            var serializedObject = property.serializedObject;
            var asset = serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            AssetDatabase.AddObjectToAsset(subasset, assetPath);

            property.objectReferenceInstanceIDValue = subasset.GetInstanceID();
            property.isExpanded = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        //----------------------------------------------------------------------

        private void RemoveTarget(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var asset = serializedObject.targetObject;
            var target = property.objectReferenceValue;
            if (target != null)
            {
                property.objectReferenceValue = null;
                if (TargetIsSubassetOf(asset, target))
                {
                    TryDestroyImmediate(target, allowDestroyingAssets: true);
                    // TODO: recursively destroy subassets
                }
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        //----------------------------------------------------------------------

        private static void TryDestroyImmediate(
            Object obj,
            bool allowDestroyingAssets = false)
        {
            try
            {
                if (obj != null)
                    Object.DestroyImmediate(obj, allowDestroyingAssets);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        //----------------------------------------------------------------------

        private struct ObjectScope : IDisposable
        {
            private static readonly HashSet<int> s_objectScopeSet =
                new HashSet<int>();

            private readonly int m_instanceID;

            public ObjectScope(Object obj)
            {
                m_instanceID = obj.GetInstanceID();
                s_objectScopeSet.Add(m_instanceID);
            }

            public void Dispose()
            {
                s_objectScopeSet.Remove(m_instanceID);
            }

            public static bool Contains(Object obj)
            {
                Debug.Assert(obj != null);
                var instanceID = obj.GetInstanceID();
                return s_objectScopeSet.Contains(instanceID);
            }
        }

    }

}
