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
            var types = assemblies.SelectMany(
                a => {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        return e.Types.Where(t => t != null);
                    }
                }
            );
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
                using (new ObjectScope(asset))
                {
                    var target = property.objectReferenceValue;
                    var targetExists = target != null;
                    if (targetExists && !ObjectScope.Contains(target))
                    {
                        var spacing = EditorGUIUtility.standardVerticalSpacing;
                        height += spacing;
                        height += GetInlinePropertyHeight(target);
                        height += 1;
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

            DoContextMenuGUI(propertyRect, property);
            DoObjectFieldGUI(propertyRect, property, label);
            DoFoldoutGUI(propertyRect, property);

            if (property.isExpanded)
            {
                var serializedObject = property.serializedObject;
                var asset = serializedObject.targetObject;
                using (new ObjectScope(asset))
                {
                    var target = property.objectReferenceValue;
                    var targetExists = target != null;
                    if (targetExists && !ObjectScope.Contains(target))
                    {
                        var enabled =
                            attribute.canEditRemoteTarget ||
                            TargetIsSubassetOf(asset, target);
                        var inlineRect = position;
                        inlineRect.yMin = propertyRect.yMax;
                        var spacing = EditorGUIUtility.standardVerticalSpacing;
                        inlineRect.xMin += 2;
                        inlineRect.xMax -= 18;
                        inlineRect.yMin += spacing;
                        inlineRect.yMax -= 1;
                        DoInlinePropertyGUI(inlineRect, target, enabled);
                    }
                }
            }

            DiscardObsoleteSerializedObjectsOnNextEditorUpdate();
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
            SerializedProperty property)
        {
            if (attribute.canCreateSubasset == false)
                return;

            var controlID = GetControlID(position);
            ObjectSelector.DoGUI(controlID, property, SetObjectReferenceValue);

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
                    types);
            }
        }

        private static void SetObjectReferenceValue(
            SerializedProperty property,
            Object newTarget)
        {
            var serializedObject = property.serializedObject;
            var oldSubassets = property.FindReferencedSubassets();
            property.objectReferenceValue = newTarget;
            property.isExpanded = true;
            if (oldSubassets.Any())
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                serializedObject.DestroyUnreferencedSubassets(oldSubassets);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
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

            /* Can also get the BaseType */
            if (objectType.IsArray)
            {
                objectType = objectType.GetElementType();
            }
            else if (objectType.IsGenericType)
            {
                objectType = objectType.GenericTypeArguments[0];
            }

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
                SetObjectReferenceValue(property, newTarget);
            }
        }

        //----------------------------------------------------------------------

        private void DoFoldoutGUI(
            Rect position,
            SerializedProperty property)
        {
            var foldoutRect = position;
            foldoutRect.width = EditorGUIUtility.labelWidth;

            var target = property.objectReferenceValue;
            var targetExists = target != null;
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
            Type[] types)
        {
            var menu = new GenericMenu();

            menu.AddItem(
                gui.selectContent,
                on: false,
                func: () => ShowObjectSelector(controlID, property));

            menu.AddSeparator("");

            var target = property.objectReferenceValue;
            if (target != null && TargetIsSubassetOf(property))
                menu.AddItem(
                    gui.deleteSubassetContent,
                    on: false,
                    func: () => DestroyTarget(property));
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
                    var createAssetMenuAttribute =
                        (CreateAssetMenuAttribute)
                        type.GetCustomAttribute(
                            typeof(CreateAssetMenuAttribute));
                    var menuPath =
                        createAssetMenuAttribute != null
                        ? createAssetMenuAttribute.menuName
                        : useTypeFullName
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

        private float GetInlinePropertyHeight(Object target)
        {
            var serializedObject = GetSerializedObject(target);
            serializedObject.Update();
            var height = 2f;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var properties = serializedObject.EnumerateChildProperties();
            foreach (var property in properties)
            {
                height += spacing;
                height +=
                    EditorGUI
                    .GetPropertyHeight(property, includeChildren: true);
            }
            if (height > 0)
                height += spacing;
            return height;
        }

        private void DoInlinePropertyGUI(
            Rect position,
            Object target,
            bool enabled)
        {
            DrawInlineBackground(position);
            var serializedObject = GetSerializedObject(target);
            serializedObject.Update();
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var properties = serializedObject.EnumerateChildProperties();
            position.xMin += 14;
            position.xMax -= 5;
            position.yMin += 1;
            position.yMax -= 1;
            EditorGUI.BeginDisabledGroup(!enabled);
            foreach (var property in properties)
            {
                position.y += spacing;
                position.height =
                    EditorGUI
                    .GetPropertyHeight(property, includeChildren: true);
                EditorGUI
                .PropertyField(position, property, includeChildren: true);
                position.y += position.height;
            }
            EditorGUI.EndDisabledGroup();
            if (enabled)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void DrawInlineBackground(Rect position)
        {
            var isRepaint = Event.current.type == EventType.Repaint;
            if (isRepaint)
            {
                // var style = new GUIStyle("ProgressBarBack");
                // var style = new GUIStyle("Badge");
                // var style = new GUIStyle("HelpBox");
                // var style = new GUIStyle("ObjectFieldThumb");
                var style = new GUIStyle("ShurikenEffectBg");
                using (ColorAlphaScope(0.5f))
                {
                    style.Draw(position, false, false, false, false);
                }
                // EditorGUI.DrawRect()
            }
        }

        //----------------------------------------------------------------------

        private readonly Dictionary<Object, SerializedObject>
        m_serializedObjectMap = new Dictionary<Object, SerializedObject>();

        private SerializedObject GetSerializedObject(Object target)
        {
            Debug.Assert(target != null);
            var serializedObject = default(SerializedObject);
            if (m_serializedObjectMap.TryGetValue(target, out serializedObject))
                return serializedObject;

            serializedObject = new SerializedObject(target);
            m_serializedObjectMap.Add(target, serializedObject);
            return serializedObject;
        }

        private void DiscardObsoleteSerializedObjects()
        {
            var map = m_serializedObjectMap;
            var destroyedObjects = map.Keys.Where(key => key == null);
            if (destroyedObjects.Any())
            {
                foreach (var @object in destroyedObjects.ToArray())
                {
                    map.Remove(@object);
                }
            }
        }

        private void DiscardObsoleteSerializedObjectsOnNextEditorUpdate()
        {
            EditorApplication.delayCall -= DiscardObsoleteSerializedObjects;
            EditorApplication.delayCall += DiscardObsoleteSerializedObjects;
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

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (assetPath == null)
                return false;

            var targetPath = AssetDatabase.GetAssetPath(target);
            if (targetPath == null)
                return false;

            return assetPath == targetPath;
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
            var subassetType = types[typeIndex];

            var subasset = CreateInstance(subassetType);
            if (subasset == null)
            {
                Debug.LogErrorFormat(
                    "Failed to create subasset of type {0}",
                    subassetType.FullName);
                return;
            }

            if (!CanAddSubasset(subasset))
            {
                Debug.LogErrorFormat(
                    "Cannot save subasset of type {0}",
                    subassetType.FullName);
                TryDestroyImmediate(subasset, allowDestroyingAssets: true);
                return;
            }

            subasset.name = subassetType.Name;

            var serializedObject = property.serializedObject;
            serializedObject.targetObject.AddSubasset(subasset);
            SetObjectReferenceValue(property, subasset);
        }

        //----------------------------------------------------------------------

        private void DestroyTarget(SerializedProperty property)
        {
            var target = property.objectReferenceValue;
            if (target != null)
            {
                SetObjectReferenceValue(property, null);
            }
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
                if (obj == null)
                    return false;
                var instanceID = obj.GetInstanceID();
                return s_objectScopeSet.Contains(instanceID);
            }
        }

        //======================================================================

        protected struct Deferred : IDisposable
        {
            private readonly Action _onDispose;

            public Deferred(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_onDispose != null)
                    _onDispose();
            }
        }

        protected static Deferred ColorScope(Color newColor)
        {
            var oldColor = GUI.color;
            GUI.color = newColor;
            return new Deferred(() => GUI.color = oldColor);
        }

        protected static Deferred ColorAlphaScope(float a)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            return new Deferred(() => GUI.color = oldColor);
        }

        protected static Deferred IndentLevelScope(int indent = 1)
        {
            EditorGUI.indentLevel += indent;
            return new Deferred(() => EditorGUI.indentLevel -= indent);
        }
    }

}
