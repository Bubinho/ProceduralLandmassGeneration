﻿using System.Collections;
using UnityEngine;

public static class MeshGenerator {
	public static MeshData GenerateTerrainMesh(float [,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
	{
		AnimationCurve heightCurve = new AnimationCurve (_heightCurve.keys);

		int meshSimplificationIncrement = 1;//(levelOfDetail == 0)?1:levelOfDetail * 2;

		int borderedSize = heightMap.GetLength (0);
		int meshSize = borderedSize - 2 * meshSimplificationIncrement;
		int meshSizeUnsimplified = borderedSize - 2;

		float topLeftX = ( meshSizeUnsimplified - 1) * -0.5f;
		float topLeftZ = ( meshSizeUnsimplified - 1) * 0.5f;

		int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1; ////// borderedSize

		MeshData meshData = new MeshData (verticesPerLine);

		// this is for correct coloring between chunks
		int[,] vertexIndexMap = new int[borderedSize, borderedSize];
		int borderVertexIndex = -1;
		int meshVertexIndex = 0;

		for (int y = 0; y < borderedSize; y += meshSimplificationIncrement) {
			for (int x = 0; x < borderedSize; x += meshSimplificationIncrement) {
				bool isBorderIndex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;
				if (isBorderIndex) {
					vertexIndexMap [x, y] = borderVertexIndex;
					borderVertexIndex--;
				} else {
					vertexIndexMap [x, y] = meshVertexIndex;
					meshVertexIndex++;
				}
			}
		}

		for (int y = 0; y < borderedSize; y += meshSimplificationIncrement) {
			for (int x = 0; x < borderedSize; x += meshSimplificationIncrement) {
				int vertexIndex = vertexIndexMap [x, y];

				Vector2 percent = new Vector2 ((x-meshSimplificationIncrement) / (float)meshSize, (y - meshSimplificationIncrement) / (float)meshSize);
				float height = heightCurve.Evaluate (heightMap [x, y]) * heightMultiplier;
				Vector3 vertexPosition = new Vector3 (topLeftX + percent.x *  meshSizeUnsimplified, height, topLeftZ - percent.y *  meshSizeUnsimplified);

				meshData.AddVertex (vertexPosition, percent, vertexIndex);

				if (x < borderedSize - 1 && y < borderedSize - 1) 
				{
					int a = vertexIndexMap [x, y];
					int b = vertexIndexMap [x + meshSimplificationIncrement, y];
					int c = vertexIndexMap [x, y + meshSimplificationIncrement];
					int d = vertexIndexMap [x + meshSimplificationIncrement, y + meshSimplificationIncrement];

					meshData.AddTriangle (a, d, c);
					meshData.AddTriangle (d, a, b);
				}
				vertexIndex++;
			}
		}
		return meshData;
	}
}

public class MeshData{
	Vector3 [] vertices;
	int [] triangles;
	Vector2[] uvs;

	Vector3[] borderVertices;
	int[] borderTriangles;

	int triangleIndex;
	int borderTriangleIndex;

	public MeshData(int verticesPerLine)
	{
		vertices = new Vector3[verticesPerLine * verticesPerLine];
		uvs = new Vector2[verticesPerLine * verticesPerLine];
		triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

		borderVertices = new Vector3[verticesPerLine * 4 + 4];
		borderTriangles = new int[6 * 4 * verticesPerLine];
	}

	public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex){
		if (vertexIndex < 0) {
			borderVertices [-vertexIndex - 1] = vertexPosition;
		}else{
			vertices[vertexIndex] = vertexPosition;
			uvs [vertexIndex] = uv;
		}
	}

	public void AddTriangle(int a, int b, int c){
		if (a < 0 || b < 0 || c < 0) {
			borderTriangles [borderTriangleIndex] = a;
			borderTriangles [borderTriangleIndex + 1] = b;
			borderTriangles [borderTriangleIndex + 2] = c;
			borderTriangleIndex += 3;
		} else {
			triangles [triangleIndex] = a;
			triangles [triangleIndex + 1] = b;
			triangles [triangleIndex + 2] = c;
			triangleIndex += 3;
		}
	}

	Vector3[] RecalculateNormals(){
		Vector3[] vertexNormals = new Vector3[vertices.Length];
		int triangleCount = triangles.Length / 3;
		for (int i = 0; i < triangleCount; i++) {
			int normalTriangleIndex = i * 3;
			int vertexIndexA = triangles [normalTriangleIndex];
			int vertexIndexB = triangles [normalTriangleIndex + 1];
			int vertexIndexC = triangles [normalTriangleIndex + 2];

			Vector3 triangleNormal = SurfaceNormalFromIndices (vertexIndexA, vertexIndexB, vertexIndexC);
			vertexNormals [vertexIndexA] += triangleNormal;
			vertexNormals [vertexIndexB] += triangleNormal;
			vertexNormals [vertexIndexC] += triangleNormal;
		}

		int borderTriangleCount = borderTriangles.Length / 3;
		for (int i = 0; i < borderTriangleCount; i++) {
			int normalTriangleIndex = i * 3;
			int vertexIndexA = borderTriangles [normalTriangleIndex];
			int vertexIndexB = borderTriangles [normalTriangleIndex + 1];
			int vertexIndexC = borderTriangles [normalTriangleIndex + 2];

			Vector3 triangleNormal = SurfaceNormalFromIndices (vertexIndexA, vertexIndexB, vertexIndexC);
			if (vertexIndexA >= 0) {
				vertexNormals [vertexIndexA] += triangleNormal;
			}
			if (vertexIndexB >= 0) {
				vertexNormals [vertexIndexB] += triangleNormal;
			}if (vertexIndexC >= 0) {
				vertexNormals [vertexIndexC] += triangleNormal;
			}

		}

		for (int i = 0; i < vertexNormals.Length; i++) {
			vertexNormals [i].Normalize ();
		}

		return vertexNormals;

	}

	Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
	{
		Vector3 pointA = (indexA < 0)?borderVertices[-indexA -1] : vertices[indexA];
		Vector3 pointB = (indexB < 0)?borderVertices[-indexB -1] : vertices[indexB];
		Vector3 pointC = (indexC< 0)?borderVertices[-indexC -1] : vertices[indexC];

		Vector3 lineAB = pointB - pointA;
		Vector3 lineAC = pointC - pointA;

		return Vector3.Cross (lineAB, lineAC).normalized;
	}

	public Mesh CreateMesh(){
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.normals = RecalculateNormals ();
		return mesh;
	}
}
