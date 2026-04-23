using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace LittlePhysics
{
    [CustomEditor(typeof(SpacialMapSettingsAuthoring))]
    [CanEditMultipleObjects]
    public class SpacialMapSettingsAuthoringEditor : Editor
    {
        private void OnSceneGUI()
        {
            SpacialMapSettingsAuthoring authoring = (SpacialMapSettingsAuthoring)target;

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(authoring.Position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(authoring, "Move Spacial Map Position");
                authoring.Position = newPosition;
            }

            drawGrid(authoring);
        }

        public override void OnInspectorGUI()
        {
            SpacialMapSettingsAuthoring authoring = (SpacialMapSettingsAuthoring)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            int cellCount = authoring.GridSize.x * authoring.GridSize.y * authoring.GridSize.z;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Total Cells Count", cellCount);
            EditorGUI.EndDisabledGroup();
        }

        private void drawGrid(SpacialMapSettingsAuthoring authoring)
        {
            float cellSize = authoring.CellWidth;
            Vector3 gridMin = authoring.Position;
            Vector3Int gridSize = new Vector3Int(authoring.GridSize.x, authoring.GridSize.y, authoring.GridSize.z);

            Vector3 totalSize = (Vector3)gridSize * cellSize;
            Vector3 gridCenter = gridMin + totalSize * 0.5f;

            Handles.color = Color.cyan;
            drawWireCube(gridCenter, totalSize.x, totalSize.y, totalSize.z);

            if (authoring.ShouldDrawCells)
            {
                Handles.color = Color.white;

                for (int x = 0; x < gridSize.x; x++)
                {
                    for (int y = 0; y < gridSize.y; y++)
                    {
                        for (int z = 0; z < gridSize.z; z++)
                        {
                            Vector3 cellMin = gridMin + new Vector3(x * cellSize, y * cellSize, z * cellSize);
                            Vector3 cellCenter = cellMin + new Vector3(cellSize * 0.5f, cellSize * 0.5f, cellSize * 0.5f);
                            drawWireCube(cellCenter, cellSize, cellSize, cellSize);
                        }
                    }
                }
            }
        }

        private void drawWireCube(Vector3 center, float width, float height, float depth)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float halfDepth = depth * 0.5f;

            Vector3 bottomFrontLeft  = center + new Vector3(-halfWidth, -halfHeight, -halfDepth);
            Vector3 bottomFrontRight = center + new Vector3( halfWidth, -halfHeight, -halfDepth);
            Vector3 bottomBackLeft   = center + new Vector3(-halfWidth, -halfHeight,  halfDepth);
            Vector3 bottomBackRight  = center + new Vector3( halfWidth, -halfHeight,  halfDepth);

            Vector3 topFrontLeft  = center + new Vector3(-halfWidth,  halfHeight, -halfDepth);
            Vector3 topFrontRight = center + new Vector3( halfWidth,  halfHeight, -halfDepth);
            Vector3 topBackLeft   = center + new Vector3(-halfWidth,  halfHeight,  halfDepth);
            Vector3 topBackRight  = center + new Vector3( halfWidth,  halfHeight,  halfDepth);

            Handles.DrawLine(bottomFrontLeft,  bottomFrontRight);
            Handles.DrawLine(bottomFrontRight, bottomBackRight);
            Handles.DrawLine(bottomBackRight,  bottomBackLeft);
            Handles.DrawLine(bottomBackLeft,   bottomFrontLeft);

            Handles.DrawLine(topFrontLeft,  topFrontRight);
            Handles.DrawLine(topFrontRight, topBackRight);
            Handles.DrawLine(topBackRight,  topBackLeft);
            Handles.DrawLine(topBackLeft,   topFrontLeft);

            Handles.DrawLine(bottomFrontLeft,  topFrontLeft);
            Handles.DrawLine(bottomFrontRight, topFrontRight);
            Handles.DrawLine(bottomBackLeft,   topBackLeft);
            Handles.DrawLine(bottomBackRight,  topBackRight);
        }
    }
}
