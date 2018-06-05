using System.Collections;
using UnityEngine;

public static class TextureGenerator {
	public static Texture2D TextureFromColorMap(Color[] colors, int width, int height)
	{
		Texture2D texture = new Texture2D (width, height);
		texture.filterMode = FilterMode.Point;
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.SetPixels (colors);
		texture.Apply ();
		return texture;
	}

	public static Texture2D TextureFromHeightMap(float[,] heightMap)
	{
		int mapWidth = heightMap.GetLength (0);
		int mapHeight = heightMap.GetLength (1);
		Color[] colors = new Color[mapWidth * mapHeight];

		for(int y = 0; y < mapHeight; y++){
			for(int x=0; x < mapWidth; x++){
				colors [y * mapWidth + x] = Color.Lerp(Color.black, Color.white, heightMap[x, y]); 
			}
		}
		return TextureFromColorMap (colors, mapWidth, mapHeight);
	}

}
