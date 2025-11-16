using UnityEngine;
using UnityEditor;

public class TerrainResizer : EditorWindow
{
    public Terrain terrain;
    public Vector3 newSize = new Vector3(500, 50, 500);
    
    [MenuItem("Tools/Terrain Resizer")]
    static void Init()
    {
        TerrainResizer window = (TerrainResizer)EditorWindow.GetWindow(typeof(TerrainResizer));
        window.Show();
    }
    
    void OnGUI()
    {
        GUILayout.Label("Terrain Size Adjuster", EditorStyles.boldLabel);
        
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        
        if (terrain != null)
        {
            Vector3 currentSize = terrain.terrainData.size;
            EditorGUILayout.LabelField("Current Size", $"X: {currentSize.x}, Y: {currentSize.y}, Z: {currentSize.z}");
        }
        
        newSize = EditorGUILayout.Vector3Field("New Size", newSize);
        
        if (GUILayout.Button("Resize Terrain"))
        {
            if (terrain != null)
            {
                ResizeTerrain();
            }
            else
            {
                Debug.LogError("Terrain이 선택되지 않았습니다!");
            }
        }
        
        EditorGUILayout.HelpBox("주의: 크기를 변경하면 기존 디테일이 영향을 받을 수 있습니다.", MessageType.Warning);
    }
    
    void ResizeTerrain()
    {
        Undo.RecordObject(terrain.terrainData, "Resize Terrain");
        
        Vector3 oldSize = terrain.terrainData.size;
        terrain.terrainData.size = newSize;
        
        Debug.Log($"Terrain 크기 변경: {oldSize} → {newSize}");
        
        EditorUtility.SetDirty(terrain.terrainData);
    }
}