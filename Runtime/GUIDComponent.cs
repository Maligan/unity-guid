using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine
{
    [AddComponentMenu("Miscellaneous/GUID")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(int.MinValue)]
    public partial class GUIDComponent : MonoBehaviour
    {
        private static Dictionary<string, GUIDComponent> sActive = new Dictionary<string, GUIDComponent>();

        public static GUIDComponent Find(string guid) => Find<GUIDComponent>(guid);

        public static T Find<T>(string guid)
        {
            if (sActive.TryGetValue(guid, out var component) && component != null)
                return component.GetComponent<T>();

            return default(T);
        }

        public string Value => m_Value;

        [SerializeField]
        private string m_Value;

        // NB! It's possible to load one scene multiple times
        // so it's a valid situation to have GUIDs collision.
        // TODO: How handle this scenario
        private void Awake() => sActive.TryAdd(m_Value, this);

        private void OnDestroy() => sActive.Remove(m_Value);
    }

#if UNITY_EDITOR
    public partial class GUIDComponent
    {
        private int m_EditorInstanceId;
        private string m_EditorValue;

        private void Reset()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            if (IsInstance())
            {
                // in case object was duplicated (with Ctrl+D)
                if (IsDuplicate())
                    m_Value = string.Empty;

                // don't change GUID after reset or revert prefab overrides
                if (IsReverted())
                    m_Value = m_EditorValue;

                if (string.IsNullOrEmpty(m_Value))
                    m_Value = Guid.NewGuid().ToString();

                // we don't care about duplicates and/or previous values here
                // this is just editor time and sActive is not serialized
                sActive[m_Value] = this;
            }
            else
            {
                m_Value = string.Empty;
            }

            m_EditorValue = m_Value;
            m_EditorInstanceId = GetInstanceID();
        }

        private bool IsDuplicate()
        {
            // If we are deserializing object during scene loading
            // when it's not an duplicate but just loading scene into memory
            return gameObject.scene.isLoaded
                && string.IsNullOrEmpty(m_Value) == false
                && m_EditorInstanceId != GetInstanceID();
        }

        private bool IsReverted()
        {
            // !IsDuplicate()
            return string.IsNullOrEmpty(m_Value)
                && string.IsNullOrEmpty(m_EditorValue) == false;
        }

        private bool IsInstance()
        {
            return !string.IsNullOrEmpty(gameObject.scene.path);
        }

        [CustomEditor(typeof(GUIDComponent))]
        [CanEditMultipleObjects]
        public class GUIDComponentEditor : Editor
        {
            private const string kValue = nameof(GUIDComponent.m_Value);
            private const string kGUID = "Value";
            private const string kHelp = "All instances of this prefab will have serializable GUID";

            public override void OnInspectorGUI()
            {
                var value = serializedObject.FindProperty(kValue);

                // nb! hide prefab override state
                value.prefabOverride = false;
                
                var isPrefab = false;
                for (var i = 0; i < targets.Length && !isPrefab; i++)
                    isPrefab |= !(targets[i] as GUIDComponent).IsInstance();

                if (isPrefab)
                {
                    EditorGUILayout.HelpBox(kHelp, MessageType.Info);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(value, new GUIContent(kGUID));
                    EditorGUI.EndDisabledGroup();
                }
            }
        }
    }
#endif
}