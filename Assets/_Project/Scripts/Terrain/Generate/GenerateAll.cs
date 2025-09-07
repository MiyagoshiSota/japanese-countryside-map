using _Project.Scripts.Terrain.Generate;
using UnityEngine;

public class GenerateAll : MonoBehaviour
{
    [SerializeField] private RandomUndulationMountain randomUndulationMountain;
    [SerializeField] private ClusteredZoneAnalyzer clusteredZoneAnalyzer;
    [SerializeField] private RoadAndBridgeMaskGenerator roadGenerator;
    [SerializeField] private SettlementPlacerWithRiver settlementPlacer;
    [SerializeField] private RicePaddyPlacer ricePaddyPlacer;
    [SerializeField] private HighElevationTreePlacer highElevationTreePlacer;
    [SerializeField] private MaskCombiner maskCombiner;
    
    [ContextMenu("Generate all")]
    void Generate()
    {
        randomUndulationMountain.Generate();
        clusteredZoneAnalyzer.AnalyzeAndGenerateMasks();
        maskCombiner.CombineMasks();
        roadGenerator.GenerateRoadMask();
        settlementPlacer.PlaceSettlements();
        ricePaddyPlacer.GeneratePaddies();
        highElevationTreePlacer.PlaceTrees();
    }
}
