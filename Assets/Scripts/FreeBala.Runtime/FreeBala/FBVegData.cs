using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBVegData : ScriptableObject
{
	[Serializable]
	public class Kind
	{
		public string name;

		public Mesh mesh;

		public Material mat;

		public bool shadows;
	}

	[Serializable]
	public class Cell
	{
		public Vector3 center;

		public float radius;

		public int kind;

		public Matrix4x4[] m;
	}

	public Kind[] kinds;

	public List<Cell> cells = new List<Cell>();

	public List<Vector3> openSpots = new List<Vector3>();

	public List<Vector3> edgeSpots = new List<Vector3>();

	public int total;

	public Texture2D grassMask;

	public Texture2D grassHeight;

	public Vector4 grassRect;

	public float grassY0;

	public Material grassMat;
}
}
