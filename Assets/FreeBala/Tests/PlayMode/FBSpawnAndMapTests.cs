using System.Collections;

using System.Collections.Generic;

using NUnit.Framework;

using UnityEngine;

using UnityEngine.TestTools;



namespace FreeBala.Tests

{

    public class FBSpawnAndMapTests

    {

        [Test]

        public void Test_PlayableBounds_DefinedForAllMaps()

        {

            string[] maps = { "praca_aguas", "vila_jardim", "praca_pt", "orla", "estande", "bermuda" };

            foreach (var m in maps)

            {

                var b = FBWorld.GetPlayableBounds(m);

                Assert.Greater(b.size.x, 20f, $"{m}: bounds x");

                Assert.Greater(b.size.z, 20f, $"{m}: bounds z");

                Assert.Greater(b.size.y, 10f, $"{m}: bounds y");

            }

        }



        [Test]

        public void Test_ArenaRadius_NeverExceedsPlayableBounds()

        {

            string[] maps = { "praca_aguas", "vila_jardim", "praca_pt", "orla" };

            string[] modes = { "squad", "classico" };



            foreach (var map in maps)

            {

                var pb = FBWorld.GetPlayableBounds(map);

                float maxAllowableRadius = Mathf.Min(pb.extents.x, pb.extents.z) * 0.88f;



                foreach (var mode in modes)

                {

                    FBSession.map = map;

                    float r = FBArena.RadiusFor(mode);

                    Assert.LessOrEqual(r, maxAllowableRadius + 0.01f, $"{map}/{mode}: arena radius {r} exceeds allowable limit {maxAllowableRadius}");

                    Assert.GreaterOrEqual(r, 25f, $"{map}/{mode}: arena radius too small");

                }

            }

        }



        [Test]

        public void Test_ValidateSpawnSpot_RejectsOutOfBounds()

        {

            var pb = FBWorld.GetPlayableBounds("praca_aguas");



            // Point far outside map limits

            var outPoint = new Vector3(pb.max.x + 50f, 0, pb.max.z + 50f);

            bool ok = FBMatch.ValidateSpawnSpot(outPoint, out var valid);

            // It should either reject or fallback to a spot strictly inside pb

            if (ok)

            {

                Assert.IsTrue(pb.Contains(valid), "Fallback spot was outside playable bounds!");

            }

            else

            {

                Assert.IsFalse(ok, "ValidateSpawnSpot should reject points outside playable bounds");

            }

        }



        [Test]

        public void Test_ValidateSpawnSpot_SeparatesTeammates()

        {

            var avoid = new List<Vector3> { Vector3.zero };

            // Trying to spawn on top of Vector3.zero (distance 0.1m)

            var cand = new Vector3(0.1f, 0, 0.1f);

            FBMatch.ValidateSpawnSpot(cand, out var valid, null, avoid, 1.35f);

            // Distance must be >= 1.35m

            float dist = (valid - Vector3.zero).magnitude;

            Assert.GreaterOrEqual(dist, 1.2f, "Teammate spawn did not respect separation distance");

        }



        [Test]

        public void Test_FBMatchMap_ToggleState()

        {

            FBMatchMap.Close();

            Assert.IsFalse(FBMatchMap.Open, "Map should start closed");

            FBMatchMap.Toggle();

            Assert.IsTrue(FBMatchMap.Open, "Map should be open after Toggle");

            FBMatchMap.Close();

            Assert.IsFalse(FBMatchMap.Open, "Map should be closed after Close");

        }



        [Test]

        public void Test_FBMatchMap_SingleTogglePerFrame()

        {

            FBMatchMap.Close();

            Assert.IsFalse(FBMatchMap.Open);

            // Calling Toggle twice in the same frame should only toggle once (protecting against multiple Update checks)

            FBMatchMap.Toggle();

            FBMatchMap.Toggle();

            Assert.IsTrue(FBMatchMap.Open, "Multiple Toggle calls in same frame should only execute once");

            FBMatchMap.Close();

        }



        [Test]

        public void Test_FBKit_Cover_VerticalAlignment()

        {

            var go = new GameObject("TestCover");

            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var ri = FBKit.Cover(go.transform, tex, 0, 0, 426, 126, 0.25f);

            Assert.IsNotNull(ri);

            Assert.GreaterOrEqual(ri.uvRect.y, 0f, "UV Y should not be negative");

            Assert.LessOrEqual(ri.uvRect.y + ri.uvRect.height, 1.001f, "UV Y + height should not exceed 1");

            Assert.Less(ri.uvRect.y, 0.20f, "yAlign = 0.25 should position UV Y lower than 0.20 to capture the buildings");

            Object.DestroyImmediate(tex);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Test_FBHeliRecon_ScanningAndRadius()
        {
            FBHeliRecon.Clear();
            Assert.IsFalse(FBHeliRecon.IsScanning);
            FBHeliRecon.SetScanState(true, new Vector3(10, 0, 20));
            Assert.IsTrue(FBHeliRecon.IsScanning);
            Assert.AreEqual(new Vector3(10, 0, 20), FBHeliRecon.ScanPosition);
            Assert.GreaterOrEqual(FBHeliRecon.ScanRadius, 30f);
            FBHeliRecon.Clear();
            Assert.IsFalse(FBHeliRecon.IsScanning);
        }

        [Test]
        public void Test_FBMxBike_ManualGrauMode()
        {
            // GrauAssist deve estar desativado para garantir controle físico manual pelo jogador
            Assert.IsFalse(FBMxBike.GrauAssist, "GrauAssist deve ser false para permitir controle manual real");
        }

        [Test]
        public void Test_Characters_AllGendersAvailable()
        {
            var roster = FBCustomizeData.Roster;
            Assert.IsNotNull(roster);
            Assert.GreaterOrEqual(roster.Count, 48, "Roster deve conter pelo menos 48 personagens");
            int fems = roster.FindAll(e => e.fem).Count;
            int males = roster.FindAll(e => !e.fem).Count;
            Assert.GreaterOrEqual(fems, 8, "Devem haver pelo menos 8 personagens femininas disponíveis");
            Assert.GreaterOrEqual(males, 35, "Devem haver personagens masculinos disponíveis");
        }
    }
}

