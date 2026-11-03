using System;
using System.Collections.Generic;
using FreeBala;
using UnityEngine;

// Ferramenta independente: NÃO declara FBCharacter nem modifica os scripts do jogo.
namespace FreeBalaWeaponAdjustSafe
{
    public enum AdjustLocation { Holster, InHand }
    // Valores 0..3 preservados para manter o banco .asset existente compatível.
    public enum AdjustPose
    {
        Hold = 0, Aim = 1, Fire = 2, Reload = 3,
        Walk = 4, Run = 5, Crouch = 6,
        WalkAim = 7, WalkFire = 8, CrouchWalk = 9,
        CrouchAim = 10, CrouchFire = 11
    }

    [CreateAssetMenu(fileName = "FBWeaponAdjustSafeDatabase", menuName = "FreeBala/Calibracao segura de armas")]
    public sealed class FBWeaponAdjustSafeDatabase : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string skinId = "*";
            public string weaponId = "";
            public AdjustLocation location;
            public AdjustPose pose;
            public Vector3 localPositionOffset;
            public Quaternion localRotationOffset = Quaternion.identity;
            // Multiplicador relativo a escala original do modelo (1,1,1 = sem ajuste).
            // Registros antigos podem ser desserializados como (0,0,0): tratamos isso como identidade.
            public Vector3 localScaleMultiplier = Vector3.one;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        // Procura um perfil exato, privilegiando a skin antes do perfil global.
        private bool TryFindExact(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, out Entry found)
        {
            found = null;
            string skin = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.skinId == skin && e.weaponId == weaponId && e.location == location && e.pose == pose)
                { found = e; return true; }
            }
            if (skin != "*")
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e != null && e.skinId == "*" && e.weaponId == weaponId && e.location == location && e.pose == pose)
                    { found = e; return true; }
                }
            }
            return false;
        }

        public bool TryFind(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, out Entry found)
        {
            found = null;
            if (string.IsNullOrEmpty(weaponId)) return false;
            if (TryFindExact(skinId, weaponId, location, pose, out found)) return true;
            // Perfil especifico SEMPRE vence a heranca. Perfil Hold original continua intacto.
            if (location != AdjustLocation.InHand || pose == AdjustPose.Hold) return false;
            switch (pose)
            {
                case AdjustPose.WalkAim:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Aim, out found)) return true;
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found)) return true;
                    break;
                case AdjustPose.WalkFire:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Fire, out found)) return true;
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found)) return true;
                    break;
                case AdjustPose.CrouchAim:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Aim, out found)) return true;
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found)) return true;
                    break;
                case AdjustPose.CrouchFire:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Fire, out found)) return true;
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found)) return true;
                    break;
                case AdjustPose.CrouchWalk:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Crouch, out found)) return true;
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found)) return true;
                    break;
                case AdjustPose.Run:
                    if (TryFindExact(skinId, weaponId, location, AdjustPose.Walk, out found)) return true;
                    break;
            }
            return TryFindExact(skinId, weaponId, location, AdjustPose.Hold, out found);
        }

        // Mantem assinatura antiga caso outras ferramentas a utilizem.
        public void Set(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, Vector3 pos, Quaternion rot)
        {
            // Compatibilidade com interfaces anteriores: nao apaga uma escala ja calibrada.
            Vector3 multiplier = Vector3.one;
            string skin = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            foreach (var e in entries)
                if (e != null && e.skinId == skin && e.weaponId == weaponId && e.location == location && e.pose == pose)
                { multiplier = SafeMultiplier(e.localScaleMultiplier); break; }
            Set(skinId, weaponId, location, pose, pos, rot, multiplier);
        }

        public void Set(string skinId, string weaponId, AdjustLocation location, AdjustPose pose, Vector3 pos, Quaternion rot, Vector3 scaleMultiplier)
        {
            string skin = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            Entry target = null;
            foreach (var e in entries)
            {
                if (e != null && e.skinId == skin && e.weaponId == weaponId && e.location == location && e.pose == pose)
                { target = e; break; }
            }
            if (target == null)
            {
                target = new Entry { skinId = skin, weaponId = weaponId, location = location, pose = pose };
                entries.Add(target);
            }
            target.localPositionOffset = pos;
            target.localRotationOffset = Quaternion.Normalize(rot);
            target.localScaleMultiplier = SafeMultiplier(scaleMultiplier);
        }

        // 0 em todos os eixos indica dado antigo sem escala serializada.
        public static Vector3 SafeMultiplier(Vector3 v)
        {
            if (v == Vector3.zero) return Vector3.one;
            float S(float x) => float.IsNaN(x) || float.IsInfinity(x) || x <= 0.0001f
                ? 1f : Mathf.Clamp(x, 0.01f, 100f);
            return new Vector3(S(v.x), S(v.y), S(v.z));
        }

        public bool Remove(string skinId, string weaponId, AdjustLocation location, AdjustPose pose)
        {
            string skin = string.IsNullOrEmpty(skinId) ? "*" : skinId;
            return entries.RemoveAll(e => e != null && e.skinId == skin && e.weaponId == weaponId && e.location == location && e.pose == pose) > 0;
        }
    }

    // LateUpdate do FBCharacter usa a ordem padrão. Este componente roda APÓS ele.
    [DefaultExecutionOrder(30000)]
    public sealed class FBWeaponAdjustSafeRunner : MonoBehaviour
    {
        public struct BasePose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            // Ultimo resultado aplicado: permite recuperar a escala-base da arma
            // mesmo quando nenhum outro script restaura a escala na proxima frame.
            public Vector3 appliedScale;
            public bool hasAppliedScale;
            public int frame;
        }

        private static FBWeaponAdjustSafeRunner instance;
        // Desligar aqui desativa apenas as correcoes visuais; nao interfere na logica principal.
        public static bool CalibrationEnabled { get; set; } = true;
        // Ajustes de COLDRE do banco foram calibrados sobre a posicao antiga das armas guardadas (longe do corpo, dependente da direcao do
        // personagem); somados a posicao nova eles empurram a arma para dentro do corpo. Desligado por padrao; os dados continuam no .asset.
        public static bool ApplyHolsterAdjust { get; set; } = false;
        private const string DatabaseResource = "FBWeaponAdjustSafeDatabase";
        private FBWeaponAdjustSafeDatabase database;
        private readonly List<FBCharacter> characters = new List<FBCharacter>();
        private readonly Dictionary<Transform, BasePose> bases = new Dictionary<Transform, BasePose>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance) return;
            var go = new GameObject("FBWeaponAdjustSafeRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FBWeaponAdjustSafeRunner>();
        }

        private void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        public static bool TryGetBase(Transform target, out BasePose pose)
        {
            pose = default(BasePose);
            return instance && target && instance.bases.TryGetValue(target, out pose);
        }

        // Estados da gameplay, sem ler nome de clipe nem alterar rig do Mixamo.
        // Ordem de prioridade: recarga > disparo > mira > agachado > corrida > andar > descanso.
        public static AdjustPose CurrentPose(FBCharacter character)
        {
            if (!character || !character.isActiveAndEnabled) return AdjustPose.Hold;
            float speed = new Vector2(character.velocity.x, character.velocity.z).magnitude;
            if (character.animVel.HasValue)
                speed = Mathf.Max(speed, character.animVel.Value.magnitude);
            // Uma velocidade nao finita nao deve trocar perfis sem previsibilidade.
            if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0f;
            bool moving = speed > 0.20f;
            bool crouching = character.crouch;
            if (character.reloading) return AdjustPose.Reload;
            if (character.shooting || character.lastShot < 0.16f)
            {
                if (crouching) return AdjustPose.CrouchFire;
                return moving ? AdjustPose.WalkFire : AdjustPose.Fire;
            }
            if (character.aiming)
            {
                if (crouching) return AdjustPose.CrouchAim;
                return moving ? AdjustPose.WalkAim : AdjustPose.Aim;
            }
            if (crouching) return moving ? AdjustPose.CrouchWalk : AdjustPose.Crouch;
            if (moving && character.sprint) return AdjustPose.Run;
            return moving ? AdjustPose.Walk : AdjustPose.Hold;
        }

        private void RefreshCharacters()
        {
            characters.Clear();
            characters.AddRange(FindObjectsByType<FBCharacter>(FindObjectsSortMode.None));
        }

        private void LateUpdate()
        {
            if (!CalibrationEnabled)
            {
                // Remove o multiplicador visível quando o usuário desliga o calibrador.
                foreach (var item in bases)
                    if (item.Key && item.Value.hasAppliedScale &&
                        NearlySameScale(item.Key.localScale, item.Value.appliedScale))
                        item.Key.localScale = item.Value.scale;
                bases.Clear();
                return;
            }
            if (!database) database = Resources.Load<FBWeaponAdjustSafeDatabase>(DatabaseResource);
            if (Time.frameCount % 25 == 0 || characters.Count == 0) RefreshCharacters();
            // Limpa apenas objetos destruidos. Limpar tudo de tempos em tempos
            // causaria acumulacao da escala em armas equipadas ainda existentes.
            if (Time.frameCount % 600 == 0)
            {
                var expired = new List<Transform>();
                foreach (var item in bases) if (!item.Key) expired.Add(item.Key);
                foreach (var dead in expired) bases.Remove(dead);
            }
            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                // Personagens ainda nao inicializados nao devem participar da calibracao.
                if (!character || !character.isActiveAndEnabled || !character.gameObject.activeInHierarchy) continue;
                if (!character.an || !character.an.isInitialized) continue;
                var equipped = character.weaponGO;
                if (equipped && equipped.activeInHierarchy && character.weapon != null && character.dance == null)
                {
                    string equippedId = character.weapon.id;
                    if (!string.IsNullOrEmpty(equippedId))
                        Apply(character, equipped.transform, equippedId, AdjustLocation.InHand, CurrentPose(character));
                }

                // Mantem coldres existentes e a protecao de colisao do jogo.
                var holsters = character.HolsterObjects;
                if (holsters == null) continue;
                foreach (var holster in holsters)
                {
                    if (!holster || !holster.activeInHierarchy || !holster.name.StartsWith("Coldre_", StringComparison.Ordinal)) continue;
                    string weaponId = holster.name.Substring("Coldre_".Length);
                    if (!string.IsNullOrEmpty(weaponId))
                        Apply(character, holster.transform, weaponId, AdjustLocation.Holster, AdjustPose.Hold);
                }
            }
        }

        private static bool NearlySameScale(Vector3 a, Vector3 b)
        {
            bool Near(float x, float y) =>
                Mathf.Abs(x - y) <= 0.000001f + 0.0001f * Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            return Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
        }

        private void Apply(FBCharacter character, Transform target, string weaponId, AdjustLocation location, AdjustPose pose)
        {
            if (!target || !character) return;

            // O coldre recebe a escala-base em LateUpdate do FBCharacter, mas a arma
            // na mao pode conservar a escala que aplicamos na frame anterior.
            // Recuperar a base anterior evita multiplicacao acumulada (0.9, 0.81, ...).
            var rawScale = target.localScale;
            if (bases.TryGetValue(target, out var previous) && previous.hasAppliedScale &&
                NearlySameScale(rawScale, previous.appliedScale))
                rawScale = previous.scale;

            var snapshot = new BasePose
            {
                position = target.localPosition,
                rotation = target.localRotation,
                scale = rawScale,
                appliedScale = rawScale,
                hasAppliedScale = false,
                frame = Time.frameCount
            };

            // Retorna ao tamanho-base caso a pose nova nao tenha perfil.
            target.localScale = rawScale;
            if (database && (location != AdjustLocation.Holster || ApplyHolsterAdjust) && database.TryFind(character.skinId, weaponId, location, pose, out var adjustment))
            {
                target.localPosition += adjustment.localPositionOffset;
                target.localRotation = target.localRotation * adjustment.localRotationOffset;
                var mult = FBWeaponAdjustSafeDatabase.SafeMultiplier(adjustment.localScaleMultiplier);
                snapshot.appliedScale = Vector3.Scale(rawScale, mult);
                target.localScale = snapshot.appliedScale;
                snapshot.hasAppliedScale = true;
            }
            bases[target] = snapshot;
        }
    }
}
