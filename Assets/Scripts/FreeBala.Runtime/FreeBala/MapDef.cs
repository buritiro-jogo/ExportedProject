using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class MapDef
{
	public string id;

	public string name;

	public bool disabled;

	public Dictionary<string, object> level;

	public List<Vector3> spawnA = new List<Vector3>();

	public List<Vector3> spawnB = new List<Vector3>();

	public List<Vector3> bots = new List<Vector3>();

	public List<Vector3> points = new List<Vector3>();

	public List<Vector3> waypoints = new List<Vector3>();
}
}
