using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBMobileHudTests
    {
        [Test]
        public void Test_MobileLayout_DefaultsContainAllKeyButtons()
        {
            var defs = FBMobileLayout.GetDefaults();
            Assert.IsNotNull(defs);
            Assert.GreaterOrEqual(defs.Count, 16);

            string[] requiredIds = {
                "JOYSTICK", "FIRE", "FIRE_LEFT", "AIM", "JUMP",
                "CROUCH", "PRONE", "SPRINT", "RELOAD", "SWAP",
                "EMOTE", "HEAL", "PICKUP", "INTERACT", "MAP", "CAMERA"
            };

            foreach (var reqId in requiredIds)
            {
                var btn = defs.Find(b => b.id == reqId);
                Assert.IsNotNull(btn, $"Botão obrigatório ausente nos defaults: {reqId}");
                Assert.IsTrue(btn.enabled, $"Botão {reqId} deveria iniciar habilitado");
                Assert.Greater(btn.radius, 10f, $"Raio do botão {reqId} inválido");
                Assert.Greater(btn.scale, 0.4f, $"Escala do botão {reqId} inválida");
                Assert.Greater(btn.opacity, 0.1f, $"Opacidade do botão {reqId} inválida");
                Assert.Greater(btn.pos.x, 0, $"Posição X do botão {reqId} fora da tela");
                Assert.Greater(btn.pos.y, 0, $"Posição Y do botão {reqId} fora da tela");
            }
        }

        [Test]
        public void Test_MobileLayout_SaveAndLoadPersistence()
        {
            var list = FBMobileLayout.GetDefaults();
            var fire = list.Find(b => b.id == "FIRE");
            Assert.IsNotNull(fire);

            // Modifica valores
            fire.pos = new Vector2(1234.5f, 456.7f);
            fire.scale = 1.35f;
            fire.opacity = 0.88f;
            fire.enabled = false;

            // Salva
            FBMobileLayout.Save(list);

            // Recarrega
            var loaded = FBMobileLayout.Load();
            var loadedFire = loaded.Find(b => b.id == "FIRE");
            Assert.IsNotNull(loadedFire);

            Assert.AreEqual(1234.5f, loadedFire.pos.x, 0.5f);
            Assert.AreEqual(456.7f, loadedFire.pos.y, 0.5f);
            Assert.AreEqual(1.35f, loadedFire.scale, 0.05f);
            Assert.AreEqual(0.88f, loadedFire.opacity, 0.05f);
            Assert.IsFalse(loadedFire.enabled);

            // Restaura padrões para não afetar sessões reais
            FBMobileLayout.ResetToDefaults();
            var resetList = FBMobileLayout.Load();
            var resetFire = resetList.Find(b => b.id == "FIRE");
            Assert.IsTrue(resetFire.enabled);
        }

        [UnityTest]
        public IEnumerator Test_MobileHudEditor_OpenAndClose()
        {
            FBMobileHudEditor.Open();
            yield return null;

            Assert.IsTrue(FBMobileHudEditor.IsOpen);
            Assert.IsNotNull(FBMobileHudEditor.Instance);

            FBMobileHudEditor.Close();
            yield return null;

            Assert.IsFalse(FBMobileHudEditor.IsOpen);
        }
    }
}

