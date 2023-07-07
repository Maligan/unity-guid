using System;
using System.Reflection;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public partial class GUIDReference
{
    public string Scene => m_Scene;
    public string GUID => m_GUID;

    [SerializeField] private string m_Scene;
    [SerializeField] private string m_GUID;

    public bool TryGetComponent<T>(out T value)
    {
        return (value = GUIDComponent.Find<T>(m_GUID)) != null;
    }
}

#if UNITY_EDITOR

public partial class GUIDReference : ISerializationCallbackReceiver
{
    [SerializeField] private SceneAsset m_SceneAsset;
    [SerializeField] private string m_Name;

    public void OnAfterDeserialize() { }
    public void OnBeforeSerialize()
    {
        m_Scene = m_SceneAsset ? m_SceneAsset.name : null;
    }
}

[CustomPropertyDrawer(typeof(GUIDReference))]
public class GUIDRefereceDrawer : PropertyDrawer
{
    private static GUIContent k_DropdownButtonContent = EditorGUIUtility.IconContent("_Menu");

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
        var pName = property.FindPropertyRelative("m_Name");

        var currentValue = GUIDComponent.Find(pGUID.stringValue);


        if (Event.current.type == EventType.ContextClick && position.Contains(Event.current.mousePosition))
        {
            GenericMenu context = new GenericMenu();
            AddItem(context, EditorGUIUtility.TrTextContent("Copy GUID").text, CanCopyGUID(), CopyGUID);
            context.AddSeparator(string.Empty);
            AddItem(context, EditorGUIUtility.TrTextContent("Open Scene").text, CanOpenScene(), OpenScene);
            AddItem(context, EditorGUIUtility.TrTextContent("Close Scene").text, CanCloseScene(), CloseScene);
            context.ShowAsContext();
            Event.current.Use();
            return;
        }


        // [OBJECT]
        if (!pGUID.hasMultipleDifferentValues && (currentValue != null || string.IsNullOrEmpty(pGUID.stringValue)))
        {
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.ObjectField(position, label, currentValue, typeof(GUIDComponent), true);
            if (EditorGUI.EndChangeCheck())
            {
                var component = (GUIDComponent)value;
                if (component != null)
                {
                    pGUID.stringValue = component.Value;
                    pName.stringValue = component.name;
                    pSceneAsset.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(component.gameObject.scene.path); // XXX: Unsaved Scene
                    pSceneName.stringValue = component.gameObject.scene.name;
                }
                else
                {
                    pGUID.stringValue = string.Empty;
                    pName.stringValue = string.Empty;
                    pSceneAsset.objectReferenceValue = null;
                    pSceneName.stringValue = null;
                }
            }
        }
        else
        {
            EditorGUI.PrefixLabel(position, label);

            var content = new GUIContent();

            if (pGUID.hasMultipleDifferentValues)
            {
                // Mixed Values
                content.text = "—";
            }
            else
            {
                content.image = EditorGUIUtility.ObjectContent(property.serializedObject.targetObject, typeof(GUIDComponent)).image;
                content.text = $"{pName.stringValue} ({ObjectNames.NicifyVariableName(nameof(GUIDComponent))})";
            }

            using (new EditorGUIUtility.IconSizeScope(new Vector2(12, 12)))
            {
                // Draw
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && position.Contains(evt.mousePosition))
                {
                    if (evt.clickCount == 1)
                    {
                        EditorGUIUtility.PingObject(pSceneAsset.objectReferenceValue);
                    }
                    else
                    {
                        OpenSceneAndPing();
                    }

                    evt.Use();
                }

                position.xMin += EditorGUIUtility.labelWidth + 2;
                GUI.Button(position, content, EditorStyles.objectField);

                var buttonPosition = new Rect(position.xMax - 19, position.y, 19, position.height);
                var buttonRect = objectFieldButton.margin.Remove(buttonPosition);
                GUI.Button(buttonRect, GUIContent.none, objectFieldButton);
            }

        }

        //
        // Context
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

            // XXX: Delay to scene opened?
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
            
            // deny if it's the same scene
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

/*
[CustomPropertyDrawer(typeof(GUIDReference<>))]
public class GUIDRefereceDrawer : PropertyDrawer
{
    private const string kScene = "_sceneAsset";
    private const string kGUID = "_guid";
    private const string kName = "_guidName";

    private GUIStyle m_ButtonIcon;
    private GUIStyle m_DropdownMissing;
    private GUIStyle m_DropdownNormal;

    public GUIDRefereceDrawer()
    {
        m_ButtonIcon = new GUIStyle(GUI.skin.button);
        m_ButtonIcon.padding = new RectOffset();

        m_DropdownNormal = EditorStyles.popup;
        m_DropdownMissing = new GUIStyle(m_DropdownNormal);
        m_DropdownMissing.normal.textColor = 
        m_DropdownMissing.hover.textColor = 
        m_DropdownMissing.active.textColor = 
        m_DropdownMissing.focused.textColor = new Color(1f, 0.25f, 0.25f, 1); 
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var scene = property.FindPropertyRelative(kScene);

        EditorGUI.PrefixLabel(position, label);
        position.xMin += EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing;
        position.width -= EditorGUIUtility.singleLineHeight + 2* EditorGUIUtility.standardVerticalSpacing;
        position.width /= 2;
        EditorGUI.PropertyField(position, scene, GUIContent.none);

        position.x = position.xMax + EditorGUIUtility.standardVerticalSpacing;
        DropdownMenu(position, property);

        EditorGUI.BeginDisabledGroup(IsTogglable(scene, out var sceneIsLoaded) == false);
        position.x = position.xMax + EditorGUIUtility.standardVerticalSpacing;
        position.width = EditorGUIUtility.singleLineHeight;
        if (GUI.Button(position, sceneIsLoaded ? "\u2191" : "\u2193", m_ButtonIcon)) Toggle(scene);
        EditorGUI.EndDisabledGroup();
    }

    private void DropdownMenu(Rect position, SerializedProperty property)
    {
        IsTogglable(property.FindPropertyRelative(kScene), out var hasScene);

        var options = FindGUIDs(property);

        var guidProp = property.FindPropertyRelative(kGUID);
        var nameProp = property.FindPropertyRelative(kName);

        var dropdownLabel = nameProp.stringValue;
        var dropdownStyle = hasScene && !options.ContainsKey(guidProp.stringValue) ? m_DropdownMissing : m_DropdownNormal;

        EditorGUI.BeginDisabledGroup(hasScene == false);
        var dropdown = EditorGUI.DropdownButton(position, new GUIContent(nameProp.stringValue), FocusType.Passive, dropdownStyle);
        EditorGUI.EndDisabledGroup();

        if (dropdown)
        {
            var menu = new GenericMenu();

            foreach (var pair in options)
            {
                if (pair.Key == guidProp.stringValue)
                    menu.AddDisabledItem(new GUIContent(pair.Value));
                else
                    menu.AddItem(new GUIContent(pair.Value), false, OnGUIDSelect, pair);
            }

            menu.DropDown(position);
        }

        void OnGUIDSelect(object ctx)
        {
            var data = (KeyValuePair<string, string>)ctx;
            guidProp.stringValue = data.Key;
            nameProp.stringValue = data.Value;
            guidProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Static

    private static bool IsTogglable(SerializedProperty property, out bool mode)
    {
        var scenePath = AssetDatabase.GetAssetPath(property.objectReferenceValue);
        var scene = EditorSceneManager.GetSceneByPath(scenePath);
        mode = scene.IsValid();

        var target = property.serializedObject.targetObject;
        if (target is Component)
        {
            var component = (Component)target;
            if (component.gameObject.scene == scene)
                return false;
        }

        return true;
    }

    private static void Toggle(SerializedProperty property)
    {
        var scenePath = AssetDatabase.GetAssetPath(property.objectReferenceValue);
        var scene = EditorSceneManager.GetSceneByPath(scenePath);
        
        if (scene.IsValid())
            EditorSceneManager.CloseScene(scene, true);
        else
            EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    }

    private static Dictionary<string, string> FindGUIDs(SerializedProperty property)
    {
        var result = new Dictionary<string, string>();

        var sceneProp = property.FindPropertyRelative(kScene);
        var scenePath = AssetDatabase.GetAssetPath(sceneProp.objectReferenceValue);
        var scene = EditorSceneManager.GetSceneByPath(scenePath);

        var type = GetTypeOf(property).GenericTypeArguments[0];

        var components = UnityEngine.Object.FindObjectsOfType<GUIDComponent>();
        foreach (var component in components)
            if (component.gameObject.scene == scene)
                if (component.TryGetComponent(type, out var _))
                    result.Add(component.Value, component.name);

        return result;
    }

    private static Type GetTypeOf(SerializedProperty prop)
    {
        var path = prop.propertyPath.Split('.');
        var type = prop.serializedObject.targetObject.GetType();
        
        for(int i = 0; i < path.Length; i++)
        {
            if (path[i] == "Array")
            {
                i++;
                type = type.GetElementType();
            }                   
            else
            {
                type = type.GetField(path[i], BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance).FieldType; 
            }
        }

        return type;
    }
}
*/

#endif