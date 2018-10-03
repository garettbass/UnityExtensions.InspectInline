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
        private class GUIResources
        {
            public readonly GUIStyle
            inDropDownStyle = new GUIStyle("IN DropDown");
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
                var inlineEditor = GetInlineEditor(property);
                if (inlineEditor != null)
                {
                    height += inlineEditor.GetHeight();
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

            var inlineEditor = GetInlineEditor(property);
            var targetExists = inlineEditor != null;

            DoButtonGUI(propertyRect, property, targetExists);
            DoObjectFieldGUI(propertyRect, property, label);
            DoFoldoutGUI(propertyRect, property, targetExists);

            if (targetExists)
            {
                if (property.isExpanded)
                {
                    var inlineEditorRect = position;
                    inlineEditorRect.yMin += propertyRect.height;
                    inlineEditorRect.yMin += EditorGUIUtility.standardVerticalSpacing;
                    DoInlineEditorGUI(inlineEditorRect, inlineEditor);
                }
            }
        }

        //----------------------------------------------------------------------

        private void DoObjectFieldGUI(
            Rect propertyRect,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.ObjectField(propertyRect, property, label);
            if (EditorGUI.EndChangeCheck())
            {
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

        private void DoButtonGUI(
            Rect position,
            SerializedProperty property,
            bool targetExists)
        {
            var targetIsSubasset = attribute.targetIsSubasset;
            if (targetIsSubasset)
            {
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
                    switch (types.Length)
                    {
                        case 0: break;
                        case 1:
                            if (targetExists)
                                DoRemoveSubassetMenu(buttonRect, property);
                            else
                                AddSubasset(property, types, 0);
                            break;
                        default:
                            DoAddRemoveSubassetMenu(
                                buttonRect,
                                property,
                                targetExists,
                                types);
                            break;
                    }
                }
            }
        }

        //----------------------------------------------------------------------

        private void DoAddRemoveSubassetMenu(
            Rect position,
            SerializedProperty property,
            bool targetExists,
            Type[] types)
        {
            var menu = new GenericMenu();
            if (targetExists)
            {
                menu.AddItem(
                    new GUIContent("Remove"),
                    on: false,
                    func: () => RemoveSubasset(property));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove"));
            }

            menu.AddSeparator("");

            var typeIndex = 0;
            var useTypeFullName = types.Length > 16;
            foreach (var type in types)
            {
                var menuPath =
                    useTypeFullName
                    ? type.FullName.Replace('.','/')
                    : type.Name;
                var menuTypeIndex = typeIndex++;
                menu.AddItem(
                    new GUIContent(menuPath),
                    on: false,
                    func: () =>
                        AddSubasset(property, types, menuTypeIndex));
            }
            menu.DropDown(position);
        }

        private void DoRemoveSubassetMenu(
            Rect position,
            SerializedProperty property)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Remove"),
                on: false,
                func: () => RemoveSubasset(property));
            menu.DropDown(position);
        }

        //----------------------------------------------------------------------

        private void DoInlineEditorGUI(Rect position, InlineEditor inlineEditor)
        {
            var inlineEditorHeight = inlineEditor.GetHeight();
            GUILayoutUtility.GetRect(0, -inlineEditorHeight);
            var disabled =
                !attribute.canEditRemoteTarget &&
                !attribute.targetIsSubasset;
            EditorGUI.BeginDisabledGroup(disabled);
            inlineEditor.OnGUI();
            EditorGUI.EndDisabledGroup();
        }

        //----------------------------------------------------------------------

        private class NoScriptPropertyEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUI.BeginChangeCheck();
                var obj = serializedObject;
                obj.Update();

                var property = obj.GetIterator();
                if (property.NextVisible(enterChildren: true))
                    while (property.NextVisible(enterChildren: false))
                        EditorGUILayout.PropertyField(property, true);

                obj.ApplyModifiedProperties();
                EditorGUI.EndChangeCheck();
            }
        }

        //----------------------------------------------------------------------

        private class InlineEditor
        {

            private static readonly Type
            GenericInspector =
                typeof(Editor)
                .Assembly
                .GetType("UnityEditor.GenericInspector");

            private readonly GUIStyle
            tlSelectionButton = new GUIStyle("TL SelectionButton");

            private readonly Editor m_editor;

            private float m_height;

            public InlineEditor(Editor editor)
            {
                var editorType = editor.GetType();
                if (editorType == GenericInspector)
                {
                    var target = editor.target;
                    Object.DestroyImmediate(editor);
                    Editor.CreateCachedEditor(
                        target,
                        typeof(NoScriptPropertyEditor),
                        ref editor);
                }
                m_editor = editor;
            }

            public float GetHeight()
            {
                return m_height;
            }

            public void OnGUI()
            {
                try
                {
                    EditorGUILayout.BeginVertical(tlSelectionButton);
                    EditorGUI.indentLevel += 1;

                    var rectBefore = GUILayoutUtility.GetRect(0, 2);

                    m_editor.OnInspectorGUI();

                    var rectAfter = GUILayoutUtility.GetRect(0, 1);
                    GUILayoutUtility.GetRect(0, -1);

                    m_height = rectAfter.yMax - rectBefore.yMin;
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

        private InlineEditor GetInlineEditor(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            var target = property.objectReferenceValue;
            if (target == null)
                return null;

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

            RemoveSubasset(property);
            var serializedObject = property.serializedObject;
            var subasset = default(Object);

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                subasset = ScriptableObject.CreateInstance(type);
            }
            else if (typeof(Object).IsAssignableFrom(type))
            {
                subasset = (Object)Activator.CreateInstance(type);
            }

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
                Object.DestroyImmediate(subasset);
                return;
            }

            subasset.name = property.displayName;

            var asset = serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            AssetDatabase.AddObjectToAsset(subasset, assetPath);

            property.objectReferenceInstanceIDValue = subasset.GetInstanceID();
            property.isExpanded = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void RemoveSubasset(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var subasset = property.objectReferenceValue;
            if (subasset != null)
            {
                property.objectReferenceValue = null;
                Object.DestroyImmediate(subasset, allowDestroyingAssets: true);
                // TODO: recursively destroy subassets
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

    }

}
