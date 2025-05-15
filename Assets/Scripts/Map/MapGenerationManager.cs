using UnityEngine;
using System.Collections;

public class MapGenerationManager : MonoBehaviour
{
    [Header("Required Components")]
    public BaseMapManager baseMapManager;
    public TileManager tileManager;
    public ObjectManager objectManager;
    public RoadManager roadManager;

    private void Start()
    {
        // Ensure ObjectManager is assigned to BaseMapManager
        baseMapManager.objectManager = objectManager;
        StartCoroutine(GenerateMapSequence());
    }

    private IEnumerator GenerateMapSequence()
    {
        Debug.Log("Starting map generation sequence...");

        // Step 1: Basic map setup and generation
        baseMapManager.ValidateDependencies();
        baseMapManager.InitializeRandomization();
        baseMapManager.GenerateBaseMap();

        Debug.Log("Base map generated");
        yield return new WaitForSeconds(0.1f);

        // Step 2: Place houses
        Debug.Log("Placing houses...");
        objectManager.PlaceHouses();
        yield return new WaitForSeconds(0.5f); // Increased wait time for house placement

        // Verify houses were placed
        var houseCount = objectManager.GetHouseFrontPositions()?.Count ?? 0;
        Debug.Log($"Houses placed: {houseCount}");

        // Step 3: Detect regions (now that houses are placed)
        Debug.Log("Detecting regions...");
        baseMapManager.DetectRegions();
        yield return new WaitForSeconds(0.5f); // Increased wait time for region detection

        // Step 4: Enhance terrain with variants
        Debug.Log("Enhancing terrain...");
        tileManager.EnhanceTerrain();
        yield return new WaitForSeconds(0.3f);

        // Step 5: Build roads between houses
        Debug.Log("Building roads...");
        roadManager.enabled = true;
        yield return new WaitForSeconds(0.3f);

        // Step 6: Place remaining objects (trees, grass, etc.)
        Debug.Log("Placing vegetation and objects...");
        objectManager.PlaceRemainingObjects();

        Debug.Log("Map generation sequence completed!");
    }
}
