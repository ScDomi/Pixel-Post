using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using System.Linq;

public class SceneExporter : MonoBehaviour
{
    [System.Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Vector3 localPosition;
        public Vector3 rotation;
        public Vector3 localScale;
    }

    [System.Serializable]
    public class SpriteRendererData
    {
        public string sortingLayerName;
        public int sortingOrder;
        public string spritePath;
        public Color color;
        public bool flipX;
        public bool flipY;
    }

    [System.Serializable]
    public class GameObjectData
    {
        public string name;
        public string tag;
        public string layer;
        public string parent;
        public string prefabType;
        public bool isActive;
        public TransformData transform;
        public SpriteRendererData spriteRenderer;
        public List<string> components = new List<string>();
        public Dictionary<string, object> componentData = new Dictionary<string, object>();
    }

    [System.Serializable]
    public class PrefabInfo
    {
        public string name;
        public string path;
        public List<string> components = new List<string>();
    }

    [System.Serializable]
    public class AssetFolderInfo
    {
        public string name;
        public string path;
        public List<string> files = new List<string>();
        public List<AssetFolderInfo> subfolders = new List<AssetFolderInfo>();
    }

    [System.Serializable]
    public class SceneData
    {
        public List<GameObjectData> objects = new List<GameObjectData>();
        public List<PrefabInfo> prefabs = new List<PrefabInfo>();
        public AssetFolderInfo assetStructure;
    }

    [MenuItem("Tools/Export Scene Data")]
    public static void ExportSceneData()
    {
        SceneData sceneData = new SceneData
        {
            objects = CollectGameObjectData(),
            prefabs = CollectPrefabData(),
            assetStructure = CollectAssetStructure()
        };

        string json = JsonUtility.ToJson(sceneData, true);
        string path = Application.dataPath + "/SceneExport.json";
        File.WriteAllText(path, json);
        Debug.Log("Scene and project data exported to: " + path);
        AssetDatabase.Refresh();
    }

    private static List<GameObjectData> CollectGameObjectData()
    {
        List<GameObjectData> gameObjectList = new List<GameObjectData>();

        foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
        {
            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                continue;

            GameObjectData data = new GameObjectData
            {
                name = obj.name,
                tag = obj.tag,
                layer = LayerMask.LayerToName(obj.layer),
                parent = obj.transform.parent != null ? obj.transform.parent.name : "None",
                prefabType = GetPrefabType(obj),
                isActive = obj.activeSelf,
                transform = new TransformData
                {
                    position = obj.transform.position,
                    localPosition = obj.transform.localPosition,
                    rotation = obj.transform.eulerAngles,
                    localScale = obj.transform.localScale
                }
            };

            // Erfassen der SpriteRenderer-Informationen
            var spriteRenderer = obj.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                data.spriteRenderer = new SpriteRendererData
                {
                    sortingLayerName = spriteRenderer.sortingLayerName,
                    sortingOrder = spriteRenderer.sortingOrder,
                    spritePath = spriteRenderer.sprite != null ? AssetDatabase.GetAssetPath(spriteRenderer.sprite) : null,
                    color = spriteRenderer.color,
                    flipX = spriteRenderer.flipX,
                    flipY = spriteRenderer.flipY
                };
            }

            // Erfassen aller Komponenten und ihrer Eigenschaften
            foreach (Component comp in obj.GetComponents<Component>())
            {
                if (comp == null) continue;
                
                data.components.Add(comp.GetType().Name);
                
                // Spezielle Behandlung für verschiedene Komponententypen
                if (comp is Tilemap tilemap)
                {
                    var tilemapData = new Dictionary<string, object>
                    {
                        {"tileAnchor", tilemap.tileAnchor},
                        {"orientation", tilemap.orientation.ToString()},
                        {"cellSize", tilemap.cellSize},
                        {"cellGap", tilemap.cellGap},
                        {"animationFrameRate", tilemap.animationFrameRate}
                    };
                    data.componentData[comp.GetType().Name] = tilemapData;
                }
                else if (comp is Renderer renderer)
                {
                    var rendererData = new Dictionary<string, object>
                    {
                        {"sortingLayerID", renderer.sortingLayerID},
                        {"sortingLayerName", renderer.sortingLayerName},
                        {"sortingOrder", renderer.sortingOrder},
                        {"allowOcclusionWhenDynamic", renderer.allowOcclusionWhenDynamic},
                        {"enabled", renderer.enabled}
                    };
                    data.componentData[comp.GetType().Name] = rendererData;
                }
                
                // Erfassen der MonoBehaviour public fields
                if (comp is MonoBehaviour)
                {
                    var serializedObject = new SerializedObject(comp);
                    var iterator = serializedObject.GetIterator();
                    bool enterChildren = true;
                    
                    Dictionary<string, object> fieldValues = new Dictionary<string, object>();
                    
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.name != "m_Script")
                        {
                            switch (iterator.propertyType)
                            {
                                case SerializedPropertyType.Integer:
                                    fieldValues[iterator.name] = iterator.intValue;
                                    break;
                                case SerializedPropertyType.Float:
                                    fieldValues[iterator.name] = iterator.floatValue;
                                    break;
                                case SerializedPropertyType.String:
                                    fieldValues[iterator.name] = iterator.stringValue;
                                    break;
                                case SerializedPropertyType.Boolean:
                                    fieldValues[iterator.name] = iterator.boolValue;
                                    break;
                                case SerializedPropertyType.Vector2:
                                    fieldValues[iterator.name] = new Vector2(iterator.vector2Value.x, iterator.vector2Value.y);
                                    break;
                                case SerializedPropertyType.Vector3:
                                    fieldValues[iterator.name] = new Vector3(iterator.vector3Value.x, iterator.vector3Value.y, iterator.vector3Value.z);
                                    break;
                                case SerializedPropertyType.ObjectReference:
                                    if (iterator.objectReferenceValue != null)
                                    {
                                        fieldValues[iterator.name] = AssetDatabase.GetAssetPath(iterator.objectReferenceValue);
                                    }
                                    break;
                            }
                        }
                    }
                    
                    if (fieldValues.Count > 0)
                    {
                        data.componentData[comp.GetType().Name + "_fields"] = fieldValues;
                    }
                }
            }

            gameObjectList.Add(data);
        }

        return gameObjectList;
    }

    private static List<PrefabInfo> CollectPrefabData()
    {
        List<PrefabInfo> prefabList = new List<PrefabInfo>();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                PrefabInfo info = new PrefabInfo
                {
                    name = prefab.name,
                    path = path,
                    components = prefab.GetComponents<Component>()
                                     .Select(c => c.GetType().Name)
                                     .ToList()
                };
                prefabList.Add(info);
            }
        }

        return prefabList;
    }

    private static AssetFolderInfo CollectAssetStructure()
    {
        return ScanDirectory(Application.dataPath, 0);
    }

    private static AssetFolderInfo ScanDirectory(string path, int depth)
    {
        if (depth > 2) // Limit depth to prevent too large structure
        {
            return null;
        }

        AssetFolderInfo folder = new AssetFolderInfo
        {
            name = Path.GetFileName(path),
            path = path.Replace(Application.dataPath, "Assets"),
            files = new List<string>(),
            subfolders = new List<AssetFolderInfo>()
        };

        // Get files
        try
        {
            string[] files = Directory.GetFiles(path)
                                    .Where(f => !f.EndsWith(".meta"))
                                    .Select(f => Path.GetFileName(f))
                                    .ToArray();
            folder.files.AddRange(files);

            // Get subdirectories
            string[] subdirs = Directory.GetDirectories(path);
            foreach (string subdir in subdirs)
            {
                if (Path.GetFileName(subdir).StartsWith(".")) continue;
                
                var subfolder = ScanDirectory(subdir, depth + 1);
                if (subfolder != null)
                {
                    folder.subfolders.Add(subfolder);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error scanning directory {path}: {e.Message}");
        }

        return folder;
    }

    private static string GetPrefabType(GameObject obj)
    {
#if UNITY_EDITOR
        PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(obj);
        PrefabInstanceStatus instanceStatus = PrefabUtility.GetPrefabInstanceStatus(obj);

        if (instanceStatus == PrefabInstanceStatus.NotAPrefab)
            return "Scene Object";
        if (instanceStatus == PrefabInstanceStatus.MissingAsset)
            return "Missing Prefab";
        if (instanceStatus == PrefabInstanceStatus.Connected)
            return "Prefab Instance";
        if (instanceStatus == PrefabInstanceStatus.Disconnected)
            return "Disconnected Prefab";
        if (assetType == PrefabAssetType.Variant)
            return "Prefab Variant";
        if (assetType == PrefabAssetType.Regular)
            return "Prefab";

        return "Unknown";
#else
        return "Runtime Object";
#endif
    }
}