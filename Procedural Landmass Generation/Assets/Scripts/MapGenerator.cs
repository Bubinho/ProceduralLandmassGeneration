using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour {

	public enum DrawMode {NoiseMap, ColorMap, Mesh, FalloffMap};
	public DrawMode drawMode;
	public Noise.NormalizeMode normalizeMode;
	public static int mapChunkSize = 241;
	[Range(0,6)]
	public int editorPreviewLOD;
	public float scale;
	public int octaves;
	[Range(0,1)]
	public float persistance;
	public float lacunarity;
	public int seed;
	public Vector2 offset;
	public float meshHeightMultiplier;
	public AnimationCurve animationCurve;

	public bool autoUpdate;
	public bool useFalloff;

	float [,] falloffMap;

	public TerrainType [] regions;

	Queue<MapThreadInfo<MapData>> mapDataInfoQueue = new Queue<MapThreadInfo<MapData>>();
	Queue<MapThreadInfo<MeshData>> meshDataInfoQueue = new Queue<MapThreadInfo<MeshData>>();


	void Awake(){
		falloffMap = FalloffGenerator.GenerateFalloffMap (mapChunkSize);
	}
	public void DrawMapInEditor(){

		MapData mapData = GenerateMapData (Vector2.zero);
		MapDisplay display = FindObjectOfType<MapDisplay> ();

		if (drawMode == DrawMode.NoiseMap) {
			display.DrawTexture (TextureGenerator.TextureFromHeightMap (mapData.heightMap));
		} else if (drawMode == DrawMode.ColorMap) {
			display.DrawTexture (TextureGenerator.TextureFromColorMap (mapData.colorMap, mapChunkSize, mapChunkSize));
		} else if (drawMode == DrawMode.Mesh) {
			display.DrawMesh (MeshGenerator.GenerateTerrainMesh (mapData.heightMap, meshHeightMultiplier, animationCurve, editorPreviewLOD), TextureGenerator.TextureFromColorMap (mapData.colorMap, mapChunkSize, mapChunkSize));
		} else if (drawMode == DrawMode.FalloffMap) {
			display.DrawTexture (TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
		}
	}

	public void RequestMapData(Vector2 center, Action<MapData> callback){
		ThreadStart threadStart = delegate {
			MapDataThread (center, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MapDataThread(Vector2 center, Action<MapData> callback){
		MapData mapData = GenerateMapData (center);
		lock (mapDataInfoQueue) {
			mapDataInfoQueue.Enqueue (new MapThreadInfo<MapData>(callback, mapData));
		}

	}

	public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback){
		ThreadStart threadStart = delegate {
			MeshDataThread (mapData, lod, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback){
		MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, animationCurve, lod);
		lock (meshDataInfoQueue) {
			meshDataInfoQueue.Enqueue (new MapThreadInfo<MeshData>(callback, meshData));
		}

	}

	void Update(){
		if (mapDataInfoQueue.Count > 0) {
			for(int i = 0; i < mapDataInfoQueue.Count; i++){
				MapThreadInfo<MapData> threadInfo = mapDataInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
		if (meshDataInfoQueue.Count > 0) {
			for(int i = 0; i < meshDataInfoQueue.Count; i++){
				MapThreadInfo<MeshData> threadInfo = meshDataInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
	}

	MapData GenerateMapData(Vector2 center){
		float[,] noiseMap = Noise.GenerateNoiseMap (mapChunkSize, mapChunkSize, seed, scale, octaves, persistance, lacunarity, center + offset, normalizeMode);

		//map colors to heightvalues
		Color[] colors = new Color[mapChunkSize * mapChunkSize];
		for (int y = 0; y < mapChunkSize; y++) {
			for (int x = 0; x < mapChunkSize; x++) {
				if (useFalloff) {
					noiseMap [x, y] = Mathf.Clamp01(noiseMap [x, y] -  falloffMap [x, y]);
				}
				float heightValue = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (heightValue >= regions [i].height) {
						colors [y * mapChunkSize + x] = regions [i].color;

					} else {
						break;
					}
				}
			}
		}
		return new MapData (noiseMap, colors);
	}

	void OnValidate()
	{
		if (lacunarity < 1) {
			lacunarity = 1;
		}
		if (octaves < 0) {
			octaves = 0;
		}

		falloffMap = FalloffGenerator.GenerateFalloffMap (mapChunkSize);
	}

	struct MapThreadInfo<T>{
		public readonly Action<T> callback;
		public readonly T parameter;

		public MapThreadInfo (Action<T> callback, T parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}
		
	}
}

[System.Serializable]
public struct TerrainType{
	public string name;
	public float height;
	public Color color;
}	

public struct MapData{
	public readonly float[,] heightMap;
	public readonly Color [] colorMap;

	public MapData (float[,] heightMap, Color[] colorMap)
	{
		this.heightMap = heightMap;
		this.colorMap = colorMap;
	}
}
	