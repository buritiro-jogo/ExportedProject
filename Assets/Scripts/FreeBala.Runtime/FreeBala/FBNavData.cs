using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
public class FBNavData : MonoBehaviour
{
	public NavMeshData data;

	private NavMeshDataInstance h;

	private void OnEnable()
	{
		if ((bool)data)
		{
			h = NavMesh.AddNavMeshData(data);
		}
	}

	private void OnDisable()
	{
		if (h.valid)
		{
			h.Remove();
		}
	}
}
}
