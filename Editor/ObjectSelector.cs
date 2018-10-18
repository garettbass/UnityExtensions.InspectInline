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

    internal static class ObjectSelector
    {

        private static readonly Type
        ObjectSelectorType = 
            typeof(EditorGUI)
            .Assembly
            .GetType("UnityEditor.ObjectSelector");

        private static readonly Func<Object>
        GetObjectSelector =
            (Func<Object>)
            System
            .Delegate
            .CreateDelegate(
                typeof(Func<Object>), null,
                ObjectSelectorType
                .GetProperty("get")
                .GetGetMethod());

        //----------------------------------------------------------------------

        private delegate void
        ShowObjectSelectorDelegate(
            Object obj,
            Type requiredType,
            SerializedProperty property,
            bool allowSceneObjects);

        private static readonly MethodInfo
        ObjectSelector_ShowObjectSelectorInfo =
            ObjectSelectorType
            .GetMethod(
                "Show",
                new Type[]
                {
                    typeof(Object),
                    typeof(Type),
                    typeof(SerializedProperty),
                    typeof(bool)
                });

        public static void Show(
            int controlID,
            Object target,
            Type targetType,
            SerializedProperty property,
            bool allowSceneObjects,
            string searchFilter = "")
        {
            var objectSelector = GetObjectSelector();
            ObjectSelector_ShowObjectSelectorInfo.Invoke(
                objectSelector,
                new object[]
                {
                    target,
                    targetType,
                    property,
                    allowSceneObjects
                });
            SetControlID(objectSelector, controlID);
            SetSearchFilter(objectSelector, searchFilter);
        }

        //----------------------------------------------------------------------

        private const string
        ObjectSelectorClosedCommand = "ObjectSelectorClosed",
        ObjectSelectorUpdatedCommand = "ObjectSelectorUpdated";

        public static void DoGUI(
            int controlID,
            SerializedProperty property,
            Action<SerializedProperty, Object> setObjectReferenceValue)
        {
            var @event = Event.current;
            if (@event.type != EventType.ExecuteCommand)
                return;

            var objectSelector = GetObjectSelector();
            if (objectSelector == null)
                return;

            if (GetControlID(objectSelector) != controlID)
                return;

            switch (@event.commandName)
            {
                case ObjectSelectorClosedCommand:
                    Event.current.Use();
                    break;
                case ObjectSelectorUpdatedCommand:
                    Event.current.Use();
                    AssignSelectedObject(
                        objectSelector,
                        property,
                        setObjectReferenceValue);
                    break;
            }
        }

        //----------------------------------------------------------------------

        private static void AssignSelectedObject(
            Object objectSelector,
            SerializedProperty property,
            Action<SerializedProperty, Object> setObjectReferenceValue)
        {
            var newInstanceID = GetSelectedInstanceID(objectSelector);
            var oldInstanceID = property.objectReferenceInstanceIDValue;
            if (oldInstanceID != newInstanceID)
            {
                var newTarget = EditorUtility.InstanceIDToObject(newInstanceID);
                setObjectReferenceValue(property, newTarget);
                GUI.changed = true;
            }
        }

        //----------------------------------------------------------------------

        private static readonly FieldInfo
        ObjectSelector_objectSelectorID =
            ObjectSelectorType
            .GetField(
                "objectSelectorID",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        private static int GetControlID(Object objectSelector)
        {
            return
                (int)
                ObjectSelector_objectSelectorID
                .GetValue(objectSelector);
        }

        private static void SetControlID(
            Object objectSelector,
            int controlID)
        {
            ObjectSelector_objectSelectorID
            .SetValue(objectSelector, controlID);
        }

        //----------------------------------------------------------------------

        private static readonly MethodInfo
        ObjectSelector_GetSelectedInstanceID =
            ObjectSelectorType
            .GetMethod(
                "GetSelectedInstanceID",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        private static int GetSelectedInstanceID(Object objectSelector)
        {
            return
                (int)
                ObjectSelector_GetSelectedInstanceID
                .Invoke(objectSelector, null);
        }

        private static Object GetSelectedObject(Object objectSelector)
        {
            int instanceID = GetSelectedInstanceID(objectSelector);
            return EditorUtility.InstanceIDToObject(instanceID);
        }

        //----------------------------------------------------------------------

        private static readonly PropertyInfo
        ObjectSelector_searchFilterInfo =
            ObjectSelectorType
            .GetProperty(
                "searchFilter",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        private static void SetSearchFilter(Object objectSelector, string value)
        {
            ObjectSelector_searchFilterInfo
            .SetValue(objectSelector, value, null);
        }

    }

}