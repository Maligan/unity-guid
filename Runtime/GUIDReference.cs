using System;

#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace UnityEngine
{
    [Serializable]
    public partial class GUIDReference
    {
        public string GUID => m_GUID;
        public string Scene => m_Scene;

        [SerializeField] private string m_GUID;
        [SerializeField] private string m_Scene;

        public T GetComponent<T>()
        {
            return GUIDComponent.Find<T>(m_GUID);
        }

        public bool TryGetComponent<T>(out T value)
        {
            return (value = GUIDComponent.Find<T>(m_GUID)) != null;
        }
    }

#if UNITY_EDITOR
    public partial class GUIDReference : ISerializationCallbackReceiver
    {
        [SerializeField] private SceneAsset m_EditorSceneAsset;
        [SerializeField] private string m_EditorGameObjectName;

        public void OnAfterDeserialize() { }
        public void OnBeforeSerialize()
        {
            m_Scene = m_EditorSceneAsset ? m_EditorSceneAsset.name : null;
        }
    }

    [CustomPropertyDrawer(typeof(GUIDReference))]
    public class GUIDRefereceDrawer : PropertyDrawer
    {
        private static readonly GUIContent s_MixedValueContent = EditorGUIUtility.TrTextContent("\u2014", "Mixed Values");
        private static readonly Color s_MixedValueContentColor = new Color(1, 1, 1, 0.5f);

        private static Texture2D iconCache;
        private static Texture2D icon
        {
            get
            {
                if (iconCache == null)
                {
                    var scriptType = typeof(GUIDComponent);
                    var scriptAsset = AssetDatabase.FindAssets(scriptType.Name + " t:" + nameof(MonoScript));
                    var scriptPath = AssetDatabase.GUIDToAssetPath(scriptAsset[0]);
                    var scriptObject = AssetDatabase.LoadAssetAtPath(scriptPath, typeof(MonoScript));
                    iconCache = AssetPreview.GetMiniThumbnail(scriptObject);
                }

                return iconCache;
            }
        }

        private static GUIStyle objectFieldButtonCache;
        private static GUIStyle objectFieldButton
        {
            get
            {
                if (objectFieldButtonCache == null)
                {
                    objectFieldButtonCache = (GUIStyle)typeof(EditorStyles)
                        .GetProperty(nameof(objectFieldButton), BindingFlags.NonPublic | BindingFlags.Static)
                        .GetValue(null); 
                }

                return objectFieldButtonCache;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var pGUID = property.FindPropertyRelative("m_GUID");
            var pSceneName = property.FindPropertyRelative("m_Scene");
            var pSceneAsset = property.FindPropertyRelative("m_EditorSceneAsset");
            var pName = property.FindPropertyRelative("m_EditorGameObjectName");

            //
            // Draw as ObjectField
            //
            {
                var controlId = GUIUtility.GetControlID(FocusType.Keyboard, position);
                
                var totalPos = position;
                var fieldPos = position; fieldPos.xMin += EditorGUIUtility.labelWidth + 2;
                var buttonPos = objectFieldButton.margin.Remove(new Rect(position.xMax - 19, position.y, 19, position.height));

                switch (Event.current.type)
                {
                    case EventType.ContextClick:
                        if (totalPos.Contains(Event.current.mousePosition))
                        {
                            GenericMenu context = new GenericMenu();
                            AddItem(context, "Copy GUID", CanCopyGUID(), CopyGUID);
                            context.AddSeparator(string.Empty);
                            AddItem(context, "Open Scene", CanOpenScene(), OpenScene);
                            AddItem(context, "Close Scene", CanCloseScene(), CloseScene);
                            context.ShowAsContext();
                            Event.current.Use();
                        }
                        break;

                    case EventType.DragUpdated:
                        if (fieldPos.Contains(Event.current.mousePosition) && TryGetGUID(DragAndDrop.objectReferences, out _))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                            DragAndDrop.activeControlID = controlId;
                            Event.current.Use();
                        }
                        else
                        {
                            DragAndDrop.activeControlID = 0;
                        }
                        break;
                    
                    case EventType.DragPerform:
                        if (fieldPos.Contains(Event.current.mousePosition) && TryGetGUID(DragAndDrop.objectReferences, out var newValue))
                        {
                            SetValue(newValue);
                            GUI.changed = true;
                            DragAndDrop.AcceptDrag();
                            DragAndDrop.activeControlID = 0;
                            Event.current.Use();
                        }
                        break;

                    case EventType.MouseDown:
                        if (Event.current.button == 0 && totalPos.Contains(Event.current.mousePosition))
                        {
                            if (buttonPos.Contains(Event.current.mousePosition))
                            {
                                EditorGUIUtility.ShowObjectPicker<GUIDComponent>(GUIDComponent.Find(pGUID.stringValue), true, string.Empty, controlId);
                                Event.current.Use();
                            }
                            else if (fieldPos.Contains(Event.current.mousePosition))
                            {
                                Ping(Event.current.clickCount > 1);
                                Event.current.Use();
                            }
                            else
                            {
                                GUIUtility.keyboardControl = controlId;
                                Event.current.Use();
                            }
                        }
                        break;
                    
                    case EventType.KeyDown:
                        if (GUIUtility.keyboardControl == controlId)
                        {
                            var hasModifier = (Event.current.alt || Event.current.shift || Event.current.command || Event.current.control);
                            if (hasModifier)
                                break;

                            var cmdDelete = Event.current.keyCode == KeyCode.Backspace || Event.current.keyCode == KeyCode.Delete;
                            if (cmdDelete)
                            {
                                SetValue(null);
                                GUI.changed = true;
                                Event.current.Use();
                                break;
                            }

                            var cmdSelect = Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return;
                            if (cmdSelect)
                            {
                                EditorGUIUtility.ShowObjectPicker<GUIDComponent>(GUIDComponent.Find(pGUID.stringValue), true, string.Empty, controlId);
                                Event.current.Use();
                                break;
                            }
                        }
                        break;

                    case EventType.ExecuteCommand:
                        if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == controlId)
                        {
                            if (TryGetGUID(new [] { EditorGUIUtility.GetObjectPickerObject() }, out var value))
                            {
                                SetValue(value);
                                GUI.changed = true;
                                Event.current.Use();
                            }
                        }
                        break;

                    case EventType.Repaint:

                        // Prefix
                        EditorGUI.PrefixLabel(totalPos, controlId, label);

                        // Field
                        var prevColor = GUI.contentColor;
                        if (pGUID.hasMultipleDifferentValues || pSceneAsset.objectReferenceValue && !IsLoaded(pSceneAsset)) GUI.contentColor *= s_MixedValueContentColor;
                        EditorStyles.objectField.Draw(fieldPos, GetContent(pGUID, pName, pSceneAsset), controlId, DragAndDrop.activeControlID == controlId, fieldPos.Contains(Event.current.mousePosition));
                        GUI.contentColor = prevColor;

                        // Button
                        var prevSize = EditorGUIUtility.GetIconSize();
                        EditorGUIUtility.SetIconSize(new Vector2(12, 12));
                        objectFieldButton.Draw(buttonPos, GUIContent.none, controlId, DragAndDrop.activeControlID == controlId, buttonPos.Contains(Event.current.mousePosition));
                        EditorGUIUtility.SetIconSize(prevSize);

                        break;
                }
            }

            //
            // Context Menu (Actions)
            //

            void CopyGUID()
            {
                GUIUtility.systemCopyBuffer = pGUID.stringValue;
            }

            void OpenScene()
            {
                var scenePath = AssetDatabase.GetAssetPath(pSceneAsset.objectReferenceValue);
                if (string.IsNullOrEmpty(scenePath))
                    return;

                EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }

            void Ping(bool doubleClick)
            {
                if (pGUID.hasMultipleDifferentValues)
                    return;

                var targetGUID = pGUID.stringValue;
                var target = GUIDComponent.Find(targetGUID);

                if (target != null)
                {
                    if (!doubleClick) EditorGUIUtility.PingObject(target);
                    else Selection.activeObject = target;
                }
                else
                {
                    if (!doubleClick) EditorGUIUtility.PingObject(pSceneAsset.objectReferenceValue);
                    else
                    {
                        OpenScene();

                        // TODO: Should we delay until scene is loaded?
                        EditorApplication.delayCall += () =>
                        {
                            var targetGUID = pGUID.stringValue;
                            var target = GUIDComponent.Find(targetGUID);
                            EditorGUIUtility.PingObject(target);
                        };
                    }
                }
            }

            void CloseScene()
            {
                var sceneAsset = (SceneAsset)pSceneAsset.objectReferenceValue;
                var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                var scene = EditorSceneManager.GetSceneByPath(scenePath);
                EditorSceneManager.CloseScene(scene, true);
            }

            bool CanCopyGUID()
            {
                return !pGUID.hasMultipleDifferentValues
                    && !string.IsNullOrEmpty(pGUID.stringValue);
            }

            bool CanOpenScene()
            {
                var sceneAsset = (SceneAsset)pSceneAsset.objectReferenceValue;
                if (sceneAsset == null)
                    return false;
                
                var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (scenePath == null)
                    return false;
                
                var scene = EditorSceneManager.GetSceneByPath(scenePath);
                if (scene.isLoaded)
                    return false;

                return true;
            }

            bool CanCloseScene()
            {
                var sceneAsset = (SceneAsset)pSceneAsset.objectReferenceValue;
                if (sceneAsset == null)
                    return false;
                
                var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (scenePath == null)
                    return false;
                
                var scene = EditorSceneManager.GetSceneByPath(scenePath);
                if (!scene.isLoaded)
                    return false;

                // deny if scene is not saved (or has changes)
                if (scene.isDirty)
                    return false;
                
                // deny if there is only current scene
                var hasOnlyScene = property
                    .serializedObject
                    .targetObjects
                    .All(x => x is Component component && component.gameObject.scene == scene);
                
                if (hasOnlyScene)
                    return false;

                return true;
            }

            void SetValue(GUIDComponent component)
            {
                if (component != null)
                {
                    pGUID.stringValue = component.Value;
                    pName.stringValue = component.name;
                    // XXX: Unsaved Scene
                    pSceneAsset.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(component.gameObject.scene.path); 
                    pSceneName.stringValue = component.gameObject.scene.name;
                }
                else
                {
                    pGUID.stringValue = string.Empty;
                    pName.stringValue = string.Empty;
                    pSceneAsset.objectReferenceValue = null;
                    pSceneName.stringValue = null;
                }

                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private static GUIContent GetContent(SerializedProperty guid, SerializedProperty name, SerializedProperty scene)
        {
            if (guid.hasMultipleDifferentValues)
                return s_MixedValueContent;

            var className = ObjectNames.NicifyVariableName(nameof(GUIDComponent));

            if (string.IsNullOrEmpty(guid.stringValue))
                return new GUIContent($"None ({className})");

            if (scene.objectReferenceValue == null)
                return new GUIContent($"Missing ({className})");

            if (IsLoaded(scene) && GUIDComponent.Find(guid.stringValue) == null)
                return new GUIContent($"Missing ({className})");
            
            return new GUIContent($"{name.stringValue} ({className})", icon);
        }

        private static bool IsLoaded(SerializedProperty scene)
        {
            var sceneAsset = (SceneAsset)scene.objectReferenceValue;
            if (sceneAsset == null)
                return false;

            var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (scenePath == null)
                return false;
            
            var sceneObject = EditorSceneManager.GetSceneByPath(scenePath);
            if (sceneObject.IsValid() == false)
                return false;

            return sceneObject.isLoaded;
        }

        private static void AddItem(GenericMenu menu, string name, bool enabled, GenericMenu.MenuFunction action)
        {
            var label = EditorGUIUtility.TrTextContent(name).text;

            if (enabled)
                menu.AddItem(new GUIContent(label), false, action);
            else
                menu.AddDisabledItem(new GUIContent(label));
        }

        private static bool TryGetGUID(UnityEngine.Object[] references, out GUIDComponent result)
        {
            result = null;

            for (var i = 0; i < references.Length; i++)
            {
                switch (references[i])
                {
                    case GameObject gameObject:
                        gameObject.TryGetComponent<GUIDComponent>(out result);
                        break;
                    
                    case GUIDComponent component:
                        result = component;
                        break;
                }
            }

            return result != null && !string.IsNullOrEmpty(result.Value);
        }
    }
#endif
}