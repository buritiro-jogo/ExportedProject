using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public abstract class FBMode
{
	protected FBMatch G;

	public string name;

	public bool respawn;

	public bool rounds;

	public bool econ;

	public bool ffa;

	public bool br;

	public bool ffDamage;

	public bool arsenal;

	public bool role;

	public float autoRespawn;

	protected static readonly Color BLUE = new Color(0.18f, 0.48f, 1f);

	protected static readonly Color RED = new Color(0.95f, 0.23f, 0.27f);

	protected static readonly Color GOLD = new Color(0.96f, 0.67f, 0.09f);

	protected static readonly Color DIM = new Color(1f, 1f, 1f, 0.16f);

	protected static readonly Color GREEN = new Color(0.2f, 0.8f, 0.35f);

	public virtual List<Vector4> Points => null;

	public virtual FBZone.Z Zone => null;

	public virtual int Round => 1;

	protected FBMode(FBMatch g)
	{
		G = g;
	}

	public virtual void Setup()
	{
	}

	public virtual void Update(float dt)
	{
	}

	public virtual void Hud()
	{
	}

	public virtual void OnKill(FBCharacter victim, FBCharacter killer, bool head)
	{
	}

	public virtual Vector3? Goal(FBCharacter b)
	{
		return null;
	}

	public virtual bool Urgent(FBCharacter b)
	{
		return false;
	}

	public virtual Vector3? UrgentGoal(FBCharacter b)
	{
		return null;
	}

	public virtual Vector3 SpawnPoint()
	{
		if (G.teamLine[FBSession.team] == null)
		{
			return G.TeamSpawns(FBSession.team)[0];
		}
		return G.teamLine[FBSession.team].spots[0];
	}

	public virtual void Dispose()
	{
	}

	public virtual (FBTarget tg, float t, bool head)? TargetRay(Vector3 o, Vector3 d, float max)
	{
		return null;
	}

	protected static string Mmss(float t)
	{
		int num = Mathf.Max(0, Mathf.CeilToInt(t));
		return $"{num / 60}:{num % 60:00}";
	}

	protected IEnumerable<FBCharacter> Alive(int t)
	{
		return from a in FBMatch.Actors()
			where a.alive && a.team == t
			select a;
	}
}
}
