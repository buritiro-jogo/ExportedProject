// FARINHA × BURITI — porte 1:1 das regras puras do src/farinhaburiti.js (objetivo do modo Polícia × Ladrão). Testado em
// Tests/EditMode/FBFarinhaBuritiTests.cs com os casos de tests/farinhaburiti.test.mjs.
// Equipe 0 (FARINHA, polícia) captura o saco de farinha (perto da base do Buriti); equipe 1 (BURITI, ladrões) captura o cacho de buriti
// (perto da base da Farinha). Levar até o ponto de entrega da própria base; caiu = item no chão (o lado que defende devolve; some em 20 s).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBFarinhaBuriti
    {
        public const float PICK = 2.4f, DELIVER = 4, DROP_T = 20;
        public static readonly string[] TEAMS = { "FARINHA", "BURITI" }, ITEMS = { "FARINHA", "BURITI" }, ART = { "A FARINHA", "O BURITI" };
        public interface IActor { Vector3 Pos { get; } int Team { get; } bool Alive { get; } bool Knocked { get; } }
        public class Item { public int team; public Vector3 home, pos; public string st = "home"; public IActor carrier; public float dropT; }
        public class Board { public Vector3[] zones; public Item[] items; }
        public struct Ev { public string t; public Item item; public IActor actor; }
        static float XZ(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public static Board Create(Vector3[] homes, Vector3[] zones) => new Board { zones = (Vector3[])zones.Clone(), items = new[] { new Item { team = 0, home = homes[0], pos = homes[0] }, new Item { team = 1, home = homes[1], pos = homes[1] } } };
        public static void Reset(Board B) { foreach (var it in B.items) { it.pos = it.home; it.st = "home"; it.carrier = null; it.dropT = 0; } }
        public static List<Ev> Step(Board B, IEnumerable<IActor> actors, float dt)
        {
            var ev = new List<Ev>(); var list = new List<IActor>(actors);
            foreach (var it in B.items)
            {
                if (it.st == "carried")
                {
                    if (it.carrier == null || !it.carrier.Alive || it.carrier.Knocked) { Drop(B, it.carrier, ev); continue; }
                    it.pos = it.carrier.Pos; if (XZ(it.pos, B.zones[it.team]) < DELIVER) ev.Add(new Ev { t = "deliver", item = it }); continue;
                }
                IActor Near(int team, float ymax = 2.5f) => list.Find(a => a.Alive && !a.Knocked && a.Team == team && new Vector2(a.Pos.x - it.pos.x, a.Pos.z - it.pos.z).magnitude < PICK && Mathf.Abs(a.Pos.y - it.pos.y) < ymax);
                if (it.st == "dropped")
                {
                    var def = Near(1 - it.team);
                    if (def != null) { it.pos = it.home; it.st = "home"; it.dropT = 0; ev.Add(new Ev { t = "return", item = it, actor = def }); continue; }
                    it.dropT -= dt; if (it.dropT <= 0) { it.pos = it.home; it.st = "home"; ev.Add(new Ev { t = "expire", item = it }); continue; }
                }
                var thief = Near(it.team);
                if (thief != null) { it.st = "carried"; it.carrier = thief; it.dropT = 0; ev.Add(new Ev { t = "pickup", item = it, actor = thief }); }
            }
            return ev;
        }
        public static void Drop(Board B, IActor actor, List<Ev> ev)
        {
            foreach (var it in B.items) if (it.st == "carried" && (actor == null || it.carrier == actor))
                { var p = actor != null ? actor.Pos : it.pos; it.st = "dropped"; it.carrier = null; it.pos = p; it.dropT = DROP_T; ev?.Add(new Ev { t = "drop", item = it, actor = actor }); }
        }
        public static float Progress(Board B, Item it) { float d0 = Mathf.Max(1, XZ(it.home, B.zones[it.team])); return Mathf.Min(1, XZ(it.pos, B.zones[it.team]) / d0); }
        public static int TimeoutWinner(Board B, int[] alive)
        {
            float a = Progress(B, B.items[0]), b = Progress(B, B.items[1]);
            if (Mathf.Abs(a - b) >= .05f) return a < b ? 0 : 1;
            if (alive[0] != alive[1]) return alive[0] > alive[1] ? 0 : 1; return 0;
        }
        public static (Vector3? goal, bool urgent) BotGoal(Board B, IActor actor, Func<float> rnd = null)
        {
            rnd ??= () => UnityEngine.Random.value; var mine = B.items[actor.Team]; var foe = B.items[1 - actor.Team];
            if (mine.carrier == actor) return (B.zones[actor.Team], true);
            if (foe.st == "dropped") return (foe.pos, false);
            if (foe.st == "carried") return (rnd() < .7f ? foe.pos : (Vector3?)null, true);
            if (mine.st == "dropped") return (mine.pos, false);
            if (mine.st == "carried") return (rnd() < .6f ? mine.pos : (Vector3?)null, false);
            float r = rnd(); return (r < .5f ? mine.home : r < .75f ? foe.home : (Vector3?)null, false);
        }
    }
}
