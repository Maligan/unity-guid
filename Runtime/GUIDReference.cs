using System;
using UnityEngine;

#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

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
    [SerializeField] private SceneAsset m_SceneAsset;
    [SerializeField] private string m_ObjectName;

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize()
    {
        m_Scene = m_SceneAsset ? m_SceneAsset.name : null;
    }
}

[CustomPropertyDrawer(typeof(GUIDReference))]
public class GUIDRefereceDrawer : PropertyDrawer
{
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
        var pSceneName = property.FindPropertyRelative("m_Scene");
        var pSceneAsset = property.FindPropertyRelative("m_SceneAsset");
        var pGUID = property.FindPropertyRelative("m_GUID");
        var pName = property.FindPropertyRelative("m_ObjectName");


        //
        // Context Menu
        //
        if (Event.current.type == EventType.ContextClick && position.Contains(Event.current.mousePosition))
        {
            GenericMenu context = new GenericMenu();
            AddItem(context, "Copy GUID", CanCopyGUID(), CopyGUID);
            context.AddSeparator(string.Empty);
            AddItem(context, "Open Scene", CanOpenScene(), OpenScene);
            AddItem(context, "Close Scene", CanCloseScene(), CloseScene);
            context.ShowAsContext();
            Event.current.Use();
            return;
        }


        //
        // Draw as ObjectField
        //
        var component = GUIDComponent.Find(pGUID.stringValue);

        var useObjectField = false;
        useObjectField |= component != null;
        useObjectField |= string.IsNullOrEmpty(pGUID.stringValue);
        useObjectField &= !pGUID.hasMultipleDifferentValues;

        if (useObjectField)
        {
            var value = (GUIDComponent)EditorGUI.ObjectField(position, label, component, typeof(GUIDComponent), true);
            if (value != component)
            {
                if (value != null)
                {
                    pGUID.stringValue = value.Value;
                    pName.stringValue = value.name;
                    // XXX: Unsaved Scene
                    pSceneAsset.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(value.gameObject.scene.path); 
                    pSceneName.stringValue = value.gameObject.scene.name;
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

        //
        // Draw as Custom
        //
        else
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive, position);
            
            var totalPos = position;
            var fieldPos = position; fieldPos.xMin += EditorGUIUtility.labelWidth + 2;
            var buttonPos = objectFieldButton.margin.Remove(new Rect(position.xMax - 19, position.y, 19, position.height));

            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    if (Event.current.button == 0 && totalPos.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.clickCount == 1)
                            EditorGUIUtility.PingObject(pSceneAsset.objectReferenceValue);
                        else
                            OpenSceneAndPing();

                        Event.current.Use();
                    }
                    break;

                case EventType.Repaint:

                    // EditorGUI.BeginHandleMixedValueContentColor();

                    var content = new GUIContent();
                    content.image = AssetPreview.GetMiniThumbnail(property.serializedObject.targetObject); // XXX: Type
                    content.text = $"{pName.stringValue} ({ObjectNames.NicifyVariableName(nameof(GUIDComponent))})";

                    using (new EditorGUIUtility.IconSizeScope(new Vector2(12, 12)))
                    {
                        // Prefix
                        EditorGUI.PrefixLabel(totalPos, controlId, new GUIContent(label.text + "*"));
                        // Field
                        EditorStyles.objectField.Draw(fieldPos, content, controlId, DragAndDrop.activeControlID == controlId, fieldPos.Contains(Event.current.mousePosition));
                        // Button
                        objectFieldButton.Draw(buttonPos, GUIContent.none, controlId, DragAndDrop.activeControlID == controlId, buttonPos.Contains(Event.current.mousePosition));
                    }

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
            var sceneAsset = (SceneAsset)pSceneAsset.objectReferenceValue;
            var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        }

        void OpenSceneAndPing()
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
    }

    private void AddItem(GenericMenu menu, string name, bool enabled, GenericMenu.MenuFunction action)
    {
        var label = EditorGUIUtility.TrTextContent(name).text;

        if (enabled)
            menu.AddItem(new GUIContent(label), false, action);
        else
            menu.AddDisabledItem(new GUIContent(label));
    }
}

#endif