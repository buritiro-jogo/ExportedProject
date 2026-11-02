using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBVoiceAutoTest : MonoBehaviour
{
	private class WavRec : MonoBehaviour
	{
		private readonly List<float> buf = new List<float>();

		private readonly object lk = new object();

		private int chans = 2;

		private volatile bool on = true;

		private void OnAudioFilterRead(float[] data, int channels)
		{
			if (!on)
			{
				return;
			}
			lock (lk)
			{
				chans = channels;
				if (buf.Count < 23040000)
				{
					buf.AddRange(data);
				}
			}
		}

		public string Save(string path)
		{
			on = false;
			float[] array;
			int num;
			lock (lk)
			{
				array = buf.ToArray();
				num = chans;
			}
			int outputSampleRate = AudioSettings.outputSampleRate;
			float num2 = 0f;
			float[] array2 = array;
			foreach (float f in array2)
			{
				num2 = Mathf.Max(num2, Mathf.Abs(f));
			}
			using (BinaryWriter binaryWriter = new BinaryWriter(File.Create(path)))
			{
				int num3 = array.Length * 2;
				binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
				binaryWriter.Write(36 + num3);
				binaryWriter.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
				binaryWriter.Write(16);
				binaryWriter.Write((short)1);
				binaryWriter.Write((short)num);
				binaryWriter.Write(outputSampleRate);
				binaryWriter.Write(outputSampleRate * num * 2);
				binaryWriter.Write((short)(num * 2));
				binaryWriter.Write((short)16);
				binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
				binaryWriter.Write(num3);
				array2 = array;
				foreach (float num4 in array2)
				{
					binaryWriter.Write((short)Mathf.Clamp(Mathf.RoundToInt(num4 * 32767f), -32768, 32767));
				}
			}
			return $"{path} ({(float)(array.Length / Mathf.Max(1, num)) / (float)outputSampleRate:0.0} s, {num} canais, pico {num2:0.00})";
		}
	}

	private bool standalone;

	private string job = "vila_jardim:tatico";

	private string dir;

	private string section = "inicio";

	private StreamWriter csv;

	private WavRec rec;

	private float t0;

	private readonly Dictionary<string, int> played = new Dictionary<string, int>();

	private readonly Dictionary<string, int> suppressed = new Dictionary<string, int>();

	private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

	private const string MASC = "r_tutu";

	private const string FEM = "r_elaine";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbaudio");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_AutoTesteAudio");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBVoiceAutoTest fBVoiceAutoTest = obj.AddComponent<FBVoiceAutoTest>();
			fBVoiceAutoTest.standalone = Array.IndexOf(commandLineArgs, "-fbauto") < 0;
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBVoiceAutoTest.job = commandLineArgs[num + 1];
			}
		}
	}

	private IEnumerator Start()
	{
		if (standalone)
		{
			string[] array = job.Split(':');
			GameData.Load();
			FBSession.Load();
			FBSession.map = array[0];
			FBSession.mode = ((array.Length > 1) ? array[1] : "tatico");
			FBSession.online = false;
			FBSession.Save();
			AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + FBSession.map);
			if (op == null)
			{
				Debug.Log("[FreeBala][audio] FALHA: cena Map_" + FBSession.map + " não existe");
				Application.Quit(1);
				yield break;
			}
			while (!op.isDone)
			{
				yield return null;
			}
		}
		for (float w2 = 0f; w2 < 120f; w2 += Time.unscaledDeltaTime)
		{
			if ((bool)FBMatch.I && (bool)FBMatch.Player)
			{
				break;
			}
			yield return null;
		}
		if (!FBMatch.I || !FBMatch.Player)
		{
			Debug.Log("[FreeBala][audio] FALHA: partida não montou");
			yield break;
		}
		for (float w2 = 0f; w2 < 3f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Roteiro();
		if (standalone)
		{
			Debug.Log("[FreeBala][audio] fim");
			Application.Quit(0);
		}
	}

	private IEnumerator Roteiro()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		csv = new StreamWriter(Path.Combine(dir, "audio_log.csv"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		csv.WriteLine("tempo_s,secao,linha,personagem,sexo,dano_tipo,dano,vida,banco,decisao,clipe,volume,pitch,intensidade,distancia_m,vozes_ativas");
		t0 = Time.time;
		FBVoice.trace = Row;
		FBMatch M = FBMatch.I;
		FBCharacter P = FBMatch.Player;
		bool freeze = M.freeze;
		bool god = FBMatch.God;
		M.freeze = true;
		FBMatch.God = true;
		FBAudio.StopAllLoops();
		FBAmbience.Stop();
		AudioListener audioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
		if ((bool)audioListener)
		{
			rec = audioListener.gameObject.AddComponent<WavRec>();
		}
		Debug.Log(string.Format("[FreeBala][audio] roteiro iniciado · ouvinte {0} · {1} Hz", audioListener ? audioListener.name : "NENHUM", AudioSettings.outputSampleRate));
		Transform transform = (Camera.main ? Camera.main.transform : P.transform);
		Vector3 fw = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
		if (fw.sqrMagnitude < 0.1f)
		{
			fw = P.transform.forward;
		}
		Vector3 rt = Vector3.Cross(Vector3.up, fw);
		Vector3 basePos = P.Pos + fw * 4f;
		FBCharacter i = M.Spawn("r_tutu", "", P.team, Ground(basePos - rt * 1.4f), player: false);
		FBCharacter f = M.Spawn("r_elaine", "", P.team, Ground(basePos + rt * 1.4f), player: false);
		if (!i || !f)
		{
			Debug.Log("[FreeBala][audio] FALHA: bonecos r_tutu/r_elaine não nasceram");
			yield break;
		}
		FBCharacter[] array = new FBCharacter[2] { i, f };
		foreach (FBCharacter obj in array)
		{
			obj.transform.rotation = Quaternion.LookRotation(-fw);
			obj.displayName = ((obj == i) ? "TESTE_MASC" : "TESTE_FEM");
		}
		yield return Wait(1.5f);
		Debug.Log("[FreeBala][audio] sexo dos bonecos: r_tutu=" + FBVoice.SexOf(i) + " r_elaine=" + FBVoice.SexOf(f));
		(FBCharacter, string)[] array2 = new(FBCharacter, string)[2]
		{
			(i, "masc"),
			(f, "fem")
		};
		for (int k = 0; k < array2.Length; k++)
		{
			var (c, tag) = array2[k];
			Sec("tiro_leve_" + tag);
			Ready(c);
			Shot(c, 8f, "body");
			yield return Wait(1.4f);
			Sec("tiro_grave_" + tag);
			Shot(c, 40f, "body");
			yield return Wait(1.6f);
			Sec("tiro_cabeca_" + tag);
			Ready(c);
			Shot(c, 22f, "head", head: true);
			yield return Wait(1.4f);
			Sec("tiro_membro_" + tag);
			Ready(c);
			Shot(c, 6f, "leg");
			yield return Wait(1.4f);
			Sec("pouca_vida_" + tag);
			Ready(c, 30f);
			Shot(c, 6f, "body");
			yield return Wait(1.8f);
		}
		Sec("rajada_auto_masc");
		Ready(i);
		for (int k = 0; k < 12; k++)
		{
			if (i.hp > 10f)
			{
				Shot(i, 7f, (k % 4 == 3) ? "arm" : "body");
			}
			yield return Wait(0.1f);
		}
		yield return Wait(1.5f);
		Sec("simultaneo_fem");
		Ready(f);
		Shot(f, 10f, "body");
		FBCombat.DamageActor(f, 30f, head: false, null, "explosion");
		FBCombat.DamageActor(f, 5f, head: false, null, "fall");
		yield return Wait(1.8f);
		Sec("queda_masc");
		Ready(i);
		FBCombat.DamageActor(i, 12f, head: false, null, "fall");
		yield return Wait(1.6f);
		Sec("queda_grave_masc");
		FBCombat.DamageActor(i, 35f, head: false, null, "fall");
		yield return Wait(1.8f);
		Sec("explosao_fem");
		Ready(f);
		FBCombat.DamageActor(f, 45f, head: false, null, "explosion");
		yield return Wait(2f);
		Sec("explosao_masc");
		Ready(i);
		FBCombat.DamageActor(i, 45f, head: false, null, "explosion");
		yield return Wait(2f);
		Sec("gas_entrada_masc_fem");
		Ready(i);
		Ready(f);
		for (int k = 0; k < 15; k++)
		{
			array = new FBCharacter[2] { i, f };
			for (int j = 0; j < array.Length; j++)
			{
				FBCombat.DamageActor(array[j], 3f, head: false, null, "zone");
			}
			yield return Wait(1f);
		}
		Sec("gas_saida_masc_fem");
		yield return Wait(11f);
		Sec("morte_no_gas_fem");
		Ready(f, 9f);
		for (int k = 0; k < 12; k++)
		{
			if (!f.alive)
			{
				break;
			}
			FBCombat.DamageActor(f, 3f, head: false, null, "zone");
			yield return Wait(1f);
		}
		yield return Wait(4f);
		Sec("morte_tiro_masc");
		Ready(i, 20f);
		Shot(i, 30f, "body");
		yield return Wait(0.5f);
		if (i.alive)
		{
			Shot(i, 30f, "body");
		}
		yield return Wait(4f);
		FBCharacter walker = M.Spawn("r_tutu", "", P.team, Ground(basePos + fw * 3f), player: false);
		if ((bool)walker)
		{
			yield return Wait(1f);
			yield return Passos(walker, P, fw, rt);
			FBMatch.All.Remove(walker);
			UnityEngine.Object.Destroy(walker.gameObject);
		}
		Sec("fim");
		FBVoice.trace = null;
		FBFootsteps.trace = null;
		M.freeze = freeze;
		FBMatch.God = god;
		array = new FBCharacter[2] { i, f };
		foreach (FBCharacter fBCharacter in array)
		{
			if ((bool)fBCharacter)
			{
				FBMatch.All.Remove(fBCharacter);
				UnityEngine.Object.Destroy(fBCharacter.gameObject);
			}
		}
		string text = "sem ouvinte";
		if ((bool)rec)
		{
			text = rec.Save(Path.Combine(dir, "audio_roteiro.wav"));
			UnityEngine.Object.Destroy(rec);
		}
		csv.Close();
		Debug.Log(string.Format("[FreeBala][audio] roteiro ok ({0:0} s) · tocadas: {1} · suprimidas: {2} · wav: {3} · csv: {4}", Time.time - t0, Sum(played), Sum(suppressed), text, Path.Combine(dir, "audio_log.csv")));
		static string Sum(Dictionary<string, int> d)
		{
			return string.Join(" ", from x in d
				orderby x.Key
				select $"{x.Key}={x.Value}");
		}
	}

	private IEnumerator Passos(FBCharacter c, FBCharacter P, Vector3 fw, Vector3 rt)
	{
		FBFootsteps.trace = delegate(FBFootsteps.Trace r)
		{
			csv.WriteLine(string.Join(",", T(), section, "passo", r.ch ? r.ch.skinId : "", "", r.mode, "", "", r.surf, (r.foot == 0) ? "pe_esq" : "pe_dir", r.clip, r.vol.ToString("0.000", IC), "", r.speed.ToString("0.00", IC), r.dist.ToString("0.00", IC), ""));
		};
		Ready(c);
		List<GameObject> slabs = new List<GameObject>();
		(string, int)[] pisos = new(string, int)[3]
		{
			("Asfalto_rua_teste", 0),
			("Grama_teste", 1),
			("Chapa_teste", 4)
		};
		Vector3 o = Ground(P.Pos + fw * 7f - rt * 4f);
		float y = o.y + 0.06f;
		for (int j = 0; j < pisos.Length; j++)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.name = pisos[j].Item1;
			gameObject.layer = 8;
			slabs.Add(gameObject);
			gameObject.transform.position = new Vector3(o.x, y - 0.05f, o.z) + fw * ((float)j * 2.4f) + rt * 4f;
			gameObject.transform.rotation = Quaternion.LookRotation(rt);
			gameObject.transform.localScale = new Vector3(2f, 0.1f, 17f);
		}
		Physics.SyncTransforms();
		for (int i = 0; i < pisos.Length; i++)
		{
			(float, string)[] array = new(float, string)[2]
			{
				(1.6f, "andar"),
				(4.6f, "correr")
			};
			for (int k = 0; k < array.Length; k++)
			{
				var (spd, text) = array[k];
				Sec("passos_" + text + "_" + pisos[i].Item1.Split('_')[0].ToLowerInvariant());
				Vector3 a = new Vector3(o.x, y, o.z) + fw * ((float)i * 2.4f);
				Vector3 b = a + rt * 16f;
				c.transform.position = a;
				c.transform.rotation = Quaternion.LookRotation(rt);
				yield return null;
				yield return null;
				for (float d = 0f; d < 16f; d += spd * Time.deltaTime)
				{
					c.sprint = spd > 4f;
					c.grounded = true;
					c.velocity = rt * spd;
					c.transform.position = Vector3.Lerp(a, b, d / 16f);
					yield return null;
				}
				c.velocity = Vector3.zero;
				c.sprint = false;
				yield return Wait(0.6f);
			}
		}
		Sec("passos_pouso");
		c.transform.position = new Vector3(o.x, y + 1.5f, o.z);
		c.grounded = false;
		yield return Wait(0.6f);
		c.transform.position = new Vector3(o.x, y, o.z);
		c.grounded = true;
		yield return Wait(1f);
		foreach (GameObject item in slabs)
		{
			UnityEngine.Object.Destroy(item);
		}
	}

	private void Sec(string s)
	{
		section = s;
		csv.WriteLine(T() + "," + s + ",secao,,,,,,,,,,,,,");
		csv.Flush();
	}

	private string T()
	{
		return (Time.time - t0).ToString("0.000", IC);
	}

	private static IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.deltaTime)
		{
			yield return null;
		}
	}

	private static Vector3 Ground(Vector3 p)
	{
		if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hitInfo, 10f, FBWorld.SolidMask))
		{
			return p;
		}
		return hitInfo.point;
	}

	private static void Ready(FBCharacter c, float hp = 100f)
	{
		if (!c.alive || c.knocked)
		{
			c.Revive(hp);
		}
		c.hp = hp;
		c.armor = 0f;
		c.helmet = false;
		c.protUntil = 0f;
		c.knockInvT = 0f;
	}

	private static void Shot(FBCharacter c, float dmg, string part, bool head = false)
	{
		Vector3 point = c.Pos + Vector3.up * (head ? 1.65f : ((part == "leg") ? 0.5f : 1.2f)) * c.ScaleK;
		FBCombat.DamageActor(c, dmg, head, null, null, new FBHitInfo
		{
			valid = true,
			point = point,
			normal = c.transform.forward,
			dir = -c.transform.forward,
			part = part,
			weaponId = "autoteste",
			dist = 4f
		});
	}

	private void Row(FBVoice.Trace r)
	{
		bool flag = r.kind == "voz";
		bool flag2 = flag && r.decision.StartsWith("tocou");
		csv.WriteLine(string.Join(",", T(), section, r.kind, r.ch ? r.ch.skinId : "", r.sex, Q(r.type), flag ? "" : F(r.amount, "0.0"), F(r.hp, "0"), r.bank, Q(r.decision), r.clip, flag2 ? F(r.vol, "0.000") : "", flag2 ? F(r.pitch, "0.000") : "", flag2 ? F(r.intensity) : "", F(r.dist), r.voices.ToString()));
		if (flag)
		{
			Dictionary<string, int> obj = (flag2 ? played : suppressed);
			obj.TryGetValue(r.bank, out var value);
			obj[r.bank] = value + 1;
		}
		static string F(float v, string fmt = "0.00")
		{
			return v.ToString(fmt, IC);
		}
		static string Q(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return "\"" + s.Replace("\"", "'") + "\"";
			}
			return "";
		}
	}
}
}
