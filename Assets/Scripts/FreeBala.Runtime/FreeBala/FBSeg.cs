using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBSeg
{
	public int id;

	public int kind;

	public int nl = 1;

	public int li;

	public int left = -1;

	public int right = -1;

	public int opp = -1;

	public int junction = -1;

	public bool one;

	public bool ring;

	public char turn = 'S';

	public Vector3[] p;

	public float[] s;

	public float len;

	public float w = 6f;

	public float vmax = 11f;

	public int[] next = new int[0];

	public int[] conf = new int[0];

	public float[] vcurv;

	public readonly List<int> prev = new List<int>();

	public Bounds box;

	public readonly List<object> occ = new List<object>();
}
}
