using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace LittlePhysics
{
    public class PhysicsDebugEditorWindow : EditorWindow
    {
        private BodyType body1Type = BodyType.Dynamic;
        private BodyType body2Type = BodyType.Dynamic;

        [MenuItem("LittlePhysics/Physics Debug")]
        public static void ShowWindow()
        {
            var window = GetWindow<PhysicsDebugEditorWindow>("Physics Debug");
            window.minSize = new Vector2(260f, 120f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += onEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= onEditorUpdate;
        }

        private void onEditorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            GUILayout.Label("Collisions", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            body1Type = (BodyType)EditorGUILayout.EnumPopup("Body 1 Type", body1Type);
            body2Type = (BodyType)EditorGUILayout.EnumPopup("Body 2 Type", body2Type);

            EditorGUILayout.Space(8f);

            var result = readDebugComponent();

            if (!result.HasValue)
            {
                EditorGUILayout.HelpBox(
                    "No PhysicsCollisionEditorDebugAuthoring found in the active world.",
                    MessageType.Info);
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Collision Count", result.Value);
            EditorGUI.EndDisabledGroup();
        }

        private int? readDebugComponent()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return null;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<PhysicsCollisionEditorDebugComponent>());

            if (query.IsEmpty)
            {
                return null;
            }

            var entity = query.GetSingletonEntity();
            var component = entityManager.GetComponentData<PhysicsCollisionEditorDebugComponent>(entity);

            if (component.Body1Filter != body1Type || component.Body2Filter != body2Type)
            {
                component.Body1Filter = body1Type;
                component.Body2Filter = body2Type;
                entityManager.SetComponentData(entity, component);
            }

            return component.CollisionCount;
        }
    }
}
