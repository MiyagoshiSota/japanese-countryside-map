using _Project.Scripts.Tanada;
using _Project.Scripts.Terrain.Generate;
using UnityEngine;

public class Main : MonoBehaviour
{
    [SerializeField] RandomUndulationMountain randomUndulationMountain;
    [SerializeField] HydraulicErosion hydraulicErosion;
    [SerializeField] RiverFinalizer riverFinalizer;
    [SerializeField] RoadGenerator roadGenerator;
    [SerializeField] ImageProcessor imageProcessor;
    [SerializeField] PlacementGenerator placementGenerator;
    [SerializeField] MapCombiner mapCombiner;
    [SerializeField] TerraceDeformer  terraceDeformer;
    [SerializeField] v2.ObjectPlacer objectPlacer;
    [SerializeField] HighElevationTreePlacer heightMapGenerator;
    [SerializeField] RiverMaskProcessor riverMaskProcessor;
    [SerializeField] DrawInstancedRice drawInstancedRice;
    
    [SerializeField] PanoramicScreenshotter screenshotter;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // randomUndulationMountain.Generate();
        //
        // hydraulicErosion.RunFullErosion();
        //
        // riverFinalizer.FinalizeRiverNetwork();
        //
        // roadGenerator.GenerateCostMap();
        // roadGenerator.GenerateRoadNetwork();
        // imageProcessor.ProcessImage();
        //
        // placementGenerator.GenerateSettlementMap();
        // placementGenerator.GenerateRoadsideMap();
        //
        // mapCombiner.CombineMaps();
        //
        // terraceDeformer.DeformAndGenerateMask();
        //
        // // objectPlacer.PlaceAllObjects();
        //
        // heightMapGenerator.PlaceTrees();
        //
        // riverMaskProcessor.ProcessAndSaveMaskedRiverMap();

        drawInstancedRice.GeneratePlacementData();

        // screenshotter.GeneratePanoramicShot();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
