using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
[Serializable]
public class FBTuneData
{
	public bool hasPaint;

	public bool hasVidro;

	public bool hasFarol;

	public bool hasRodaCor;

	public bool neon;

	public bool turbo;

	public bool hasCaixa;

	public bool hasFalante;

	public bool hasCorneta;

	public Color paint = Color.white;

	public Color vidro = new Color(0.043f, 0.055f, 0.07f);

	public Color farol = Color.white;

	public Color neonC = new Color(0f, 0.9f, 1f);

	public Color rodaCor = Color.white;

	public Color caixa;

	public Color falante;

	public Color corneta;

	public float metal = 0.3f;

	public float brilho = 0.55f;

	public float fume = 0.62f;

	public float wsize = 1f;

	public float fh;

	public float foff;

	public float fcam;

	public float rh;

	public float roff;

	public float rcam;

	public string skin = "";

	public string wheel = "";

	public string ronco = "";

	public string tracao = "";

	public string placa = "";

	public List<FBSomSlot> som = new List<FBSomSlot>();

	public bool somSet;

	public int[] bk = new int[5] { -1, -1, -1, -1, -1 };

	public int forroF;

	public int forroT;

	public int bandeira;

	public int pneu = -1;

	public bool engate;

	public bool bandL;

	public bool bandR;
}
}
