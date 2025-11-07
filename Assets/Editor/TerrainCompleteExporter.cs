using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TerrainCompleteExporter : EditorWindow
{
    public Terrain terrain;
    public string exportPath = "TerrainExport";
    
    [MenuItem("Tools/Complete Terrain Exporter")]
    static void Init()
    {
        TerrainCompleteExporter window = (TerrainCompleteExporter)EditorWindow.GetWindow(typeof(TerrainCompleteExporter));
        window.Show();
    }
    
    void OnGUI()
    {
        GUILayout.Label("Complete Terrain Exporter", EditorStyles.boldLabel);
        
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        exportPath = EditorGUILayout.TextField("Export Folder", exportPath);
        
        if (GUILayout.Button("Export Complete Terrain"))
        {
            if (terrain != null)
            {
                ExportCompleteTerrain();
            }
            else
            {
                Debug.LogError("Terrain이 선택되지 않았습니다!");
            }
        }
        
        EditorGUILayout.HelpBox("이 도구는 다음을 익스포트합니다:\n- 높이맵 (RAW)\n- 텍스처 레이어들\n- 스플랫맵\n- 메시 (OBJ)", MessageType.Info);
    }
    
    void ExportCompleteTerrain()
    {
        TerrainData terrainData = terrain.terrainData;
        
        // 폴더 생성
        string folderPath = "Assets/" + exportPath;
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", exportPath);
        }
        
        ExportHeightmap(terrainData, folderPath);
        ExportTextureLayers(terrainData, folderPath);
        ExportSplatmaps(terrainData, folderPath);
        ExportMesh(terrainData, folderPath);
        ExportDetailMaps(terrainData, folderPath);
        CreateConfigFile(terrainData, folderPath);
        
        AssetDatabase.Refresh();
    }
    
    void ExportHeightmap(TerrainData terrainData, string folderPath)
    {
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, width, height);
        
        string heightmapPath = folderPath + "/heightmap.raw";
        using (FileStream fs = new FileStream(heightmapPath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ushort heightValue = (ushort)(heights[y, x] * 65535);
                    writer.Write(heightValue);
                }
            }
        }
    }
    
    void ExportTextureLayers(TerrainData terrainData, string folderPath)
    {
        TerrainLayer[] layers = terrainData.terrainLayers;
        
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].diffuseTexture != null)
            {
                string texturePath = AssetDatabase.GetAssetPath(layers[i].diffuseTexture);
                string fileName = Path.GetFileName(texturePath);
                string destPath = folderPath + "/texture_" + i + "_" + fileName;
                
                AssetDatabase.CopyAsset(texturePath, destPath);
            }
        }
    }
    
    void ExportSplatmaps(TerrainData terrainData, string folderPath)
    {
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;
        int alphamapLayers = terrainData.alphamapLayers;
        
        float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        
        // 각 레이어별로 PNG로 저장
        for (int layer = 0; layer < alphamapLayers; layer++)
        {
            Texture2D splatTexture = new Texture2D(alphamapWidth, alphamapHeight, TextureFormat.RGB24, false);
            
            for (int y = 0; y < alphamapHeight; y++)
            {
                for (int x = 0; x < alphamapWidth; x++)
                {
                    float alpha = alphamaps[y, x, layer];
                    splatTexture.SetPixel(x, alphamapHeight - 1 - y, new Color(alpha, alpha, alpha, 1));
                }
            }
            
            splatTexture.Apply();
            
            byte[] pngData = splatTexture.EncodeToPNG();
            string splatPath = folderPath + "/splatmap_" + layer + ".png";
            File.WriteAllBytes(splatPath, pngData);
            
            DestroyImmediate(splatTexture);
        }
    }
    
    void ExportMesh(TerrainData terrainData, string folderPath)
    {
        int resolution = 513;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        
        string objPath = folderPath + "/terrain_mesh.obj";
        using (StreamWriter sw = new StreamWriter(objPath))
        {
            sw.WriteLine("# Unity Terrain Mesh");
            sw.WriteLine($"# Size: {terrainData.size.x} x {terrainData.size.z}");
            sw.WriteLine($"# Height: {terrainData.size.y}");
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float xPos = (float)x / (resolution - 1) * terrainData.size.x;
                    float zPos = (float)y / (resolution - 1) * terrainData.size.z;
                    float yPos = heights[y, x] * terrainData.size.y;
                    sw.WriteLine($"v {xPos} {yPos} {zPos}");
                }
            }
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / (resolution - 1);
                    float v = (float)y / (resolution - 1);
                    sw.WriteLine($"vt {u} {v}");
                }
            }
            
            for (int y = 0; y < resolution - 1; y++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int v1 = y * resolution + x + 1;
                    int v2 = v1 + 1;
                    int v3 = v1 + resolution;
                    int v4 = v3 + 1;
                    
                    sw.WriteLine($"f {v1}/{v1} {v3}/{v3} {v2}/{v2}");
                    sw.WriteLine($"f {v2}/{v2} {v3}/{v3} {v4}/{v4}");
                }
            }
        }
    }
    
    void CreateConfigFile(TerrainData terrainData, string folderPath)
    {
        string configPath = folderPath + "/terrain_config.txt";
        using (StreamWriter sw = new StreamWriter(configPath))
        {
            sw.WriteLine("# Unity Terrain Configuration");
            sw.WriteLine($"terrain_size_x={terrainData.size.x}");
            sw.WriteLine($"terrain_size_y={terrainData.size.y}");
            sw.WriteLine($"terrain_size_z={terrainData.size.z}");
            sw.WriteLine($"heightmap_resolution={terrainData.heightmapResolution}");
            sw.WriteLine($"alphamap_resolution={terrainData.alphamapWidth}");
            sw.WriteLine($"texture_layers={terrainData.alphamapLayers}");
            
            TerrainLayer[] layers = terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                {
                    sw.WriteLine($"layer_{i}_tiling_x={layers[i].tileSize.x}");
                    sw.WriteLine($"layer_{i}_tiling_y={layers[i].tileSize.y}");
                }
            }
        }
    }
    
    void ExportDetailMaps(TerrainData terrainData, string folderPath)
    {
        DetailPrototype[] detailPrototypes = terrainData.detailPrototypes;
        
        if (detailPrototypes.Length == 0) return;
        
        string detailInfoPath = folderPath + "/detail_info.txt";
        using (StreamWriter sw = new StreamWriter(detailInfoPath))
        {
            sw.WriteLine("# Detail Prototypes Info");
            sw.WriteLine($"detail_count={detailPrototypes.Length}");
            
            for (int i = 0; i < detailPrototypes.Length; i++)
            {
                DetailPrototype detail = detailPrototypes[i];
                sw.WriteLine($"detail_{i}_type={(detail.usePrototypeMesh ? "mesh" : "texture")}");
                
                if (detail.usePrototypeMesh && detail.prototype != null)
                {
                    sw.WriteLine($"detail_{i}_name={detail.prototype.name}");
                }
                else if (detail.prototypeTexture != null)
                {
                    sw.WriteLine($"detail_{i}_texture={detail.prototypeTexture.name}");
                    
                    string texturePath = AssetDatabase.GetAssetPath(detail.prototypeTexture);
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        string fileName = Path.GetFileName(texturePath);
                        string destPath = folderPath + "/detail_" + i + "_" + fileName;
                        AssetDatabase.CopyAsset(texturePath, destPath);
                    }
                }
                
                sw.WriteLine($"detail_{i}_density={detail.density}");
                sw.WriteLine($"detail_{i}_min_width={detail.minWidth}");
                sw.WriteLine($"detail_{i}_max_width={detail.maxWidth}");
                sw.WriteLine($"detail_{i}_min_height={detail.minHeight}");
                sw.WriteLine($"detail_{i}_max_height={detail.maxHeight}");
            }
        }
        
        for (int layer = 0; layer < detailPrototypes.Length; layer++)
        {
            int[,] detailMap = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, layer);
            
            Texture2D detailTexture = new Texture2D(terrainData.detailWidth, terrainData.detailHeight, TextureFormat.RGB24, false);
            
            for (int y = 0; y < terrainData.detailHeight; y++)
            {
                for (int x = 0; x < terrainData.detailWidth; x++)
                {
                    float density = Mathf.Clamp01(detailMap[y, x] / 16.0f);
                    detailTexture.SetPixel(x, terrainData.detailHeight - 1 - y, new Color(density, density, density, 1));
                }
            }
            
            detailTexture.Apply();
            
            byte[] pngData = detailTexture.EncodeToPNG();
            string detailMapPath = folderPath + "/detailmap_" + layer + ".png";
            File.WriteAllBytes(detailMapPath, pngData);
            
            DestroyImmediate(detailTexture);
        }
    }
}