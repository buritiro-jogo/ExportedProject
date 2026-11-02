using UnityEngine;

namespace FreeBala
{
public class FBRebFlag : MonoBehaviour
{
	public FBCar car;

	public Vector3 pivotLocal;

	private Transform pivot;

	private float ph;

	private void Start()
	{
		pivot = new GameObject("piv_bandeira").transform;
		pivot.SetParent(base.transform.parent, worldPositionStays: false);
		pivot.localPosition = pivotLocal;
		pivot.gameObject.layer = base.gameObject.layer;
		base.transform.SetParent(pivot, worldPositionStays: true);
		ph = Random.value * 10f;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBRebFlag.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		if ((bool)pivot)
		{
			float num = (car ? Mathf.Abs(car.v) : 0f);
			ph += Time.deltaTime * (2.5f + num * 0.25f);
			float y = Mathf.Sin(ph) * (4f + Mathf.Min(10f, num * 0.4f)) + Mathf.Sin(ph * 2.3f) * 2f;
			pivot.localRotation = Quaternion.Euler(0f, y, 0f);
		}
	}
}
}
