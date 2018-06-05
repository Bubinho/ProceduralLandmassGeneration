using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {

	const float scale = 1f;
	const float viewerMoveThresholdForChunkUpdate = 25.0f;
	const float sqrviewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
	public LODInfo[] detailLevels;
	public static float maxViewDist;
	public Transform viewer;
	public static Vector2 viewerPos;
	Vector2 viewerPosOld;
	public Material mapMaterial;
	static MapGenerator mapGenerator;

	int chunkSize;
	int chunksVisibleInViewDist;

	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>(); // this is needed to save displayed chunks and set them to invisble otherwise they will not disappear

	// Use this for initialization
	void Start () {
		mapGenerator = FindObjectOfType<MapGenerator> ();

		maxViewDist = detailLevels [detailLevels.Length - 1].visibleDistThreshold;
		chunkSize = MapGenerator.mapChunkSize - 1;
		chunksVisibleInViewDist = Mathf.RoundToInt (maxViewDist/chunkSize);
		UpdateVisibleChunks ();
	}
	
	// Update is called once per frame
	void Update () {
		viewerPos = new Vector2 (viewer.position.x, viewer.position.z) / scale;
		if ((viewerPosOld - viewerPos).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate) {
			viewerPosOld = viewerPos;
			UpdateVisibleChunks ();
		}

	}

	void UpdateVisibleChunks(){

		for (int chunk = 0; chunk < terrainChunksVisibleLastUpdate.Count; chunk++) {
			terrainChunksVisibleLastUpdate [chunk].SetVisible (false);
		}
		terrainChunksVisibleLastUpdate.Clear ();

		int currentChunkCoordX = Mathf.RoundToInt (viewerPos.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt (viewerPos.y / chunkSize);

		for (int yOffset = -chunksVisibleInViewDist; yOffset <= chunksVisibleInViewDist; yOffset++) {
			for (int xOffset = -chunksVisibleInViewDist; xOffset <= chunksVisibleInViewDist; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2 (currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (terrainChunkDictionary.ContainsKey (viewedChunkCoord)) {
					//Update existing chuknk
					terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
				} else {
					
					// create new chunk
					terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, this.transform, mapMaterial));
				}
			}
		}
	}


	public class TerrainChunk{

		GameObject meshObject;
		Vector2 position;
		Bounds bounds;
		MeshRenderer meshRenderer;
		MeshFilter meshFilter;
		LODInfo [] detailLevels;
		LODMesh [] lodMeshes;

		MapData mapData;
		bool mapDataReceived;
		int previousLODIndex = -1;

		public TerrainChunk(Vector2 coord, int size, LODInfo [] detailLevels, Transform parent, Material material){
			this.detailLevels = detailLevels; 
			position = coord * size;
			bounds = new Bounds(position, Vector2.one * size);
			Vector3 positionV3 = new Vector3(position.x, 0, position.y);
			meshObject = new GameObject("Terrain Chunk" + coord.x + coord.y);
			meshRenderer = meshObject.AddComponent<MeshRenderer>();
			meshRenderer.material = material;
			meshFilter = meshObject.AddComponent<MeshFilter>();
			meshObject.transform.position = positionV3 * scale;
			meshObject.transform.localScale = Vector3.one * scale;
			meshObject.transform.parent = parent;
			SetVisible(false);

			// create a mesh for each lod
			lodMeshes = new LODMesh[detailLevels.Length];
			for(int i = 0; i < detailLevels.Length; i++){
				lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
			}

			mapGenerator.RequestMapData(position, OnMapDataReceived);
		}

		void OnMapDataReceived(MapData mapData){
			this.mapData = mapData;
			mapDataReceived = true;
			Texture2D texture = TextureGenerator.TextureFromColorMap (mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
			meshRenderer.material.mainTexture = texture;
			UpdateTerrainChunk ();
		}

		public void UpdateTerrainChunk(){
			if (mapDataReceived) {
				float viewerDistFromNearestEdge = Mathf.Sqrt (bounds.SqrDistance(viewerPos));
				bool visible = viewerDistFromNearestEdge <= maxViewDist;

				if (visible) {
					int lodIndex = 0;
					for (int i = 0; i < detailLevels.Length - 1; i++) { // dont have to look at last one, because bool visible would then be false
						if (viewerDistFromNearestEdge > detailLevels [i].visibleDistThreshold) {
							lodIndex = i + 1;
						} else {
							break;
						}
					}

					if (lodIndex != previousLODIndex) {
						LODMesh lodMesh = lodMeshes [lodIndex];
						if (lodMesh.hasMesh) {
							previousLODIndex = lodIndex;
							meshFilter.mesh = lodMesh.mesh;

						} else if (!lodMesh.hasRequestedMesh) {
							lodMesh.RequestMesh (mapData);
						}
					}
					terrainChunksVisibleLastUpdate.Add (this);
				}
				SetVisible (visible);
			}
		}

		public void SetVisible(bool visible)
		{
			meshObject.SetActive (visible);
		}

		public bool isVisible(){
			return meshObject.activeSelf;
		}
	}

	class LODMesh{
		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;
		int lod;
		System.Action updateCallback;

		public LODMesh(int lod, System.Action updateCallback){
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		void OnMeshDataReceived(MeshData meshData){
			mesh = meshData.CreateMesh ();
			hasMesh = true;
			updateCallback ();
		}

		public void RequestMesh(MapData mapData){
			hasRequestedMesh = true;
			mapGenerator.RequestMeshData (mapData, lod, OnMeshDataReceived);
		}
	}

	[System.Serializable]
	public struct LODInfo{
		public int lod;
		public float visibleDistThreshold;
	}
}
