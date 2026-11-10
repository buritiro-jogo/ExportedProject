// Personagem (jogador, bot, civil, remoto): porte do actor.js do jogo web para o Mecanim nativo + Animation Rigging.
//  - Animator com o controller ORIGINAL (AAA_relaxed): VelX/VelZ/Speed, InCombat, IsRelaxed, IsAiming, IsShooting, IsRunning, pulo, agachar,
//    com os mesmos tempos do PlayerVisualPuppet (fpdata.puppet): peso dinâmico do Upper Body, reação a dano com chance/cooldown, freio.
//  - Trocas de clipe (AnimatorOverrideController) com os MESMOS mapas do actor.js: locomoção desarmada (UA_OVR + conjunto NPC) e armada
//    fuzil/pistola (AR_OVR); tiro automático × semiautomático no estado FIRE.
//  - Camadas: Emote Full Body (danças, sentar, poses de sistema FF_SYS_* — caído, deitar, cura, paraquedas, veículo; A/B com mistura),
//    Emote Upper Body (sacar/guardar do IE, gestos), Finisher (morte sem estado próprio, takedown/especiais).
//  - Pós-animação (FBRig, ordem do actor.js): tronco mira pelo CANO, mão esquerda no guarda-mão / pistola com duas mãos, dedos na arma.
//    IK dos pés = FBFootIK (componente próprio), inclinar/espreitar (Q/E), arma em "low ready" no descanso, recuo do braço, abertura dos braços desarmado.
//  - Coldre do IE: armas guardadas nas costas / bandoleira / coxa; sacar e guardar com os clipes Humanoid do IE.
//  - Passos sincronizados com a velocidade real (rs dos clipes no lugar).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [RequireComponent(typeof(Animator))]
    public class FBCharacter : MonoBehaviour, IKnockable, FBFarinhaBuriti.IActor
    {
        public string skinId, displayName; public int team; public bool isPlayer, isBot, isRemote, civ;
        public float hp = 100, maxHp = 100, armor; [System.NonSerialized] public float protUntil; public bool alive = true, helmet; public int kills, deaths, netId = -1, kits = FBMed.Start;
        [System.NonSerialized] public Vector3 velocity; [System.NonSerialized] public bool grounded = true;
        [System.NonSerialized] public bool crouch, sprint, aiming, shooting, combat, relaxed, reloading, prone, special, surrender, talking, fleeing;
        [System.NonSerialized] public Vector3 aimPoint; [System.NonSerialized] public bool hasAim;
        [System.NonSerialized] public float lastShot = 9, lean, peekRoom = .25f, combatHold;
        [System.NonSerialized] public WeaponDef weapon; [System.NonSerialized] public GameObject weaponGO; public bool Unarmed => weapon == null;
        public System.Action<FBCharacter, FBCharacter> onDeath;
        // último dano (FBCombat) — a morte escolhe a queda pela direção da bala, parte atingida e motivo (FBDeath)
        [System.NonSerialized] public FBHitInfo lastHit; [System.NonSerialized] public string lastWhy; [System.NonSerialized] public bool lastHitHead; [System.NonSerialized] public Vector3? lastHitFrom;
        [System.NonSerialized] public string deathClip;
        // BR / capa do FF: colete e capacete com durabilidade; troca de pose (sem headshot por 0,7 s); nome sobre a cabeça; levantar do caído
        [System.NonSerialized] public FBBrCore.Armor vest, helmetFF; [System.NonSerialized] public float poseT = -9, tagT, getUpT; [System.NonSerialized] public string koFall;
        // knockdown (FBMechanics / IE)
        [System.NonSerialized] public bool knocked; [System.NonSerialized] public float knockInvT, bleedAcc, reviveP, reviveTick, koAt; [System.NonSerialized] public object revivedBy; [System.NonSerialized] public FBCharacter knockedBy;
        public bool Knocked { get => knocked; set => knocked = value; } public float Hp { get => hp; set => hp = value; } public float MaxHp => maxHp;
        public float KnockInvT { get => knockInvT; set => knockInvT = value; } public float BleedAcc { get => bleedAcc; set => bleedAcc = value; }
        public object RevivedBy { get => revivedBy; set => revivedBy = value; } public float ReviveP { get => reviveP; set => reviveP = value; } public bool Civ => civ;
        public bool IsFemale { get { var d = GameData.Skins.Find(s => s.id == skinId); return d != null ? d.sex == "f" : FBCustomizeData.Find(skinId)?.fem == true; } }
        public string Name => !string.IsNullOrEmpty(displayName) ? displayName : (GameData.Skins.Find(s => s.id == skinId)?.name ?? FBCustomizeData.Find(skinId)?.name ?? skinId);   // NPCs do guarda-roupa (Agente 8)
        public Vector3 Pos => transform.position; public Vector3 Fwd => transform.forward; public int Team => team; public bool Alive => alive;
        public float Yaw { get => transform.eulerAngles.y; set => transform.rotation = Quaternion.Euler(0, value, 0); }
        // pose de sistema (FF_SYS_*) / pose de espera; dança/emote de corpo inteiro
        [System.NonSerialized] public string sys; [System.NonSerialized] public float sysFade = -1; /* sysFade: r49i, mistura da PRÓXIMA troca de sys (pilotar moto 0,45 s) */ string waitPose; public class Dance { public string clip; public float t, len; public Queue<(string clip, bool hold)> chain; public string sitOut; public bool leaving; }
        [System.NonSerialized] public Dance dance; public bool Sitting => dance != null && dance.sitOut != null && !dance.leaving;
        public bool UnarmedLocomotion { get; private set; }
        public bool IsPistol => weapon != null && weapon.IsPistol;
        // postura de combate = o InCombat do Animator (CombatLocomotion, arma à frente). Rodada 37: como o PlayerVisualPuppet do Complexo
        // (alwaysInCombat = 1, usePatrolRunOnSprint = 1) armado fica SEMPRE nela, menos no sprint (corrida de patrulha). Fora de ação
        // (Hot = mirou/atirou/recarregou nos últimos 3 s, o combatFlagDuration do puppet) a arma desce em "low ready" (FBRig.lowerS):
        // antes caía na PatrolLocomotion/relaxado ("Rifle Idle"), que com a pegada do puppet dava braços esticados e arma solta.
        public bool CombatStance { get; private set; }
        public bool Hot { get; private set; }
        public bool HasTpsGrip => weapon != null && Json.O(GameData.TpsGrip, weapon.id) != null;   // pegada das mãos medida na animação de 1ª pessoa

        // ---- parâmetros do PlayerVisualPuppet original (fpdata.puppet)
        static float PUP(string k, float d) => Json.F(GameData.Section("puppet"), k, d);
        static readonly string[] DEATHS = { "Rifle_Death_3", "Rifle_Death_L", "Rifle_Death_R", "Rifle_Death_L" };

        readonly HashSet<string> pars = new(); readonly Dictionary<string, string> pn = new();
        string P(string n) { foreach (var p in an.parameters) if (p.name.Trim() == n) return p.name; return null; }
        string PN(string n) { if (!pn.TryGetValue(n, out var r)) pn[n] = r = pars.Contains(n) ? P(n) : null; return r; }
        void SF(string n, float v) { var r = PN(n); if (r != null) an.SetFloat(r, v); }
        void SB(string n, bool v) { var r = PN(n); if (r != null) an.SetBool(r, v); }
        void ST(string n) { var r = PN(n); if (r != null) an.SetTrigger(r); }
        [System.NonSerialized] public Animator an; AnimatorOverrideController ov; readonly List<KeyValuePair<AnimationClip, AnimationClip>> ovList = new();
        readonly Dictionary<string, AnimationClip> origByName = new(); readonly Dictionary<string, string> curOver = new();
        int LU = -1, LF = -1, LE = -1, LEU = -1; bool hasDeathState;
        float upperW, emoW, danW, finW, finTarget, airT, vx, vz, stillT, hitCd, lastSpeed, kick, lastShotPrev = 9, pwW, leanS;
        bool wasAir, wasCrouch, wasSprint, wasStill, emoteB, fullB; string npcIdleLast;
        Transform rHand, lHand, hips, chest, neck, rThigh, rKnee, rFore, rArm, lArm, lFoot, rFoot, muzzle, grip, mag;
        Transform[] spine;
        public Transform Head { get; private set; }
        public Transform Muzzle => muzzle ? muzzle : rHand;
        public Transform RightHand => rHand; public Transform LeftHand => lHand; public Transform Hips => hips; public Transform Neck => neck; public Transform Chest => chest;
        [System.NonSerialized] public FBRig rig;
        [System.NonSerialized] public FBFpsaf fpsaf;   // r57: rig de combate novo (nulo quando desligado nos ajustes ou em policial)
        // conjunto do pacote NPCS (idle sorteado a cada parada, andar próprio) — npcanims.js
        public class NpcSet { public List<string> idle = new(), talk = new(), sit = new(), dance = new(); public Dictionary<string, string> ovr = new(); public string walk, sex = "m"; public float walkRs = 1; public bool activity; }   // activity: andar de atividade do 171 (celular, cigarro…)
        [System.NonSerialized] public float walkSpeed = 1.4f;
        [System.NonSerialized] public NpcSet npcSet;
        float scaleK = 1;                                               // tamanho do corpo em relação ao boneco padrão (2,2)

        void Awake()
        {
            GameData.Load();
            an = GetComponent<Animator>();
            if (!an.runtimeAnimatorController && FBRegistry.I) an.runtimeAnimatorController = FBRegistry.I.mainController;
            if (an.runtimeAnimatorController)
            {
                ov = new AnimatorOverrideController(an.runtimeAnimatorController); an.runtimeAnimatorController = ov;
                ov.GetOverrides(ovList); foreach (var kv in ovList) if (kv.Key) origByName[kv.Key.name] = kv.Key;
            }
            foreach (var p in an.parameters) pars.Add(p.name.Trim());
            LU = an.GetLayerIndex("Upper Body"); LF = an.GetLayerIndex("Finisher"); LE = an.GetLayerIndex("Emote Full Body"); LEU = an.GetLayerIndex("Emote Upper Body");
            hasDeathState = an.runtimeAnimatorController && an.HasState(0, Animator.StringToHash("Death Reaction"));
            if (an.isHuman)
            {
                B(out rHand, HumanBodyBones.RightHand); B(out lHand, HumanBodyBones.LeftHand); Head = an.GetBoneTransform(HumanBodyBones.Head);
                // arma própria do prefab (ownWeapon do actor.js): filhos da mão direita que não são ossos — some quando o jogo põe a arma dele na mão
                if (rHand) { var bones = new HashSet<Transform>(); for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var bt = an.GetBoneTransform((HumanBodyBones)i); if (bt) bones.Add(bt); }
                    foreach (Transform c in rHand) if (!c.name.StartsWith("mixamorig") && !bones.Contains(c) && c.GetComponentInChildren<Renderer>(true)) ownWeapon.Add(c.gameObject); }
                B(out hips, HumanBodyBones.Hips); B(out neck, HumanBodyBones.Neck); B(out rFore, HumanBodyBones.RightLowerArm); B(out rArm, HumanBodyBones.RightUpperArm); B(out lArm, HumanBodyBones.LeftUpperArm);
                chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.Spine);
                B(out rThigh, HumanBodyBones.RightUpperLeg); B(out rKnee, HumanBodyBones.RightLowerLeg); B(out lFoot, HumanBodyBones.LeftFoot); B(out rFoot, HumanBodyBones.RightFoot);
                var sp = new List<Transform>(); foreach (var b in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }) { var t = an.GetBoneTransform(b); if (t) sp.Add(t); }
                spine = sp.ToArray();
                InitTorsoAxes();                                                     // eixos do tronco no esqueleto de repouso (envelope do coldre)
            }
            scaleK = transform.lossyScale.y / FBConst.ModelScale;
            handCorr = FBHand.RightCorrection(an, out palmK, out HandDeg);       // rig com o osso da mão girado (IE/CC) → mesma pegada anatômica
            rig = this.GetOrAdd<FBRig>(); rig.Init(this);
            fpsaf = FBFpsaf.Criar(this);                                   // r57: rig novo (KINEMATION) por cima; null = segue só o clássico
            feet = this.GetOrAdd<FBFootIK>(); feet.Init(this);              // IK dos pés (Agente 3)
            SetUnarmedLocomotion(true, true);
        }
        void B(out Transform t, HumanBodyBones b) => t = an.GetBoneTransform(b);
        // jogador/NPC e sexo só ficam definidos depois do Awake (FBMatch.Spawn): refaz as trocas de locomoção com eles
        void Start() => SetUnarmedLocomotion(UnarmedLocomotion, true);

        // ---------------- trocas de clipe (mesmos mapas do actor.js)
        public void SetOverrides(Dictionary<string, string> map)
        {
            if (ov == null) return;
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var kv in map)
            {
                if (!origByName.TryGetValue(FBRegistry.Safe(kv.Key), out var orig) && !origByName.TryGetValue(kv.Key, out orig)) continue;
                var rep = kv.Value == kv.Key ? null : FBRegistry.I ? FBRegistry.I.Clip(kv.Value) : null;
                if (curOver.TryGetValue(kv.Key, out var was) && was == kv.Value) continue; curOver[kv.Key] = kv.Value;
                list.Add(new KeyValuePair<AnimationClip, AnimationClip>(orig, rep));
            }
            if (list.Count > 0) ov.ApplyOverrides(list);
        }
        public void SetOverride(string orig, string clip) => SetOverrides(new Dictionary<string, string> { [orig] = clip });
        public string OverrideOf(string orig) => curOver.TryGetValue(orig, out var v) ? v : orig;
        static bool Has(string clip) => FBRegistry.I && FBRegistry.I.Clip(clip);

        // um mapa só para os três conjuntos (FBAnimProfile): desarmado = conjunto aprovado antes da rodada 37 (IE + andar de lado/ré do NPC;
        // o NPC do rolê com conjunto próprio por cima); armado = trote/sprint do IE nas vagas de corrida, sprint de pistola do IE; o resto das
        // árvores do Complexo (andar de combate, parado, agachado) fica como está.
        void LocoOverrides()
        {
            var m = new Dictionary<string, string>();
            foreach (var k in FBAnimProfile.Unarmed.Keys) m[k] = k;
            foreach (var k in FBAnimProfile.ArmedRun.Keys) m[k] = k;
            bool pol = IsPolice;
            if (pol) foreach (var d in PolMaps) foreach (var k in d.Keys) m[k] = k;   // troca de arma: nada do conjunto anterior fica preso
            bool novo = !pol && FBAnimProfile.CombateNovo(skinId);                 // r58: combate novo para quem não é policial
            if (novo) foreach (var d in NovoMaps) foreach (var k in d.Keys) m[k] = k;
            if (UnarmedLocomotion)
            {
                foreach (var kv in FBAnimProfile.Unarmed) if (Has(kv.Value)) m[kv.Key] = kv.Value;
                var walk = IsFemale ? FBAnimProfile.WalkFemale : FBAnimProfile.WalkMale; if (Has(walk)) m["WalkFwdLoop"] = walk;   // andar novo por sexo
                // lado/ré: sempre os clipes originais do controller (valor = chave → sem troca), com ou sem conjunto de NPC
                foreach (var kv in FBAnimProfile.UnarmedWalkNpc) if (kv.Value == kv.Key || Has(kv.Value)) m[kv.Key] = kv.Value;
                foreach (var kv in FBAnimProfile.UnarmedRight) if (Has(kv.Value)) m[kv.Key] = kv.Value;
                if (npcSet != null) foreach (var kv in npcSet.ovr) if (Has(kv.Value)) m[kv.Key] = kv.Value;
                if (!isPlayer && !isRemote)
                {   // NPC: lados/diagonais originais; homem anda para frente/trás com os loops originais
                    foreach (var k in FBAnimProfile.NpcStrafe) m[k] = k;
                    if (!IsFemale) foreach (var k in FBAnimProfile.NpcMaleWalk) if (!(k == "WalkFwdLoop" && npcSet != null && npcSet.activity)) m[k] = k;
                }
            }
            else
            {
                foreach (var kv in FBAnimProfile.ArmedRun) if (Has(kv.Value)) m[kv.Key] = kv.Value;
                if (IsPistol && Has(FBAnimProfile.PistolSprint)) m["Rifle_SprintLoop"] = FBAnimProfile.PistolSprint;
            }
            CombatOverrides(m);
            if (pol) PoliceOverrides(m);
            if (novo) NovoCombate(m);
            SetOverrides(m);
        }
        // r58: só mira/tiro/recarga/granada/reação. Nada de locomoção — ver o cabeçalho do NovoRifle no FBAnimProfile.
        static readonly Dictionary<string, string>[] NovoMaps = { FBAnimProfile.NovoRifle, FBAnimProfile.NovoPistol };
        void NovoCombate(Dictionary<string, string> m)
        {
            if (UnarmedLocomotion || weapon == null) return;
            void P(Dictionary<string, string> d)
            {
                foreach (var kv in d) if ((origByName.ContainsKey(kv.Key) || origByName.ContainsKey(FBRegistry.Safe(kv.Key))) && Has(kv.Value)) m[kv.Key] = kv.Value;
            }
            if (IsPistol) { P(FBAnimProfile.NovoPistol); if (crouch && Has(FBAnimProfile.NovoPistolCrouch)) m["Rifle_Idle"] = FBAnimProfile.NovoPistolCrouch; return; }
            P(FBAnimProfile.NovoRifle);
            if (crouch && Has(FBAnimProfile.NovoCrouchAim)) m["Rifle_Idle"] = FBAnimProfile.NovoCrouchAim;
        }
        // rodada 55: policiais (categoria "policia" do elenco) com os pacotes táticos do Mixamo (FBAnimProfile.Pol*); só com -fbpoliciatatica desde a r56
        static readonly Dictionary<string, string>[] PolMaps = { FBAnimProfile.PolRifle, FBAnimProfile.PolRifleSprint, FBAnimProfile.PolPistol, FBAnimProfile.PolPistolSprint, FBAnimProfile.PolUnarmed, FBAnimProfile.PolCompact };
        string polSkin; bool polVal, polSprint, polCrouch; bool novoCrouch;
        public bool IsPolice { get { if (polSkin != skinId) { polSkin = skinId; polVal = FBAnimProfile.PoliciaTatica && FBElenco.In(skinId, FBElenco.POLICIA); } return polVal; } }
        void PoliceOverrides(Dictionary<string, string> m)
        {
            void P(Dictionary<string, string> d, string skip = null)
            {
                foreach (var kv in d) if (kv.Key != skip && (origByName.ContainsKey(kv.Key) || origByName.ContainsKey(FBRegistry.Safe(kv.Key))) && Has(kv.Value)) m[kv.Key] = kv.Value;
            }
            polSprint = sprint; polCrouch = crouch;
            if (UnarmedLocomotion || weapon == null) { P(FBAnimProfile.PolUnarmed, IsFemale ? "WalkFwdLoop" : null); return; }   // a policial mantém o andar feminino aprovado
            if (IsPistol) { P(FBAnimProfile.PolPistol); if (sprint) P(FBAnimProfile.PolPistolSprint); return; }   // recarga da pistola: a de sempre
            P(FBAnimProfile.PolRifle); if (sprint) P(FBAnimProfile.PolRifleSprint);
            // arma curta (SMG/escopeta): parado de mira e pulo do Basic Shooter, para não ficar igual ao fuzil
            if (weapon.cls == "submetralhadora" || weapon.cls == "escopeta") P(FBAnimProfile.PolCompact);
            if (crouch && Has(FBAnimProfile.PolCrouchAim)) m["Rifle_Idle"] = FBAnimProfile.PolCrouchAim;
        }
        // agachado rápido (Shift agachado): troca as 4 direções do andar agachado armado pelas do IE (ver FBAnimProfile.UnarmedCrouch)
        bool crouchFast;
        public bool CrouchFast { get => crouchFast; set { if (crouchFast == value) return; crouchFast = value; LocoOverrides(); } }
        public void SetUnarmedLocomotion(bool on, bool force = false)
        {
            if (!force && UnarmedLocomotion == on) return;
            UnarmedLocomotion = on; stillT = 0; LocoOverrides();
            if (on) { aiming = false; shooting = false; reloading = false; }
        }
        public void SetNpcSet(NpcSet s) { npcSet = s != null && s.idle.Exists(Has) ? s : null; npcIdleLast = null; if (UnarmedLocomotion) SetUnarmedLocomotion(true, true); }
        public void SetFireMode(bool automatic)
        {
            var target = automatic ? "Rifle_ShootLoop_Additive" : FBAnimProfile.Fire(weapon) ?? "Rifle_ShootOnce";
            if ((IsPolice || FBAnimProfile.CombateNovo(skinId)) && weapon != null && !IsPistol) target = automatic ? FBAnimProfile.PolFireAuto : FBAnimProfile.PolFireSemi;
            SetOverride("Rifle_ShootOnce", Has(target) ? target : "Rifle_ShootOnce");
        }
        // ações por classe (FBAnimProfile): recarga do Complexo para fuzil/SMG; pistola/escopeta/sniper do IE (o Complexo não tem em 3ª pessoa);
        // agachado: árvore do Complexo para armado, parado desarmado do IE
        void CombatOverrides(Dictionary<string, string> m)
        {
            bool bare = UnarmedLocomotion || weapon == null;
            void O(string orig, string clip) { if (origByName.ContainsKey(orig) || origByName.ContainsKey(FBRegistry.Safe(orig))) m[orig] = clip != null && Has(clip) ? clip : orig; }
            foreach (var kv in FBAnimProfile.UnarmedCrouch) O(kv.Key, bare ? kv.Value : null);
            if (!bare) foreach (var kv in FBAnimProfile.ArmedCrouchFast) O(kv.Key, crouchFast ? kv.Value : null);
            O("Rifle_Reload_2", bare ? null : FBAnimProfile.Reload(weapon));
        }

        // ---------------- arma
        public void SetWeapon(WeaponDef def, GameObject model)
        {
            if (weaponGO) Destroy(weaponGO);
            foreach (var o in ownWeapon) if (o) o.SetActive(false);   // a arma embutida no modelo nunca aparece junto com a do jogo
            weapon = def; weaponGO = null; muzzle = grip = mag = null; curModel = model; if (cur != null) { Destroy(cur.go); cur = null; } drawT = -1; drawHid = false;
            if (def != null && model && rHand)
            {
                var pup = GameData.Section("puppet");
                var p = Json.O(pup, "tpsWeaponProxyLocalPosition"); var e = Json.O(pup, "tpsWeaponProxyLocalEuler"); var sc = Json.O(pup, "tpsWeaponProxyLocalScale");
                weaponGO = FBWeaponModel.Make(def, rHand);
                // pose manual do PlayerVisualPuppet com os valores do Unity ORIGINAL, sem espelho: o exportador dos APKs espelhou X para o glTF
                // e o glTFast desfaz isso na importação (avatar.json = glTF com x,-y,-z) — mão e arma já estão no espaço do jogo original.
                // O web (three.js, espaço do glTF) precisa espelhar; aqui espelhar de novo girava a arma 180° no próprio cano (de cabeça
                // para baixo) e trocava o lado do deslocamento. handCorr: referencial anatômico da mão deste rig (FBHand).
                var lp = p != null ? new Vector3(Json.F(p, "x"), Json.F(p, "y"), Json.F(p, "z")) : new Vector3(-.0191f, .0931f, .0241f);
                var eu = e != null ? new Vector3(Json.F(e, "x"), Json.F(e, "y"), Json.F(e, "z")) : new Vector3(-92.148f, 168.481f, -91.717f);
                float hk = HandK;                                                   // r50: osso da mão com outra escala que não 2,2 × scaleK
                weaponGO.transform.localPosition = handCorr * lp * palmK * hk;
                weaponGO.transform.localRotation = handCorr * Quaternion.Euler(eu);   // Euler do Unity = ordem ZXY, a mesma do original
                weaponGO.transform.localScale = Vector3.one * (sc != null ? Json.F(sc, "x", .6175f) : .6175f) * hk * WeaponModelFix(def.id);
                // [parceiro] empunhadura pela arma de referência da classe (GripCorrect: gira e move a arma); sem referência (a própria
                // AK/Desert Eagle, arma sem tpsGrip) fica a r51: só translação da palma para a palma padrão (AlignGrip)
                GripShift = GripCorrect(def, hk) ?? (GripAlign ? AlignGrip(def, weaponGO.transform, hk) : Vector3.zero);
                proxyQ = weaponGO.transform.localRotation; proxyP = weaponGO.transform.localPosition;
                foreach (var t in weaponGO.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("SOCKET_Muzzle")) muzzle = t; else if (t.name.StartsWith("SOCKET_Grip")) grip = t; else if (t.name.StartsWith("SOCKET_Magazine")) mag = t;
                }
                foreach (var c in weaponGO.GetComponentsInChildren<Collider>(true)) Destroy(c);
                barrel = null; FBLaser.Bind(this, weaponGO, def, false);
            }
            SetUnarmedLocomotion(def == null);
            if (def != null) SetFireMode(def.auto);
            LocoOverrides();
            rig?.OnWeapon(weaponGO, grip, mag, muzzle, IsPistol);
            fpsaf?.OnWeapon(weaponGO, grip, IsPistol);                      // r57: alvo dos ossos virtuais do rig novo
        }
        Quaternion handCorr = Quaternion.identity; float palmK = 1; [System.NonSerialized] public float HandDeg;   // desvio do osso da mão deste rig (graus)
        public Vector3 ProxyLocalPos => proxyP; public Quaternion ProxyLocalRot => proxyQ;
        Quaternion proxyQ; Vector3 proxyP; (Transform node, Vector3 dir)? barrel;
        readonly List<GameObject> ownWeapon = new();
        // rodada 39 (retorno: "ao puxar a arma das costas aparecem duas armas"): FBPlayer/FBBot chamam isto TODO quadro e religavam a arma da
        // mão durante o saque, enquanto a cópia das costas (cur) ainda aparecia até a mão pegar — agora o saque/guarda (drawHid) manda
        public void SetWeaponVisible(bool v) { if (weaponGO) weaponGO.SetActive(v && !drawHid); else foreach (var o in ownWeapon) if (o) o.SetActive(v && !vehiclePose); }

        // eixo do cano (barrelAxis do actor.js): GunAxes (soquete da boca com o maior deslocamento — cada arma tem dois)
        (Transform, Vector3)? BarrelAxis()
        {
            if (weaponGO && GunAxes(weaponGO, out var ax, out var bl, out _)) return (ax, bl);
            return weaponGO ? (weaponGO.transform, Vector3.forward) : null;
        }
        /// <summary>Eixos de uma arma (rodada 37): cada GLB tem DOIS SOCKET_Muzzle — um no nó 'root' deslocado ao longo do cano (ex.: 0,−0,56,0,06)
        /// e outro dentro do acessório da boca em (0,0,0). O código antigo pegava o ÚLTIMO e deduzia o cano pela posição local dele: em sniper,
        /// Barrett, G18, Desert Eagle, 1911, Beretta, P90, Uzi e no "38" o eixo virava o "frente" do objeto (cano de lado: arma atravessada na
        /// mão, mira do tronco/punho corrigindo para o lado errado, coldre girado). Aqui: o soquete com MAIOR deslocamento; cano = eixo dominante
        /// do deslocamento; topo = o resto perpendicular (a altura da alma acima do punho).</summary>
        public static bool GunAxes(GameObject w, out Transform space, out Vector3 barrel, out Vector3 top)
        {
            space = null; barrel = Vector3.forward; top = Vector3.up; if (!w) return false; Transform best = null; float bm = 1e-3f;
            foreach (var t in w.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("SOCKET_Muzzle") && t.parent && t.localPosition.magnitude > bm) { bm = t.localPosition.magnitude; best = t; }
            if (!best) return false;
            var p = best.localPosition; int ax = Mathf.Abs(p.x) >= Mathf.Abs(p.y) && Mathf.Abs(p.x) >= Mathf.Abs(p.z) ? 0 : Mathf.Abs(p.y) >= Mathf.Abs(p.z) ? 1 : 2;
            var b = Vector3.zero; b[ax] = Mathf.Sign(p[ax]); var rest = p - b * p[ax];
            Vector3 tp; if (rest.magnitude > 1e-3f) { int a2 = Mathf.Abs(rest.x) >= Mathf.Abs(rest.y) && Mathf.Abs(rest.x) >= Mathf.Abs(rest.z) ? 0 : Mathf.Abs(rest.y) >= Mathf.Abs(rest.z) ? 1 : 2; tp = Vector3.zero; tp[a2] = Mathf.Sign(rest[a2]); }
            else tp = ax == 1 ? Vector3.forward : Vector3.up;
            space = best.parent; barrel = b; top = tp; return true;
        }
        public Vector3 BarrelDir() { barrel ??= BarrelAxis(); return barrel is (Transform n, Vector3 d) && n ? n.TransformDirection(d).normalized : transform.forward; }

        // ---------------- coldre (HOLST do actor.js)
        // escala de mundo da arma guardada = a da arma na mão (pose do puppet: 0,6175 no osso da mão) — medida, não deduzida
        // r49 (pedido de 30/10: "algumas armas estão gigantescas nas mãos, costas, peito ou lateral — encontrar a CAUSA RAIZ"). Causa: a pose da
        // arma do PlayerVisualPuppet (posição e escala 0,6175) está no espaço do osso RightHand do rig ORIGINAL, cuja escala de mundo era ≈ 1,62
        // (0,6175 × 1,62 = 1: os modelos do pacote LPSP estão em metros — no glTF o AK tem 0,93 m, a G18 0,21 m, a MP5 0,60 m). Os personagens
        // atuais (r_* da r36 e x171) têm o osso da mão com a escala da raiz (2,2 × scaleK; laboratório -fbescalalab: 141 de 141), então a arma
        // saía 2,2 / 1,62 = 1,36× maior (AK com 1,27 m na mão) e o coldre copiava essa escala (costas, peito, coxa). Agora posição e escala da
        // arma ficam no referencial de mundo do rig original: arma em metros × scaleK em qualquer rig. WeaponScaleFix = false = como antes.
        // r50 (31/10: "as armas ficaram extremamente pequenas, desproporcionais ao tamanho dos personagens"): a conta da r49 estava errada — o
        // laboratório comparava o comprimento em UNIDADES DE MUNDO com metros reais, mas os personagens medem 2,39–2,80 unidades (≈ 1,43 por
        // metro de gente): HandK = (1/0,6175)/2,2 = 0,736 encolheu TODAS as armas para ~0,65 m de AK num corpo de 1,75 m. A pose 0,6175 do puppet
        // já estava certa para o osso da mão com escala de mundo 2,2 × scaleK (os 141 rigs r_*/x171): arma = modelo × 1,3585 × scaleK = AK de
        // 0,89 m proporcional ao corpo (real 0,88). HandK só corrige rig cujo osso da mão tenha outra escala; o "gigante" de 30/10 eram modelos
        // fora do tamanho real (P90 e Skorpion +34 %, .38 +26 %, 1911 +20 %…) — corrigidos um a um em ModelFix. WeaponScaleFix = false = sem ModelFix.
        public static bool WeaponScaleFix = true;
        public const float WeaponWorld = FBConst.ModelScale * .6175f;                   // unidades de mundo por metro do modelo (pose do puppet no rig 2,2)
        float HandK => rHand ? FBConst.ModelScale * scaleK / Mathf.Max(1e-4f, Mathf.Abs(rHand.lossyScale.x)) : 1;
        // comprimento do modelo (m) → real (m), com o corpo como régua (L ÷ altura × 1,75; laboratório -fbescalalab r50): só quem passa de ~10 %
        // do real da própria arma. P90 0,66 → 0,50 · Skorpion 0,70 → 0,52 · .38 0,33 → 0,27 (revólver 6") · 1911 0,26 → 0,22 · G3/G3 1 1,14/1,18
        // → 1,03 · Galil ACE 0,44 → 0,85 (modelo encolhido) · Remington 870 1,12 → 1,00 · Sniper Hunting 1,30 → 1,12. Saíram da r49: RPG-7 (com a
        // granada mede ~1,2 m de verdade) e ParaFAL (1,11 × 1,095 real). As demais ficam a até ~10 % do real (ar15_compact/mini e G36 são curtas).
        static readonly Dictionary<string, float> ModelFix = new() { ["p90"] = .75f, ["czskorpion"] = .74f, ["38"] = .82f, ["1911_gold"] = .84f, ["g3"] = .90f, ["g3_1"] = .87f, ["galil_ace"] = 1.9f, ["remington_870"] = .89f, ["sniper_hunting"] = .86f, ["rpk"] = 1.18f };   // r50: RPK media igual à AK (0,88 m de gente; real 1,04, coronha fixa)
        // r51 (pedido: "mãos alinhadas com a empunhadura... verifique individualmente os modelos"): o proxy é UM só para todas as armas (a pegada
        // aprovada), mas o cabo de cada modelo LPSP fica num lugar diferente da raiz — no espaço do próprio modelo a palma da 1ª pessoa (tpsGrip) da
        // ar15_prime/ag14w está a 6,7 cm da mediana, .38/rc425 a 5,3 cm (no exe: palma a 6,2/6,2/5,0/4,4 cm do lugar) — e o ModelFix da r50 escalava
        // EM VOLTA DA RAIZ (galil ×1,9: cabo no padrão, levado 7 cm para fora da mão). Agora cada arma é transladada para o ponto da palma DELA cair
        // onde cai o da arma padrão da classe (mediana: pistolas × armas longas) no tamanho padrão — a escala do ModelFix cresce em volta do cabo.
        // Só translação: a rotação do proxy original fica intacta (memória tps-weapon-grip). -fbpegadaantiga = antes.
        public static bool GripAlign = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbpegadaantiga") < 0;
        [System.NonSerialized] public Vector3 GripShift;                                         // deslocamento aplicado (espaço da mão; teste)
        static Vector3? gripStdP, gripStdL;
        static Vector3 GV(object o) { var l = o as List<object>; return l != null && l.Count >= 3 ? new Vector3(-Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : Vector3.zero; }
        public static bool FpPalm(string id, out Vector3 p)
        {   // centro da palma direita na animação de 1ª pessoa (meio de punho → base do dedo médio), espaço do nó da arma (glTF, x espelhado)
            var r = Json.O(Json.O(GameData.TpsGrip, id), "r"); var w = Json.L(r, "w"); var m = Json.L(r, "m"); p = default;
            if (w == null || w.Count < 3 || m == null || m.Count == 0) return false; p = Vector3.Lerp(GV(w), GV(m[0]), .5f); return true;
        }
        public static Vector3 GripStd(bool pistol)
        {
            if (gripStdP == null)
            {
                var ps = new List<Vector3>(); var ls = new List<Vector3>();
                foreach (var kv in GameData.Weapons) if (FpPalm(kv.Key, out var p)) (kv.Value.cls == "pistola" ? ps : ls).Add(p);
                static float Med(List<Vector3> v, int i) { var a = new List<float>(); foreach (var q in v) a.Add(q[i]); a.Sort(); return a.Count > 0 ? a[a.Count / 2] : 0; }
                gripStdP = new Vector3(Med(ps, 0), Med(ps, 1), Med(ps, 2)); gripStdL = new Vector3(Med(ls, 0), Med(ls, 1), Med(ls, 2));
            }
            return pistol ? gripStdP.Value : gripStdL.Value;
        }
        static Transform GripNode(Transform root) => root.childCount == 1 && !root.GetComponent<Renderer>() ? root.GetChild(0) : root;
        static Vector3 GripRoot(Transform root, Vector3 g) => root.InverseTransformPoint(GripNode(root).TransformPoint(g));   // nó da arma → raiz do modelo (sem a escala dela)
        // deslocamento da r51 (espaço da mão) para o modelo 'root' com rotação 'rot' e escala 'scale': palma dele → palma padrão no tamanho padrão
        static Vector3 AlignShift(WeaponDef def, Transform root, Quaternion rot, float scale, float hk)
        {
            if (!FpPalm(def.id, out var pW)) return Vector3.zero; float k = WeaponModelFix(def.id);
            var d = rot * ((GripRoot(root, GripStd(def.cls == "pistola")) / k - GripRoot(root, pW)) * scale);
            return d.magnitude > .15f * hk ? Vector3.zero : d;                                                    // dado estranho: fica como era
        }
        static Vector3 AlignGrip(WeaponDef def, Transform wt, float hk) { var d = AlignShift(def, wt, wt.localRotation, wt.localScale.x, hk); wt.localPosition += d; return d; }

        // [parceiro] Empunhadura em terceira pessoa: todas as armas nascem com a mesma pose na mão direita. Para as armas (ou classes)
        // listadas aqui, a arma é girada e movida para que a mão fique nela do mesmo jeito que fica na arma de referência,
        // usando a posição da mão direita de cada arma guardada em tpsGrip.<id>.r (pulso, nós dos dedos), em espaço da arma.
        // Referências conferidas com fotos antes x depois das 51 armas (revólveres e RPG eram os que ficavam fora da mão).
        // União com a r51 (AlignGrip): a referência vai para o lugar FINAL dela (proxy + translação da palma da r51, tamanho do ModelFix da
        // referência), então as armas ajustadas caem na mão exatamente como a AK/Desert Eagle de hoje. Isto GIRA a arma (a memória
        // tps-weapon-grip pedia a rotação do proxy intacta: a referência continua com ela). -fbpegadaantiga desliga as duas (pegada da r45).
        public static Dictionary<string, string> GripRefs = new()
        {
            ["pistola"] = "desert_eagle", ["fuzil"] = "ak", ["submetralhadora"] = "ak", ["escopeta"] = "ak", ["sniper"] = "ak", ["explosivo"] = "ak"
        };
        public static bool GripAuto = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbpegadaantiga") < 0;   // [parceiro] padrão true (FBLabFisica liga/desliga)
        static readonly Dictionary<string, (Vector3 wrist, Quaternion rot)?> gripFrames = new();
        // [parceiro] pulso e orientação da mão direita no espaço da raiz do modelo da arma (lidos do prefab, iguais para toda instância)
        static (Vector3 wrist, Quaternion rot)? GripFrame(string id)
        {
            if (gripFrames.TryGetValue(id, out var f)) return f;
            (Vector3 wrist, Quaternion rot)? res = null;
            var r = Json.O(Json.O(GameData.TpsGrip, id), "r"); var m = Json.L(r, "m"); var ix = Json.L(r, "i"); var pk = Json.L(r, "p");
            var go = FBRegistry.I ? FBRegistry.I.Weapon(id) : null;
            if (r != null && r.TryGetValue("w", out var w) && m is { Count: > 0 } && ix is { Count: > 0 } && pk is { Count: > 0 } && go)
            {
                var t = go.transform; var wr = GripRoot(t, GV(w));
                var fw = GripRoot(t, GV(m[0])) - wr;                                       // pulso → nó do dedo médio
                var up = Vector3.Cross(fw, GripRoot(t, GV(ix[0])) - GripRoot(t, GV(pk[0])));   // normal da palma (indicador × mínimo)
                if (fw.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-10f) res = (wr, Quaternion.LookRotation(fw, up));
            }
            if (FBRegistry.I) gripFrames[id] = res;                                        // sem registro ainda: tenta de novo depois
            return res;
        }
        public static string GripRefOf(WeaponDef def)
        {
            if (def == null) return null;
            return GripRefs.TryGetValue(def.id, out var v) || (def.cls != null && GripRefs.TryGetValue(def.cls, out v)) ? v : null;
        }
        // devolve o deslocamento aplicado (espaço da mão) ou null quando a arma não tem referência/medida (aí vale o AlignGrip)
        Vector3? GripCorrect(WeaponDef def, float hk)
        {
            var refId = GripAuto ? GripRefOf(def) : null;
            if (!weaponGO || string.IsNullOrEmpty(refId) || refId == def.id) return null;
            var own = GripFrame(def.id); var rf = GripFrame(refId); if (own == null || rf == null) return null;
            var t = weaponGO.transform; var R = t.localRotation; float s = t.localScale.x;
            float sRef = s / WeaponModelFix(def.id) * WeaponModelFix(refId);                  // escala que a referência teria nesta mão
            var refGo = FBRegistry.I ? FBRegistry.I.Weapon(refId) : null;
            var dRef = GripAlign && refGo && GameData.Weapons.TryGetValue(refId, out var refDef) ? AlignShift(refDef, refGo.transform, R, sRef, hk) : Vector3.zero;
            var Q = R * rf.Value.rot * Quaternion.Inverse(own.Value.rot);
            var d = dRef + R * rf.Value.wrist * sRef - Q * own.Value.wrist * s;             // pulso desta arma = pulso da referência
            t.localPosition += d; t.localRotation = Q; return d;
        }
        // r51: a galil_ace ×1,9 (r50, "modelo encolhido") contradiz a própria malha — no tamanho de fábrica ela tem o cabo e o gatilho EXATAMENTE onde
        // fica a mão da animação de 1ª pessoa (vértice a 2,4 cm da palma e 2,0 cm da ponta do indicador, igual AK 2,9/2,1 · M4 3,0/2,3 · G3 2,0/1,4),
        // ou seja, é uma ACE curta modelada na proporção da mão. ×1,9 engrossava o cabo 1,9× e levava o gatilho a 18 cm da palma (exe r51c: gatilho
        // 7,8 cm longe do dedo, punho 6,4 cm). Com a pegada nova ela volta ao tamanho de fábrica; -fbpegadaantiga = ×1,9 de novo.
        public static float WeaponModelFix(string id) => WeaponScaleFix && id != null && !(GripAlign && id == "galil_ace") && ModelFix.TryGetValue(id, out var f) ? f : 1;
        public float HolsterWorldScale => WeaponWorld * scaleK;
        public IEnumerable<GameObject> HolsterObjects { get { foreach (var h in stowed.Values) if (h.go) yield return h.go; if (cur != null && cur.go) yield return cur.go; } }
        public static string HolsterSlot(string cls) => cls == "pistola" ? "thigh" : cls == "submetralhadora" ? "sling" : "back";
        class Holstered
        {
            public GameObject go;
            public string slot;
            public Vector3 b, t, c;
            public float half, thick, wide, d, fix = 1;                     // fix: r49, tamanho real do modelo (WeaponModelFix)
            public bool dOk;
            // Cache do ajuste manual (evita procurar a cada frame para varios jogadores/bots).
            public FBHolsterManualCalibrations.Entry manual;
            public FBHolsterManualCalibrations manualDatabase;
            public string manualSkin;
            public int manualRevision = int.MinValue;
            public float manualClearance;
            public bool manualClearanceReady;
        }
        static FBHolsterManualCalibrations manualHolsterDatabase;

        bool TryManualHolster(Holstered h, out FBHolsterManualCalibrations.Entry entry)
        {
            if (!manualHolsterDatabase)
                manualHolsterDatabase = Resources.Load<FBHolsterManualCalibrations>("FBHolsterManualCalibrations");
            var db = manualHolsterDatabase;
            entry = null;
            if (!db || !h.go || !h.go.name.StartsWith("Coldre_")) return false;
            var sk = skinId ?? string.Empty;
            if (h.manualDatabase != db || h.manualRevision != db.Revision || h.manualSkin != sk)
            {
                db.TryFind(sk, h.go.name.Substring("Coldre_".Length), out h.manual);
                h.manualDatabase = db;
                h.manualRevision = db.Revision;
                h.manualSkin = sk;
                h.manualClearanceReady = false;
            }
            entry = h.manual;
            return entry != null;
        }
        readonly Dictionary<string, Holstered> stowed = new();
        GameObject curModel; Holstered cur;
        // Caixa do renderer no espaço LOCAL da arma (inv = mundo→arma). O Renderer.bounds é um AABB do MUNDO: medir com ele deixava
        // comprimento/espessura dependentes da direção para onde o personagem olhava na hora de criar o coldre (a arma ia mais longe a cada troca).
        // Aqui usamos os 8 cantos da caixa da malha transformados pela matriz do renderer — o resultado independe da rotação do personagem.
        internal static bool LocalBoundsOf(Renderer r, Matrix4x4 inv, out Bounds b)   // [parceiro 01/11] internal: o FBRig v17 mede a arma com ela
        {
            b = default; Mesh m = null;
            if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh; else { var mf = r.GetComponent<MeshFilter>(); if (mf) m = mf.sharedMesh; }
            var mb = m ? m.bounds : r.bounds; var M = m ? inv * r.transform.localToWorldMatrix : inv;
            if (mb.size.sqrMagnitude <= 0) return false;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = M.MultiplyPoint3x4(corner); if (i == 0) b = new Bounds(p, Vector3.zero); else b.Encapsulate(p);
            }
            return true;
        }
        Holstered MakeHolster(WeaponDef def, GameObject model)
        {
            var h = new Holstered { slot = HolsterSlot(def.cls), go = FBWeaponModel.Make(def, transform), fix = WeaponModelFix(def.id) }; h.go.name = "Coldre_" + def.id;
            foreach (var c in h.go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            h.go.transform.localScale = Vector3.one; h.go.transform.localRotation = Quaternion.identity; h.go.transform.localPosition = Vector3.zero;
            Transform mz = null, mg = null; foreach (var t in h.go.GetComponentsInChildren<Transform>(true)) { if (t.name.StartsWith("SOCKET_Muzzle")) mz = t; else if (t.name.StartsWith("SOCKET_Magazine")) mg = t; }
            var inv = h.go.transform.worldToLocalMatrix; var bb = new Bounds(); bool first = true;
            foreach (var r in h.go.GetComponentsInChildren<Renderer>()) { if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue; if (!LocalBoundsOf(r, inv, out var lb)) continue; if (first) { bb = lb; first = false; } else bb.Encapsulate(lb); }
            h.c = bb.center;
            if (GunAxes(h.go, out var gs, out var gb, out var gt)) { h.b = inv.MultiplyVector(gs.TransformDirection(gb)).normalized; h.t = Vector3.ProjectOnPlane(inv.MultiplyVector(gs.TransformDirection(gt)), h.b).normalized; }   // eixos certos da arma
            else
            {
                h.b = mz ? (inv.MultiplyPoint3x4(mz.position) - h.c).normalized : Vector3.forward;
                var up = mg ? -(inv.MultiplyPoint3x4(mg.position) - h.c) : Vector3.up; h.t = Vector3.ProjectOnPlane(up, h.b).normalized;
            }
            if (h.t.sqrMagnitude < .5f) h.t = Mathf.Abs(Vector3.Dot(h.b, Vector3.up)) < .9f ? Vector3.ProjectOnPlane(Vector3.up, h.b).normalized : Vector3.forward;
            float Sup(Vector3 d) => Mathf.Abs(bb.extents.x * d.x) + Mathf.Abs(bb.extents.y * d.y) + Mathf.Abs(bb.extents.z * d.z);   // alcance da caixa numa direção
            h.half = Sup(h.b); h.thick = Sup(Vector3.Cross(h.b, h.t).normalized); h.wide = Sup(h.t);
            return h;
        }
        public void SetHolsters(IEnumerable<(WeaponDef def, GameObject model)> list)
        {
            var keep = new HashSet<string>();
            foreach (var (def, model) in list) { if (def == null || !model) continue; keep.Add(def.id); if (!stowed.ContainsKey(def.id)) stowed[def.id] = MakeHolster(def, model); }
            foreach (var id in new List<string>(stowed.Keys)) if (!keep.Contains(id)) { Destroy(stowed[id].go); stowed.Remove(id); }
        }
        bool stowCur;
        public void StowCurrent(bool on) { stowCur = on; }                         // bot guardou/sacou a própria arma (fica nas costas/coldre)
        // CORPO PARA AS ARMAS GUARDADAS — RODADA 39 (refeito; retorno do usuário: "as armas continuam atravessando o corpo, em outros momentos
        // ficam certas nas costas; a arma do peito está extremamente longe, flutuando"). Causa medida: o envelope gravado pelo laboratório
        // (união das poses animadas) saiu inválido — no r_tutu as COSTAS ficaram zeradas e a FRENTE com 0,5–1 m (antebraços, mãos e joelhos
        // das poses de mira/agachado entravam no perfil do tronco): a SMG do peito ia ~50 cm para a frente e o fuzil ficava rente/dentro das
        // costas, conforme a pose em que o perfil "atual" tinha sido medido. Agora, sem depender de pose medida:
        //  · partes RÍGIDAS tiradas da pose de BIND da malha (bindposes: o mesmo resultado em qualquer partida, animação ou laboratório):
        //    tronco por osso (quadril, coluna, peito, peito alto + pescoço/ombros) = colunas de profundidade no espaço do PRÓPRIO osso (frente,
        //    costas e os dois lados); braço, antebraço, mão, coxa, canela e pé = cápsulas afuniladas; cabeça = esfera (97º percentil);
        //  · a cada quadro, DEPOIS do IK, a arma é empurrada para fora na direção do encaixe até não tocar nenhuma parte na pose real
        //    (2 cm de folga): sai na hora e volta devagar (braço/joelho "empurram" a arma, sem tremer).
        class BodyPart
        {
            public HumanBodyBones hb, endHb = HumanBodyBones.LastBone; public int kind;      // 0 = tronco (colunas), 1 = cápsula afunilada, 2 = esfera, 3 = eixos da cabeça (sem volume)
            public Vector3 end, c; public float r0, r1;                                        // cápsula: ponta local (mão/pé) e raios; esfera: centro/raio(r0)
            public Vector3 ax, ay, az; public float cell; public int nx, ny, nz, ox, oy, oz;   // tronco: eixos locais (lado, cima, frente) e grade
            public float[] fF, fB, sR, sL;                                                     // frente/costas sobre (x,y); direita/esquerda sobre (z,y)
        }
        static readonly Dictionary<string, List<BodyPart>> bodyCache = new();
        List<BodyPart> body; Transform[] bodyBone, bodyEnd; bool bodyTried;
        Vector3 tUpL, tRtL; bool tAxes; Transform lThighB;
        public static double MeasureMs, SolveMs; public static int MeasureN, SolveN;                       // custo (autoteste)
        string BodyKey()
        {
            var sb = new System.Text.StringBuilder(64); sb.Append(Mathf.RoundToInt(transform.lossyScale.y * 1000));
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>()) if (r && r.enabled && r.sharedMesh && !h_isWeapon(r.transform)) sb.Append('|').Append(r.sharedMesh.GetHashCode());
            return sb.ToString();
        }
        static HumanBodyBones PartOf(HumanBodyBones b, HumanBodyBones chestHb)
        {
            if (b == HumanBodyBones.Neck || b == HumanBodyBones.LeftShoulder || b == HumanBodyBones.RightShoulder) return chestHb;
            if (b == HumanBodyBones.Jaw || b == HumanBodyBones.LeftEye || b == HumanBodyBones.RightEye) return HumanBodyBones.Head;
            if (b >= HumanBodyBones.LeftThumbProximal && b <= HumanBodyBones.LeftLittleDistal) return HumanBodyBones.LeftHand;
            if (b >= HumanBodyBones.RightThumbProximal && b <= HumanBodyBones.RightLittleDistal) return HumanBodyBones.RightHand;
            if (b == HumanBodyBones.LeftToes) return HumanBodyBones.LeftFoot; if (b == HumanBodyBones.RightToes) return HumanBodyBones.RightFoot;
            return b;
        }
        static float Pct(List<float> l, float q) { if (l.Count == 0) return 0; l.Sort(); return l[Mathf.Clamp(Mathf.RoundToInt(q * (l.Count - 1)), 0, l.Count - 1)]; }
        /// <summary>Partes rígidas do corpo na pose de BIND (uma vez por conjunto de malhas + escala). Também fixa os eixos do tronco.</summary>
        List<BodyPart> MeasureBind()
        {
            if (!an || !an.isHuman || !chest) return null;
            var human = new Dictionary<Transform, HumanBodyBones>(); for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var t = an.GetBoneTransform((HumanBodyBones)i); if (t && !human.ContainsKey(t)) human[t] = (HumanBodyBones)i; }
            if (!human.TryGetValue(chest, out var chestHb)) return null;
            var pts = new Dictionary<HumanBodyBones, List<Vector3>>();
            Vector3? up = null, rt = null; Matrix4x4[] bpUse = null; Dictionary<Transform, int> idxUse = null;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r || !r.enabled || !r.sharedMesh || !r.sharedMesh.isReadable || h_isWeapon(r.transform)) continue;
                var m = r.sharedMesh; var bones = r.bones; var bp = m.bindposes; var bw = m.boneWeights; var V = m.vertices; if (bw.Length != V.Length || bp.Length != bones.Length) continue;
                var idx = new Dictionary<Transform, int>(); for (int i = 0; i < bones.Length; i++) if (bones[i] && !idx.ContainsKey(bones[i])) idx[bones[i]] = i;
                var part = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++) { var t = bones[i]; while (t && t != transform && !human.ContainsKey(t)) t = t.parent; part[i] = t && human.TryGetValue(t, out var hb) ? (int)PartOf(hb, chestHb) : -1; }
                // eixos do tronco: posições de bind (espaço da malha) de quadril, pescoço e ombros
                if (up == null && idx.ContainsKey(chest) && hips && neck && rArm && lArm && idx.ContainsKey(hips) && idx.ContainsKey(neck) && idx.ContainsKey(rArm) && idx.ContainsKey(lArm))
                {
                    Vector3 MP(Transform t) => bp[idx[t]].inverse.GetColumn(3);
                    var u = (MP(neck) - MP(hips)).normalized; var rr = Vector3.ProjectOnPlane(MP(rArm) - MP(lArm), u).normalized;
                    if (rr.sqrMagnitude > .5f) { up = u; rt = rr; bpUse = bp; idxUse = idx; }
                }
                var pIdx = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++) { pIdx[i] = -1; if (part[i] < 0) continue; var pt = an.GetBoneTransform((HumanBodyBones)part[i]); if (pt && idx.TryGetValue(pt, out int pi)) pIdx[i] = pi; }
                for (int v = 0; v < V.Length; v++)
                {
                    var b = bw[v]; int bi = b.boneIndex0; float w = b.weight0;                   // osso dominante = MAIOR peso (não confia na ordem)
                    if (b.weight1 > w) { bi = b.boneIndex1; w = b.weight1; } if (b.weight2 > w) { bi = b.boneIndex2; w = b.weight2; } if (b.weight3 > w) { bi = b.boneIndex3; }
                    if (bi < 0 || bi >= bones.Length || pIdx[bi] < 0) continue;
                    var key = (HumanBodyBones)part[bi]; if (!pts.TryGetValue(key, out var L)) pts[key] = L = new List<Vector3>();
                    L.Add(bp[pIdx[bi]].MultiplyPoint3x4(V[v]));
                }
            }
            if (up == null || pts.Count == 0) return null;
            var parts = new List<BodyPart>(); float s = transform.lossyScale.y / FBConst.ModelScale;
            Vector3 Local(HumanBodyBones hb, Vector3 meshDir) { var t = an.GetBoneTransform(hb); return t && idxUse.TryGetValue(t, out int i) ? bpUse[i].MultiplyVector(meshDir).normalized : Vector3.zero; }
            // eixos do tronco (osso do peito) na pose de bind — TorsoAxes
            tUpL = Local(chestHb, up.Value); tRtL = Local(chestHb, rt.Value); tAxes = tUpL.sqrMagnitude > .5f && tRtL.sqrMagnitude > .5f;
            var fwM = Vector3.Cross(rt.Value, up.Value);
            // eixos da cabeça (frente/cima no osso da cabeça) para o olhar da pistola (FBRig.HeadLook)
            { var hf = Local(HumanBodyBones.Head, fwM); var hu = Local(HumanBodyBones.Head, up.Value); if (hf.sqrMagnitude > .5f && hu.sqrMagnitude > .5f) parts.Add(new BodyPart { hb = HumanBodyBones.Head, kind = 3, az = hf, ay = hu }); }
            foreach (var kv in pts)
            {
                var hb = kv.Key; var P = kv.Value; var bt = an.GetBoneTransform(hb); if (!bt || P.Count < 12) continue;
                var bp0 = new BodyPart { hb = hb };
                bool torso = hb == HumanBodyBones.Hips || hb == HumanBodyBones.Spine || hb == HumanBodyBones.Chest || hb == HumanBodyBones.UpperChest;
                if (torso)
                {
                    bp0.kind = 0; bp0.ax = Local(hb, rt.Value); bp0.ay = Local(hb, up.Value); bp0.az = Local(hb, fwM); if (bp0.ax.sqrMagnitude < .5f) continue;
                    float cell = bp0.cell = .03f * s / Mathf.Max(1e-6f, bt.lossyScale.x);
                    float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
                    foreach (var p in P) { float x = Vector3.Dot(p, bp0.ax), y = Vector3.Dot(p, bp0.ay), z = Vector3.Dot(p, bp0.az); x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); z0 = Mathf.Min(z0, z); z1 = Mathf.Max(z1, z); }
                    bp0.ox = 2 - Mathf.FloorToInt(x0 / cell); bp0.nx = Mathf.FloorToInt(x1 / cell) + bp0.ox + 3; bp0.oy = 2 - Mathf.FloorToInt(y0 / cell); bp0.ny = Mathf.FloorToInt(y1 / cell) + bp0.oy + 3; bp0.oz = 2 - Mathf.FloorToInt(z0 / cell); bp0.nz = Mathf.FloorToInt(z1 / cell) + bp0.oz + 3;
                    int NX = bp0.nx, NY = bp0.ny, NZ = bp0.nz; if (NX * NY > 40000 || NZ * NY > 40000) continue;
                    var fF = new float[NX * NY]; var fB = new float[NX * NY]; var sR = new float[NZ * NY]; var sL = new float[NZ * NY];
                    foreach (var p in P)
                    {
                        float x = Vector3.Dot(p, bp0.ax), y = Vector3.Dot(p, bp0.ay), z = Vector3.Dot(p, bp0.az);
                        int ix = Mathf.FloorToInt(x / cell) + bp0.ox, iy = Mathf.FloorToInt(y / cell) + bp0.oy, iz = Mathf.FloorToInt(z / cell) + bp0.oz;
                        if (z > 0) fF[ix * NY + iy] = Mathf.Max(fF[ix * NY + iy], z); else fB[ix * NY + iy] = Mathf.Max(fB[ix * NY + iy], -z);
                        if (x > 0) sR[iz * NY + iy] = Mathf.Max(sR[iz * NY + iy], x); else sL[iz * NY + iy] = Mathf.Max(sL[iz * NY + iy], -x);
                    }
                    float[] Dil(float[] g, int n0) { var o = new float[g.Length]; for (int a = 0; a < n0; a++) for (int b = 0; b < NY; b++) { float mx = 0; for (int da = -1; da <= 1; da++) for (int db = -1; db <= 1; db++) { int aa = a + da, bb = b + db; if (aa >= 0 && aa < n0 && bb >= 0 && bb < NY) mx = Mathf.Max(mx, g[aa * NY + bb]); } o[a * NY + b] = mx; } return o; }
                    bp0.fF = Dil(fF, NX); bp0.fB = Dil(fB, NX); bp0.sR = Dil(sR, NZ); bp0.sL = Dil(sL, NZ);
                    parts.Add(bp0); continue;
                }
                if (hb == HumanBodyBones.Head)
                {
                    var c = Vector3.zero; foreach (var p in P) c += p; c /= P.Count; var d = new List<float>(P.Count); foreach (var p in P) d.Add((p - c).magnitude);
                    bp0.kind = 2; bp0.c = c; bp0.r0 = Pct(d, .97f); parts.Add(bp0); continue;
                }
                // cápsulas: braço→cotovelo, antebraço→pulso, coxa→joelho, canela→tornozelo (ponta = osso filho na pose REAL); mão/pé: ponta fixa
                var endHb = hb switch
                {
                    HumanBodyBones.LeftUpperArm => HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm => HumanBodyBones.RightLowerArm,
                    HumanBodyBones.LeftLowerArm => HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm => HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg => HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg => HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.LeftLowerLeg => HumanBodyBones.LeftFoot, HumanBodyBones.RightLowerLeg => HumanBodyBones.RightFoot, _ => HumanBodyBones.LastBone
                };
                Vector3 endL; var tA = an.GetBoneTransform(hb); var tB = endHb != HumanBodyBones.LastBone ? an.GetBoneTransform(endHb) : null;
                if (tB && idxUse.TryGetValue(tA, out int ia0) && idxUse.TryGetValue(tB, out int ib0)) endL = bpUse[ia0].MultiplyPoint3x4(bpUse[ib0].inverse.GetColumn(3));   // ponta na pose de bind, no espaço do osso
                else if (hb == HumanBodyBones.LeftHand || hb == HumanBodyBones.RightHand || hb == HumanBodyBones.LeftFoot || hb == HumanBodyBones.RightFoot)
                {
                    var mean = Vector3.zero; foreach (var p in P) mean += p; mean /= P.Count; var dir = mean.normalized; if (dir.sqrMagnitude < .5f) continue;
                    var pr = new List<float>(P.Count); foreach (var p in P) pr.Add(Vector3.Dot(p, dir)); endL = dir * Pct(pr, .97f); endHb = HumanBodyBones.LastBone;
                }
                else continue;
                if (endL.sqrMagnitude < 1e-8f) continue;
                var lo = new List<float>(); var hi = new List<float>(); float len2 = endL.sqrMagnitude;
                foreach (var p in P) { float t = Mathf.Clamp01(Vector3.Dot(p, endL) / len2); float dd = (p - endL * t).magnitude; (t < .5f ? lo : hi).Add(dd); }
                bp0.kind = 1; bp0.endHb = endHb; bp0.end = endL; bp0.r0 = Pct(lo.Count > 0 ? lo : hi, .97f); bp0.r1 = Pct(hi.Count > 0 ? hi : lo, .97f); parts.Add(bp0);
            }
            return parts;
        }
        bool EnsureBody()
        {
            if (body != null) return true; if (bodyTried || !an || !an.isHuman) return false; bodyTried = true;
            var sw = System.Diagnostics.Stopwatch.StartNew(); var key = BodyKey();
            if (!bodyCache.TryGetValue(key, out var parts)) { parts = MeasureBind(); bodyCache[key] = parts; MeasureN++; }
            else if (parts != null) InitTorsoAxesFromCache(parts);
            MeasureMs += sw.Elapsed.TotalMilliseconds; if (parts == null) return false;
            body = parts; bodyBone = new Transform[parts.Count]; bodyEnd = new Transform[parts.Count];
            for (int i = 0; i < parts.Count; i++) { bodyBone[i] = an.GetBoneTransform(parts[i].hb); bodyEnd[i] = parts[i].endHb != HumanBodyBones.LastBone ? an.GetBoneTransform(parts[i].endHb) : null; }
            return true;
        }
        // personagem com as mesmas malhas (cache): os eixos do tronco vêm da parte do peito já medida
        void InitTorsoAxesFromCache(List<BodyPart> parts)
        {
            if (tAxes || !an || !chest) return;
            foreach (var p in parts) if (p.kind == 0 && an.GetBoneTransform(p.hb) == chest) { tUpL = p.ay; tRtL = p.ax; tAxes = true; return; }
        }
        /// <summary>Eixos do tronco: da pose de BIND (MeasureBind); antes de medir, do esqueleto atual (chamado no Init).</summary>
        void InitTorsoAxes()
        {
            if (tAxes || !chest || !hips || !neck) return;
            if (EnsureBody() && tAxes) return;
            var u = (neck.position - hips.position).normalized; var r = rArm && lArm ? Vector3.ProjectOnPlane(rArm.position - lArm.position, u) : Vector3.ProjectOnPlane(transform.right, u);
            if (r.sqrMagnitude < 1e-6f) r = Vector3.ProjectOnPlane(transform.right, u);
            tUpL = chest.InverseTransformDirection(u).normalized; tRtL = chest.InverseTransformDirection(r.normalized).normalized; tAxes = true;
        }
        /// <summary>Eixos do tronco (para cima, frente, direita) no mundo, presos ao osso do peito (frente = direita × cima).</summary>
        public void TorsoAxes(out Vector3 up, out Vector3 fw, out Vector3 right)
        {
            InitTorsoAxes();
            if (!tAxes) { up = transform.up; fw = transform.forward; right = transform.right; return; }
            up = chest.TransformDirection(tUpL).normalized; right = Vector3.ProjectOnPlane(chest.TransformDirection(tRtL), up).normalized; fw = Vector3.Cross(right, up);
        }
        /// <summary>Eixos da coxa direita: descida (quadril→joelho), lado de fora (linha dos quadris) e frente da coxa.</summary>
        public bool ThighAxes(out Vector3 dn, out Vector3 side, out Vector3 fwd)
        {
            dn = -transform.up; side = transform.right; fwd = transform.forward; if (!rThigh || !rKnee) return false;
            if (!lThighB && an && an.isHuman) lThighB = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            dn = (rKnee.position - rThigh.position).normalized;
            var s0 = lThighB ? rThigh.position - lThighB.position : transform.right; side = Vector3.ProjectOnPlane(s0, dn).normalized;
            if (side.sqrMagnitude < .5f) side = Vector3.ProjectOnPlane(transform.right, dn).normalized;
            fwd = Vector3.Cross(dn, side); return true;
        }
        /// <summary>Quanto a arma (área comprimento Bd × altura Td em volta de C0) precisa sair na direção n para não tocar nenhuma parte do
        /// corpo na pose ATUAL, já com a folga 'clear' (meia espessura da arma + 2 cm). Mundo.</summary>
        float Solve(Vector3 C0, Vector3 Bd, Vector3 Td, Vector3 n, float L, float W, float clear, int mode = 0)
        {
            float need = 0; const int K = 7, J = 2; float reach = Mathf.Sqrt(L * L + W * W);
            for (int pi = 0; pi < body.Count; pi++)
            {
                var P = body[pi]; var bt = bodyBone[pi]; if (!bt || P.kind == 3) continue; float ls = bt.lossyScale.x;
                // mode 1 = só tronco (costas/peito); mode 2 = tronco + coxa direita (pistola). Braço/mão/cabeça/perna oposta na pose do quadro causavam
                // picos de empurrão (a arma sai na hora e volta devagar = flutuando longe do corpo)
                if (mode == 1 && P.kind != 0) continue; if (mode == 2 && P.kind != 0 && P.hb != HumanBodyBones.RightUpperLeg) continue;
                if (P.kind == 0)
                {
                    var M = bt.worldToLocalMatrix; Vector3 c0 = M.MultiplyPoint3x4(C0), bd = M.MultiplyVector(Bd) * L, td = M.MultiplyVector(Td) * W, nl = M.MultiplyVector(n);
                    float inv = nl.magnitude; if (inv < 1e-9f) continue; nl /= inv;                                    // inv = 1/escala do osso
                    float cz = Vector3.Dot(nl, P.az), cx = Vector3.Dot(nl, P.ax); bool front = Mathf.Abs(cz) >= Mathf.Abs(cx); float c = front ? cz : cx; if (Mathf.Abs(c) < .3f) continue;
                    float[] G = front ? (c > 0 ? P.fF : P.fB) : (c > 0 ? P.sR : P.sL); float clearL = clear * inv, ac = Mathf.Abs(c), cell = P.cell; int NY = P.ny, NA = front ? P.nx : P.nz;
                    for (int k = -K; k <= K; k++) for (int j = -J; j <= J; j++)
                        {
                            var q = c0 + bd * (k / (float)K) + td * (j / (float)J);
                            float x = Vector3.Dot(q, P.ax), y = Vector3.Dot(q, P.ay), z = Vector3.Dot(q, P.az);
                            int iy = Mathf.FloorToInt(y / cell) + P.oy; if (iy < 0 || iy >= NY) continue;
                            int ia = front ? Mathf.FloorToInt(x / cell) + P.ox : Mathf.FloorToInt(z / cell) + P.oz; if (ia < 0 || ia >= NA) continue;
                            float depth = G[ia * NY + iy]; if (depth <= 0) continue;
                            float cur = front ? (c > 0 ? z : -z) : (c > 0 ? x : -x);
                            float d = (depth + clearL - cur) / ac; if (d > 0) need = Mathf.Max(need, d / inv);
                        }
                    continue;
                }
                Vector3 a, b; float r0, r1;
                if (P.kind == 2) { a = b = bt.TransformPoint(P.c); r0 = r1 = P.r0 * ls; }
                else { a = bt.position; b = bodyEnd[pi] ? bodyEnd[pi].position : bt.TransformPoint(P.end); r0 = P.r0 * ls; r1 = P.r1 * ls; }
                var ab = b - a; float ab2 = Mathf.Max(1e-8f, ab.sqrMagnitude);
                // descarte rápido: a parte não alcança a área da arma (mesmo empurrada até 60 cm)
                { float t0 = Mathf.Clamp01(Vector3.Dot(C0 - a, ab) / ab2); if ((C0 - (a + ab * t0)).magnitude > reach + Mathf.Max(r0, r1) + clear + .6f * scaleK) continue; }
                for (int k = -K; k <= K; k++) for (int j = -J; j <= J; j++)
                    {
                        var p = C0 + Bd * (L * k / K) + Td * (W * j / J);
                        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab2); float R = Mathf.Lerp(r0, r1, t) + clear;
                        var v = p - (a + ab * t); float vn = Vector3.Dot(v, n), vp2 = v.sqrMagnitude - vn * vn; if (vp2 >= R * R) continue;
                        float d = -vn + Mathf.Sqrt(R * R - vp2); if (d > 0) need = Mathf.Max(need, d);
                    }
            }
            return need;
        }
        /// <summary>Quanto o ponto (mundo) está DENTRO da frente do tronco, com folga pad — para afastar a arma da mão de corpos largos
        /// (21/10/2026: o fuzil atravessava a barriga do AKA JUNIN). Colunas da frente das partes do tronco (pose de bind).</summary>
        public float FrontPenetration(Vector3 p, float pad)
        {
            if (!EnsureBody()) return 0; float pen = 0;
            for (int pi = 0; pi < body.Count; pi++)
            {
                var P = body[pi]; var bt = bodyBone[pi]; if (P.kind != 0 || !bt) continue; float ls = bt.lossyScale.x; var q = bt.InverseTransformPoint(p);
                int ix = Mathf.FloorToInt(Vector3.Dot(q, P.ax) / P.cell) + P.ox, iy = Mathf.FloorToInt(Vector3.Dot(q, P.ay) / P.cell) + P.oy; if (ix < 0 || ix >= P.nx || iy < 0 || iy >= P.ny) continue;
                float depth = P.fF[ix * P.ny + iy]; if (depth <= 0) continue; pen = Mathf.Max(pen, (depth + pad / ls - Vector3.Dot(q, P.az)) * ls);
            }
            return Mathf.Max(0, pen);
        }
        // [parceiro] Quanto um ponto está dentro do corpo, pelo mesmo modelo do corpo medido da malha que o FrontPenetration usa:
        // tronco (frente, costas e lados de cada osso), cabeça e, com limbs, braços e coxas/canelas (antebraços e mãos ficam
        // de fora porque seguram a arma). Devolve a profundidade em metros (0 = fora), a direção para sair pelo lado mais
        // perto (no tronco: frente, direita ou esquerda, nunca pelas costas) e a parte (0 tronco, 1 braço/perna, 2 cabeça).
        // osso da parte mais funda achada pelo último BodyDepth (para o relatório do teste)
        [System.NonSerialized] public HumanBodyBones DepthBone;
        // [parceiro] Cópia das matrizes do tronco e da cabeça para medir muitos pontos de uma vez (FBRig.KeepOutOfTorso) sem chamar
        // a Unity a cada ponto: BodyDepth com 150 pontos x 7 passadas custava ~1 ms por personagem (20 bonecos = 44 FPS).
        // Vale enquanto tronco e cabeça não se mexem (o braço pode mexer: não é filho deles na medida).
        struct FastPart { public BodyPart p; public Matrix4x4 w2l, l2w; public float scale; public Vector3 c; public float r; }
        FastPart[] fast; int fastN;
        public bool BodyFastBegin()
        {
            fastN = 0; if (!EnsureBody()) return false;
            if (fast == null || fast.Length < body.Count) fast = new FastPart[body.Count];
            for (int i = 0; i < body.Count; i++)
            {
                var P = body[i]; var bt = bodyBone[i]; if (!bt || (P.kind != 0 && P.kind != 2)) continue;
                var fp = new FastPart { p = P, scale = bt.lossyScale.x };
                if (P.kind == 0) { fp.w2l = bt.worldToLocalMatrix; fp.l2w = bt.localToWorldMatrix; }
                else { fp.c = bt.TransformPoint(P.c); fp.r = P.r0 * fp.scale; }
                fast[fastN++] = fp;
            }
            return fastN > 0;
        }
        // profundidade de q (espaço do osso) numa parte do tronco: frente, direita ou esquerda (a saída mais curta; nunca pelas costas)
        static float TorsoDepth(BodyPart P, Vector3 q, out Vector3 dirL)
        {
            dirL = Vector3.zero;
            float lx = Vector3.Dot(q, P.ax), ly = Vector3.Dot(q, P.ay), lz = Vector3.Dot(q, P.az);
            int ix = Mathf.FloorToInt(lx / P.cell) + P.ox, iy = Mathf.FloorToInt(ly / P.cell) + P.oy, iz = Mathf.FloorToInt(lz / P.cell) + P.oz;
            if (ix < 0 || ix >= P.nx || iy < 0 || iy >= P.ny || iz < 0 || iz >= P.nz) return 0;
            float f = P.fF[ix * P.ny + iy], b = P.fB[ix * P.ny + iy], r = P.sR[iz * P.ny + iy], l = P.sL[iz * P.ny + iy];
            if (f <= 0 || b <= 0 || r <= 0 || l <= 0 || lz >= f || lz <= -b || lx >= r || lx <= -l) return 0;
            float d = f - lz; dirL = P.az;
            if (r - lx < d) { d = r - lx; dirL = P.ax; }
            if (lx + l < d) { d = lx + l; dirL = -P.ax; }
            return d;
        }
        // [parceiro] igual ao BodyDepth sem braços e pernas, usando a cópia feita no BodyFastBegin
        public float BodyDepthFast(Vector3 p, out Vector3 outDir)
        {
            outDir = Vector3.zero; float best = 0;
            for (int i = 0; i < fastN; i++)
            {
                var P = fast[i].p;
                if (P.kind == 0)
                {
                    float d = TorsoDepth(P, fast[i].w2l.MultiplyPoint3x4(p), out var dirL) * fast[i].scale;
                    if (d > best) { best = d; outDir = fast[i].l2w.MultiplyVector(dirL).normalized; DepthBone = P.hb; }
                }
                else
                {
                    var v = p - fast[i].c; float d2 = fast[i].r - v.magnitude;
                    if (d2 > best) { best = d2; outDir = v.sqrMagnitude > 1e-8f ? v.normalized : transform.right; DepthBone = P.hb; }
                }
            }
            return best;
        }
        public float BodyDepth(Vector3 p, bool limbs, out Vector3 outDir, out int part)
        {
            outDir = Vector3.zero; part = -1; if (!EnsureBody()) return 0; float best = 0;
            for (int i = 0; i < body.Count; i++)
            {
                var P = body[i]; var bt = bodyBone[i]; if (!bt) continue; float ls = bt.lossyScale.x;
                if (P.kind == 0)
                {
                    float d = TorsoDepth(P, bt.InverseTransformPoint(p), out var dirL) * ls;
                    if (d > best) { best = d; outDir = bt.TransformDirection(dirL).normalized; part = 0; DepthBone = P.hb; }
                    continue;
                }
                Vector3 a, e; float r0, r1;
                if (P.kind == 2) { a = e = bt.TransformPoint(P.c); r0 = r1 = P.r0 * ls; }
                else
                {
                    if (P.kind != 1 || !limbs || P.hb == HumanBodyBones.LeftLowerArm || P.hb == HumanBodyBones.RightLowerArm || P.hb == HumanBodyBones.LeftHand || P.hb == HumanBodyBones.RightHand) continue;
                    a = bt.position; e = bodyEnd[i] ? bodyEnd[i].position : bt.TransformPoint(P.end); r0 = P.r0 * ls; r1 = P.r1 * ls;
                }
                var seg = e - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, seg) / Mathf.Max(1e-8f, seg.sqrMagnitude));
                var v = p - (a + seg * t); float d2 = Mathf.Lerp(r0, r1, t) - v.magnitude;
                if (d2 > best) { best = d2; outDir = v.sqrMagnitude > 1e-8f ? v.normalized : transform.right; part = P.kind == 2 ? 2 : 1; DepthBone = P.hb; }
            }
            return best;
        }
        /// <summary>Frente e cima da cabeça no espaço do osso da cabeça (pose de bind).</summary>
        public bool HeadAxes(out Vector3 fwL, out Vector3 upL)
        {
            fwL = upL = Vector3.zero; if (!EnsureBody()) return false;
            foreach (var p in body) if (p.kind == 3) { fwL = p.az; upL = p.ay; return true; }
            return false;
        }
        bool h_isWeapon(Transform t) { for (; t && t != transform; t = t.parent) if ((weaponGO && t == weaponGO.transform) || t.name.StartsWith("Coldre_")) return true; return false; }
        public void HolsterRemeasure() { body = null; bodyTried = false; }                                   // troca de roupa (FBWardrobe)
        int holsterFrame;
        void PlaceHolsters()
        {
            bool showCur = (drawHid || stowCur) && weapon != null && curModel;
            if (showCur && cur == null) cur = MakeHolster(weapon, curModel);
            if (cur != null) cur.go.SetActive(showCur && alive && !Sitting);
            if ((stowed.Count == 0 && cur == null) || !chest || !hips || !neck) return;
            float s = transform.lossyScale.y / FBConst.ModelScale;
            float ws = HolsterWorldScale; bool solid = EnsureBody();
            TorsoAxes(out var up, out var fw, out var right); bool thighOk = ThighAxes(out var dn, out var side, out var tf);
            // largura real do corpo pelos ossos (ombros e quadris): base do limite de afastamento e do fallback das armas guardadas
            float shW = rArm && lArm ? Vector3.Distance(rArm.position, lArm.position) : .4f * s, hipW = rThigh && lThighB ? Vector3.Distance(rThigh.position, lThighB.position) : .2f * s;
            shW = Mathf.Clamp(shW, .2f * s, .9f * s); hipW = Mathf.Clamp(hipW, .1f * s, .45f * s);
            var all = new List<Holstered>(stowed.Values); if (cur != null && showCur) all.Add(cur);
            float gap = .02f * s, dt = Time.deltaTime;                                                       // folga entre a arma e o corpo (2 cm)
            // bot longe da câmera: resolve 1 quadro em 4 (a arma segue presa ao osso nos outros)
            var cam = Camera.main; bool far = !isPlayer && cam && (cam.transform.position - transform.position).sqrMagnitude > 25 * 25;
            bool solveNow = !far || ((holsterFrame++ + GetHashCode()) & 3) == 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var h in all)
            {
                // poses extremas: deitado de costas sendo atendido (arma entre as costas e o chão) e curando agachado fundo (a SMG do peito
                // ficaria entre o peito e os joelhos) — a arma afetada some durante a pose; dança/gesto de corpo inteiro também
                bool supine = sys == FBAnimProfile.DownedTreated || sys == FBAnimProfile.DownedGetUp, squat = sys == FBAnimProfile.HealCrouch;
                // caído de bruços: a arma do peito ficaria entre o corpo e o chão
                bool vis = alive && !Sitting && !vehiclePose && !supine && !((squat || knocked || prone) && h.slot == "sling") && dance == null;
                if (h != cur) h.go.SetActive(vis); else h.go.SetActive(vis && h.go.activeSelf); if (!vis) { h.dOk = false; continue; }
                float wsh = ws * h.fix; Vector3 C0, Bd, Td, n; float L = h.half * wsh, th = h.thick * wsh, W = h.wide * wsh; Transform bone; float fallback;
                if (h.slot == "thigh" && thighOk) { Bd = dn; Td = tf; bone = rThigh; n = side; C0 = rThigh.position + dn * .2f * s - tf * .02f * s; fallback = .135f * s; }
                else if (h.slot == "sling") { Bd = (-up * .55f - right * .84f).normalized; Td = Vector3.Cross(Bd, fw); bone = chest; n = fw; C0 = chest.position - up * .2f * s - right * .02f * s; fallback = .2f * s; }   // web: -(B×fw); o espelho three.js→Unity inverte o produto vetorial
                else { Bd = (-up * .72f - right * .7f).normalized; Td = -Vector3.Cross(Bd, fw); bone = chest; n = -fw; C0 = chest.position - up * .04f * s; fallback = .2f * s; }
                Td = Vector3.ProjectOnPlane(Td, Bd).normalized;
                // afastamento do eixo do osso: tronco ≈ meia profundidade (~0,3 × ombros), coxa ≈ raio (~0,45 × quadris); o limite máximo é a
                // largura do corpo (0,5 × ombros / 0,6 × quadris) + meia espessura da arma + folga — nunca flutua longe, mesmo se o solver errar
                bool isThigh = h.slot == "thigh" && thighOk;
                // r60: teto APERTADO. O solver afasta a arma até ela limpar a malha do corpo — e roupa volumosa entra nessa conta. Em
                // personagem de casaco longo e esvoaçante (r_miguel) o fuzil das costas e a pistola da coxa iam parar no ar, com vão
                // visível; em personagem de roupa normal (r_rigote) a mesma pose fica certa. Como isto é um TETO, baixá-lo só mexe em
                // quem estava encostando nele: quem já está bem não muda. Arma levemente enfiada no casaco lê muito melhor que arma
                // flutuando a 20 cm das costas. Medido nas folhas de costas em 07/11 (antes .5/.6; ver RODADA60.md).
                float cap = (isThigh ? .42f * hipW : .32f * shW) + th + gap; fallback = (isThigh ? .38f * hipW : .26f * shW) + th + gap;

                // Se houve calibracao visual (Play -> Pause -> arrastar -> Salvar),
                // a pose local gravada substitui a pose automatica deste coldre.
                // Os demais continuam usando o algoritmo original de posicionamento.
                if (TryManualHolster(h, out var saved))
                {
                    if (h.go.transform.parent != bone) h.go.transform.SetParent(bone, false);
                    h.go.transform.localPosition = saved.localPosition;
                    h.go.transform.localRotation = saved.localRotation;
                    // r49: escala gravada igual à padrão antiga (0,6175: só a posição foi ajustada) segue a correção da escala; escala mudada à mão fica
                    h.go.transform.localScale = Mathf.Abs(saved.localScale.x - .6175f) < .002f ? saved.localScale * HandK * h.fix : saved.localScale;
                    h.dOk = false;

                    // Opcional por preset: calcula apenas um afastamento adicional para
                    // evitar intersecoes durante animacoes diferentes da pose calibrada.
                    // Bots distantes reutilizam a ultima medida nos quadros intermediarios.
                    if (saved.preventClipping && solid)
                    {
                        if (solveNow || !h.manualClearanceReady)
                        {
                            float currentWorldScale = Mathf.Abs(h.go.transform.lossyScale.x);
                            Vector3 center = h.go.transform.TransformPoint(h.c);
                            Vector3 barrelDir = h.go.transform.TransformDirection(h.b).normalized;
                            Vector3 topDir = h.go.transform.TransformDirection(h.t).normalized;
                            h.manualClearance = Solve(center, barrelDir, topDir, n,
                                h.half * currentWorldScale, h.wide * currentWorldScale,
                                h.thick * currentWorldScale + gap);
                            h.manualClearanceReady = true;
                            SolveN++;
                        }
                        if (h.manualClearance > 0) h.go.transform.position += n * h.manualClearance;
                    }
                    else h.manualClearanceReady = false;
                    continue;
                }
                if (solid && (solveNow || !h.dOk))
                {
                    float need = Solve(C0, Bd, Td, n, L, W, th + gap, isThigh ? 2 : 1); need = float.IsNaN(need) || float.IsInfinity(need) ? fallback : Mathf.Clamp(need, th + gap, cap); SolveN++;
                    // sai na hora (nunca entra no corpo), volta devagar
                    h.d = !h.dOk || need > h.d ? need : Mathf.Lerp(h.d, need, 1 - Mathf.Exp(-dt * 4)); h.dOk = true;
                }
                else if (!solid) h.d = fallback;
                var Pp = C0 + n * h.d;
                if (h.go.transform.parent != bone) h.go.transform.SetParent(bone, true);                    // presa ao osso: segue a animação nos quadros sem cálculo
                var q = Quaternion.LookRotation(Bd, Td) * Quaternion.Inverse(Quaternion.LookRotation(h.b, h.t));
                h.go.transform.rotation = q; h.go.transform.localScale = Vector3.one * wsh / Mathf.Max(1e-5f, bone.lossyScale.x);
                h.go.transform.position = Pp - q * (h.c * wsh);
            }
            SolveMs += sw.Elapsed.TotalMilliseconds;
        }

        // sacar / guardar (clipes do IE na camada Emote Upper Body; 'grab'/'rel' = instante em que a mão pega/solta a arma)
        // Acelerados por drawSpeed / putSpeed para resposta rápida e competitiva na trocação de tiro
        float drawT = -1, drawAt, drawLen, drawSpd = 1f; bool drawing, drawHid;
        public void DrawAnim()
        {
            if (weapon == null || !FBAnimProfile.Holsters.TryGetValue(HolsterSlot(weapon.cls), out var H)) return;
            drawSpd = H.drawSpeed > .1f ? H.drawSpeed : 1f;
            if (!PlayEmote(H.draw, drawSpd)) return;
            drawing = true; drawT = 0; drawAt = H.grab; drawLen = FBRegistry.I.Clip(H.draw).length;
            drawHid = true; if (weaponGO) weaponGO.SetActive(false);
        }
        public float HolsterAnim()
        {
            if (weapon == null || !alive || !FBAnimProfile.Holsters.TryGetValue(HolsterSlot(weapon.cls), out var H)) return 0;
            drawSpd = H.putSpeed > .1f ? H.putSpeed : 1f;
            if (!PlayEmote(H.put, drawSpd)) return 0;
            drawing = false; drawT = 0; drawAt = H.rel; drawLen = FBRegistry.I.Clip(H.put).length;
            return (H.rel / drawSpd) + .02f;
        }

        // ---------------- reanimar aliado (Sigma: família CURE do Free Fire — Crouch_Self_Rescue Start/Loop/End), pose de sistema de corpo
        // inteiro; o FBPlayer/FBBot/laboratório chamam a cada quadro e põem o resultado em 'sys'. Antes: clipe do IE (anim_Reviver).
        float rescueT = -1, rescueEndT = -1;
        public string RescuePose(bool on, float dt)
        {
            float Len(string c) { var k = FBRegistry.I ? FBRegistry.I.Clip(c) : null; return k ? k.length : 0; }
            if (on) { rescueEndT = -1; if (rescueT < 0) rescueT = 0; rescueT += dt; return Has(FBAnimProfile.ReviveStart) && rescueT < Len(FBAnimProfile.ReviveStart) ? FBAnimProfile.ReviveStart : Has(FBAnimProfile.ReviveLoop) ? FBAnimProfile.ReviveLoop : null; }
            if (rescueT >= 0) { rescueT = -1; rescueEndT = 0; }
            if (rescueEndT >= 0) { rescueEndT += dt; if (rescueEndT < Len(FBAnimProfile.ReviveEnd) - .05f && Has(FBAnimProfile.ReviveEnd)) return FBAnimProfile.ReviveEnd; rescueEndT = -1; }
            return null;
        }
        public bool Rescuing => rescueT >= 0 || rescueEndT >= 0;
        public bool DrawBusy => drawing ? (drawT >= 0 && drawT < drawAt) : (drawT >= 0 && emoW > .25f);
        public bool IsDrawing => drawing && drawT >= 0 && drawT < drawAt;
        // [parceiro] Correção: depois que a mão pega a arma, a animação de sacar ainda traz a arma das costas (ou do coldre) para a frente.
        // A mira e a mão esquerda entravam logo nessa hora e giravam a arma para a frente com a mão ainda no ombro,
        // e o cano atravessava o peito. Agora elas só entram quando a animação de sacar está terminando (HandsBusy).
        public static bool DrawFollowAnim = true;
        bool DrawSettling => DrawFollowAnim && drawing && drawT >= drawAt && emoW > .35f;

        // ---------------- emotes / danças / poses de sistema
        float emoteT = -1, emoteLen, emoteSpd = 1f;
        public bool PlayEmote(string clip, float speed = 1f)                                      // camada Emote Upper Body
        {
            if (LEU < 0 || !Has(clip)) return false;
            emoteSpd = speed > .01f ? speed : 1f;
            SF("EmoteSpeed", emoteSpd);
            emoteB = !emoteB; SetOverride(emoteB ? "EMOTE_UPPER_PLACEHOLDER_B" : "EMOTE_UPPER_PLACEHOLDER", clip);
            an.CrossFadeInFixedTime(emoteB ? "EmoteB" : "Emote", emoteT >= 0 ? .12f : 0, LEU, 0); emoteT = 0; emoteLen = FBRegistry.I.Clip(clip).length; return true;
        }
        string fullClip;
        void PlayFull(string clip, float fade)
        {
            if (fullClip == clip) return; fullClip = clip; if (clip == null || LE < 0) return;
            fullB = !fullB; SetOverride(fullB ? "EMOTE_FULL_PLACEHOLDER_B" : "EMOTE_FULL_PLACEHOLDER", clip);
            an.CrossFadeInFixedTime(fullB ? "EmoteB" : "Emote", danW > .05f ? fade : 0, LE, 0);
        }
        public bool PlayDance(string clip, string sitEnter = null, string sitOut = null)
        {
            if (LE < 0 || !Has(clip) || !alive || FBEmotes.IsFF(clip)) return false;   // r47: emote FF não toca nem vindo da rede (cliente antigo)
            StopWaitPose(); var c = FBRegistry.I.Clip(clip);
            bool emote = !clip.StartsWith("FF_SYS_") && !clip.StartsWith("REB_");
            string first = clip; float len = emote ? Mathf.Max(c.length * 2, 6) : c.isLooping ? 20 : c.length; var chain = new Queue<(string, bool)>();
            if (sitEnter != null && Has(sitEnter)) { first = sitEnter; len = FBRegistry.I.Clip(first).length; chain.Enqueue((clip, true)); }
            else if (sitEnter != null) len = 1e9f;
            fullClip = null; PlayFull(first, .25f);
            dance = new Dance { clip = first, len = len, chain = chain, sitOut = sitOut != null && Has(sitOut) ? sitOut : null };
            emoteT = -1; SetWeaponVisible(false); return true;
        }
        bool ChainDance(string clip, bool hold)
        {
            if (!Has(clip) || dance == null) return false; fullClip = null; PlayFull(clip, .25f);
            dance.clip = clip; dance.t = 0; dance.len = hold ? 1e9f : FBRegistry.I.Clip(clip).length; return true;
        }
        public void StopDance(bool force = false)
        {
            var d = dance; if (d == null) return;
            if (!force && alive && d.sitOut != null)
            {
                if (d.leaving) return;
                if (d.chain.Count == 0) { d.leaving = true; if (ChainDance(d.sitOut, false)) return; }
            }
            dance = null; SetWeaponVisible(!UnarmedLocomotion);
        }
        // corta o clipe de dança/reação NA HORA (sem mistura): usado quando o clipe andou com o corpo e a raiz foi levada até ele (FBRoleNpc)
        // r46: clipe de corpo inteiro com peso cheio NO MESMO QUADRO (levantar depois do ragdoll: quem chama faz a transição com FBPoseBlend)
        public void ForceFull(string clip) { if (LE < 0 || !Has(clip) || !an) return; sys = clip; dance = null; fullClip = null; danW = 0; PlayFull(clip, 0); danW = 1; an.SetLayerWeight(LE, 1); }
        public void CutDance() { dance = null; danW = 0; if (LE >= 0 && an) an.SetLayerWeight(LE, 0); SetWeaponVisible(!UnarmedLocomotion); }
        public bool PlayWaitPose(string clip) { if (LE < 0 || !Has(clip) || !alive || special || dance != null) return false; waitPose = clip; return true; }
        public void StopWaitPose() { waitPose = null; }
        [System.NonSerialized] public bool vehiclePose, fpHeadOn; [System.NonSerialized] public FBCar fitCar; [System.NonSerialized] public float seatDrop, seatDropT, fitT;   // banco do veículo (FBDrive.PoseActor)

        // ---------------- especiais (Finisher)
        float specT = -1, specLen; System.Action specEnd; [System.NonSerialized] public bool keepFinal;
        public bool PlaySpecial(string clip, float dur = -1, System.Action onEnd = null)
        {
            if (LF < 0 || !Has(clip)) return false;
            SetOverride("FINISHER_PLACEHOLDER", clip); an.Play("Finisher", LF, 0);
            specT = 0; specLen = dur > 0 ? dur : FBRegistry.I.Clip(clip).length; specEnd = onEnd; finTarget = 1; special = true; return true;
        }

        // ---------------- eventos
        public void Trigger(string n, int idx = -1)
        {
            if (n == "reload") ST("Reload"); else if (n == "inspect") ST("Inspect"); else if (n == "grenade") ST("GrenadeThrow");
            else if (n == "hit")
            {
                if (hitCd > 0 || Random.value > PUP("hitReactionChance", .8f)) return;
                hitCd = PUP("hitReactionCooldown", .35f); SF("HitIndex", idx >= 0 ? idx : Random.Range(0, (int)PUP("numberOfHitAnimations", 4))); ST("Hit");
            }
        }
        public void Damage(float dmg, FBCharacter by, bool head) => FBCombat.DamageActor(this, dmg, head, by);
        public void Die(FBCharacter by)
        {
            if (!alive) return; alive = false; hp = 0; deaths++;
            // r42: colisores do corpo (hitbox) desligados ANTES da morte — o FBDeath pode montar o ragdoll já aqui (explosão/no ar) e
            // desligar depois apagava a colisão dele: o corpo atravessava o chão (autoteste -fbmorte: quadril a −153 m)
            foreach (var c in GetComponentsInChildren<Collider>()) if (!(c is CharacterController) && c.gameObject.layer != FBRagdoll.Layer) c.enabled = false;   // r46: morreu caindo (ragdoll vivo): as peças dele seguem
            int i = Random.Range(0, (int)PUP("numberOfDeathAnimations", 4)); deathClip = null;
            if (keepFinal) { finTarget = 1; special = false; }
            else if (LF >= 0 && !vehiclePose && (deathClip = FBDeath.Begin(this)) != null) { SetOverride("FINISHER_PLACEHOLDER", deathClip); an.Play("Finisher", LF, 0); finW = 0; finTarget = 1; }   // r42: queda direcional + ragdoll
            else if (FBRagdoll.Of(this) is FBRagdoll rgd && (rgd.Simulating || rgd.enabled)) { finTarget = 0; }   // ragdoll direto (explosão, no ar, sem espaço)
            else if (hasDeathState) { SF("DeathIndex", i); ST("Death"); }
            else if (LF >= 0 && Has(DEATHS[i])) { SetOverride("FINISHER_PLACEHOLDER", DEATHS[i]); an.Play("Finisher", LF, 0); finW = 0; finTarget = 1; }
            StopDance(true); sys = null;
            onDeath?.Invoke(this, by);
        }
        public void Revive(float h = 100)
        {
            FBWoundFx.ClearCharacter(this);                                     // ferimentos do corpo somem ao renascer (Agente 6)
            var rgd0 = GetComponent<FBRagdoll>(); if (rgd0) rgd0.Clear(); lastHit = default; lastWhy = null; lastHitHead = false; lastHitFrom = null;
            hp = h; alive = true; knocked = false; prone = false; sys = null; keepFinal = false; special = false; specT = -1;
            an.Rebind(); curOver.Clear(); SetUnarmedLocomotion(UnarmedLocomotion, true); if (weapon != null) SetFireMode(weapon.auto);
            finW = finTarget = 0; fullClip = null; dance = null;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
            SetWeaponVisible(!UnarmedLocomotion);
        }

        // ---------------- por quadro (animate do actor.js)
        [System.NonSerialized] public Vector2? animVel;                    // velocidade local já remapeada (bots: remapSpeed)
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update/LateUpdate de todos os personagens
        static readonly int __fbt_Update = FBTempo.Id("FBCharacter.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0 || !an || !an.runtimeAnimatorController) return;
            float g = alive ? new Vector2(velocity.x, velocity.z).magnitude : 0;
            var lv = transform.InverseTransformDirection(velocity); lv /= Mathf.Max(.01f, scaleK);
            float lx = animVel?.x ?? lv.x, lz = animVel?.y ?? lv.z;
            float k = 1 - Mathf.Exp(-dt / .1f); vx += ((alive ? lx : 0) - vx) * k; vz += ((alive ? lz : 0) - vz) * k;
            SF("VelX", vx); SF("VelZ", vz); SF("Speed", new Vector2(vx, vz).magnitude);
            airT = grounded ? 0 : airT + dt; bool inAir = alive && !grounded && airT > .2f;
            SB("InAir", inAir); SB("IsGrounded", !inAir); SF("VerticalVelocity", velocity.y);
            if (inAir && !wasAir && velocity.y > PUP("jumpVelocityThreshold", 2)) ST("JumpStart"); if (!inAir && wasAir) ST("JumpLand"); wasAir = inAir;
            if (IsPolice && !UnarmedLocomotion && (sprint != polSprint || crouch != polCrouch)) LocoOverrides();   // policial: corrida/mira agachado por direção
            else if (!IsPolice && !UnarmedLocomotion && crouch != novoCrouch && FBAnimProfile.CombateNovo(skinId)) { novoCrouch = crouch; LocoOverrides(); }   // r58: mira agachado
            bool cr = crouch && alive; SB("IsCrouching", cr); SF("CrouchAlpha", cr ? 1 : 0); if (cr && !wasCrouch) ST("CrouchTrigger"); wasCrouch = cr;
            bool bare = UnarmedLocomotion; bool busy = !bare && (shooting || aiming || reloading);
            stillT = g < PUP("relaxedMoveStillThreshold", .1f) && !busy && !cr ? stillT + dt : 0;
            bool rel = bare;                                                       // armado relaxado = arma baixa na postura de combate (FBRig), não a árvore relaxada
            SB("IsRelaxed", rel && alive);
            Hot = aiming || shooting || reloading || lastShot < PUP("combatFlagDuration", 3) || combatHold > 0; combatHold -= dt;
            // fora da postura de combate em veículo e em pose de sistema (cura/reanimar/caído): antes da rodada 37 o "hot" desligava isso por
            // acaso; com o alwaysInCombat a camada de braços com arma ficava por cima da pose de pilotar (braço sumindo na moto)
            // 22/10 (teste do usuário: "a animação de correr está horrível, a que tínhamos antes era melhor"): andando FORA de ação volta a
            // PatrolLocomotion (trote do IE com a arma baixa, como antes da rodada); em ação (mira/tiro/recarga/3 s depois do tiro) ou parado
            // fica a postura de combate do Complexo (parado = arma baixa pelo FBRig, sem os "braços de zumbi" da árvore de patrulha)
            bool movingArmed = g > .6f;
            CombatStance = !bare && combat && !sprint && alive && !vehiclePose && sys == null && (Hot || !movingArmed); SB("InCombat", CombatStance);
            SB("IsShooting", alive && !bare && shooting && !IsPistol);
            if (lastShot < lastShotPrev - 1e-4f) kick = Mathf.Min(1, kick + (IsPistol ? .85f : .35f)); lastShotPrev = lastShot; kick = Mathf.Max(0, kick - dt * (IsPistol ? 9 : 12));
            // [mãos 09/11] ANDANDO em postura de combate sem mirar (os 3 s depois de atirar/mirar/recarregar): a base é o andar de combate do
            // Complexo (Rifle_WalkFwdLoop e vizinhos), que leva o fuzil ALTO e inclinado para cima. Para pôr o cano no alvo o FBRig dobrava a
            // coluna ~22° para a frente e descia os braços no ombro: corpo corcunda, braços esticados retos e a coronha fora do ombro (fotos do
            // laboratório, folha fuzil_andar_combate; com -fblabraw o clipe cru aparece com o cano ~25° acima). Mirando e andando a camada
            // Upper Body entra com a pose de mira e fica certo (folha fuzil_mirando_andando). Então nesse estado a camada de cima usa a MESMA
            // pose de mira — só o Animator; ch.aiming (zoom, precisão, cabeça) não muda. Pistola fica como estava (braços pelo IK das duas mãos).
            bool poseMira = aiming || (CombatStance && movingArmed && !IsPistol && !reloading);
            SB("IsRunning", sprint && !reloading); SB("IsAiming", !bare && poseMira); SB("Tactical Sprint", false);
            float brk = PUP("brakeSpeedThreshold", 1.5f);
            if (lastSpeed > brk * 2 && g < brk && !sprint && wasSprint) { ST("StopSprint"); ST("Brake"); }
            wasSprint = sprint; lastSpeed = g; hitCd = Mathf.Max(0, hitCd - dt); lastShot += dt;
            // Upper Body: só em ações — sobe rápido, desce devagar (puppet original)
            if (LU >= 0)
            {
                var st = an.GetCurrentAnimatorStateInfo(LU); reloading = st.IsName("Reload Anim");
                bool idleUp = st.IsName("Idle") || st.IsName("Tactical Sprint") || (bare && st.IsName("Relaxed Upper"));
                bool want = (!bare && (poseMira || (shooting && !IsPistol) || reloading)) || (inAir && !bare) || !idleUp;
                upperW = Mathf.Clamp01(upperW + (want ? PUP("upperBodyWeightUpSpeed", 20) : -PUP("upperBodyWeightDownSpeed", 8)) * dt); an.SetLayerWeight(LU, upperW);
            }
            // Finisher (morte / especiais)
            if (specT >= 0) { specT += dt; if (specT >= specLen) { specT = -1; var cb = specEnd; specEnd = null; special = false; finTarget = alive ? 0 : 1; cb?.Invoke(); } }
            finW = Mathf.Lerp(finW, finTarget, 1 - Mathf.Exp(-dt * 12)); if (LF >= 0) an.SetLayerWeight(LF, finW);
            // gesto / sacar-guardar
            if (emoteT >= 0)
            {
                emoteT += dt * emoteSpd;
                if (emoteT > emoteLen - .2f || !alive || (shooting && (!drawing || drawT >= drawAt))) emoteT = -1;
            }
            emoW = Mathf.Lerp(emoW, emoteT >= 0 ? 1 : 0, 1 - Mathf.Exp(-dt * (emoteSpd > 1.2f ? 12 : 10))); if (LEU >= 0) an.SetLayerWeight(LEU, emoW);
            // dança / sentar / pose de espera / pose de sistema (corpo inteiro)
            if (dance != null)
            {
                dance.t += dt;
                if (!alive || special || shooting) StopDance(true);
                else if (dance.t > dance.len - .2f) { if (dance.chain.Count > 0) { var (c, h) = dance.chain.Dequeue(); ChainDance(c, h); } else StopDance(true); }
            }
            if (waitPose != null && (!alive || special || shooting || (!vehiclePose && g > .22f))) StopWaitPose();
            string want2 = alive && sys != null && Has(sys) ? sys : dance != null ? dance.clip : waitPose;
            bool vlt = want2 == "FF_SYS_Vault";                                  // pulo de janela: entra e sai rápido (o pulo inteiro dura ~0,6 s)
            if (want2 != null) { if (sysFade > 0 && want2 == sys && fullClip != want2) { PlayFull(want2, sysFade); sysFade = -1; } else PlayFull(want2, vlt ? .05f : .3f); } else fullClip = null;
            danW = Mathf.Lerp(danW, want2 != null ? 1 : 0, 1 - Mathf.Exp(-dt * (vlt ? 40 : vaultTail ? 25 : want2 == null && g > .6f ? 18 : sys != null ? 10 : 8))); if (LE >= 0) an.SetLayerWeight(LE, danW);
            // sacar/guardar: a arma troca de lugar no instante em que a mão chega ao coldre
            if (drawT >= 0)
            {
                drawT += dt * drawSpd; bool hide = drawing ? drawT < drawAt : drawT >= drawAt; drawHid = hide;
                // [parceiro 31/10] em veículo (vehiclePose) o saque/guarda não religa a arma da mão
                if (weaponGO && dance == null && !vehiclePose) weaponGO.SetActive(!hide);
                bool cancelEarly = drawing && drawT >= drawAt && (shooting || (aiming && emoW < .5f));
                if (drawT > drawLen || !alive || cancelEarly)
                {
                    drawT = -1; drawHid = false;
                    if (drawing && weaponGO && !vehiclePose) weaponGO.SetActive(true);
                    if (cancelEarly) emoteT = -1;
                }
            }
            // civis: parâmetros do PedestrianAnimator original
            if (civ) { SB("Surrender", surrender && alive); SB("Talk", talking && alive); SB("Flee", fleeing); }
            // NPC: ao parar sorteia o ciclo de parado deste personagem (não repete o último)
            bool still = g < .12f && grounded;
            if (bare && npcSet != null && still && !wasStill && dance == null)
            {
                var L = npcSet.idle.FindAll(n => Has(n) && n != npcIdleLast); var n2 = L.Count > 0 ? L[Random.Range(0, L.Count)] : npcSet.idle.Find(Has);
                if (n2 != null) { SetOverride("Idle", n2); npcIdleLast = n2; }
            }
            wasStill = still;
            // passos: ritmo do ciclo acompanha a velocidade no chão (rs dos clipes do conjunto atual) — só fora de ações de corpo inteiro
            // speedMatch do actor.js: ritmo do passo = velocidade real no chão ÷ velocidade natural dos clipes que ESTÃO tocando na camada
            // base (pesos reais da blend tree), em metros: rs (unidades normalizadas do Humanoid, clipRS/averageSpeed) × humanScale × escala.
            // Antes: rs cru (sem o ×humanScale×2,2 ≈ 1,25 do worldScale do web → pés ~25% acelerados), faixas de velocidade no lugar dos
            // pesos da blend tree e valores chutados para os clipes LPSP; e an.speed acelerava também recarga/tiro do Upper Body
            float rs = BaseRootSpeed() * an.humanScale * transform.lossyScale.y;
            LocoRate = g > .25f && rs > .35f && danW < .05f && finW < .05f ? Mathf.Clamp(g / rs, .7f, 1.45f) : 1;   // além disso o passo fica acelerado/arrastado demais: melhor escorregar um pouco
            if (PN("FB_LocoSpeed") != null) { SF("FB_LocoSpeed", LocoRate); an.speed = 1; } else an.speed = LocoRate;   // controller antigo: Animator inteiro
            if (VaultRate > 0 && sys == "FF_SYS_Vault") an.speed = VaultRate;                                         // pulo de janela: clipe inteiro no tempo do pulo
        }
        [System.NonSerialized] public float LocoRate = 1, VaultRate;
        static readonly List<AnimatorClipInfo> clipInfo = new(); static readonly Dictionary<AnimationClip, float> rsCache = new(); static Dictionary<string, object> measuredRS;
        // velocidade natural (unidades normalizadas do Humanoid): rs do clipe "no lugar" (clipRS) ou a deriva da raiz medida no import antes de
        // ser removida (Resources/FBClipRS.json, FBAnimImport) — o clipe importado já não anda, então averageSpeed daria 0
        public static float RootSpeedOf(AnimationClip c)
        {
            if (!c) return 0; if (rsCache.TryGetValue(c, out var v)) return v;
            if (measuredRS == null) { var ta = Resources.Load<TextAsset>("FBClipRS"); measuredRS = ta ? Json.Parse(ta.text) as Dictionary<string, object> ?? new() : new(); }
            if (!GameData.ClipRS.TryGetValue(c.name, out v))
            {
                if (measuredRS.TryGetValue(c.name, out var m)) v = Json.Fv(m);
                else { var a = c.averageSpeed; v = c.isLooping ? new Vector2(a.x, a.z).magnitude : 0; }   // import antigo (deriva ainda no clipe)
            }
            return rsCache[c] = v;
        }
        // velocidade natural ponderada (estado atual e o próximo, pelo progresso da transição)
        float BaseRootSpeed()
        {
            float sum = 0, ws = 0, tr = an.IsInTransition(0) ? an.GetAnimatorTransitionInfo(0).normalizedTime : 0;
            void Acc(float k) { if (k <= 0) return; foreach (var ci in clipInfo) { sum += RootSpeedOf(ci.clip) * ci.weight * k; ws += ci.weight * k; } }
            an.GetCurrentAnimatorClipInfo(0, clipInfo); Acc(1 - tr);
            if (tr > 0) { an.GetNextAnimatorClipInfo(0, clipInfo); Acc(tr); }
            return ws > 1e-4f ? sum / ws : 0;
        }

        // ---------------- pós-animação (por osso, na ordem do actor.js; mira do tronco / mão esquerda = FBRig)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBCharacter.LateUpdate");   // [parceiro 01/11] FBTempo (ver Update)
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            float dt = Time.deltaTime; bool spc = special || !alive || danW > .05f || vehiclePose;
            if (an && !an.enabled) return;                                        // ragdoll (morte ou r46: queda viva): a física manda nos ossos (FBRagdoll)
            // ([parceiro] fisica 31/10: chegou à mesma regra — só o FBRagdoll desliga o Animator; não escreve rig/mãos/lean num ragdoll vivo)
            bool handsBusy = HandsBusy;
            if (weaponGO && dance == null) { weaponGO.transform.localRotation = proxyQ; weaponGO.transform.localPosition = proxyP; }
            // (rodada 37) saiu a abertura de 12° nos ombros do desarmado: era para os clipes finos do IE; o conjunto do Complexo (Mixamo) já
            // tem os braços do corpo certo — somada, deixava os braços abertos e rígidos ("boneco")
            // r52: inclinar ANTES da mira do tronco. Antes (como no web) o FBRig apontava o cano e a inclinação vinha depois: a rolagem de 40°, o
            // passo lateral e o giro de 12,6° do Q tiravam a arma do alvo (o cano apontava para o lado; o tiro sai do cano até o ponto mirado,
            // então só o visual errava). Agora a coluna já inclinada é a que mira: a arma acompanha a inclinação. -fbespreitarantigo = antes
            if (LeanFirst) Lean(dt, spc);
            rig?.Aim(dt);
            if (!LeanFirst) Lean(dt, spc);
            // recuo procedural: braço direito sobe e volta (pistola: estalo curto; fuzil: só um tranco leve)
            if (kick > .01f && rFore && alive && !spc) rFore.Rotate(transform.right, -kick * kick * (IsPistol ? .22f : .06f) * Mathf.Rad2Deg, Space.World);
            // (rodada 37) saiu o "low ready" que girava a ARMA solta na mão (arma atravessada, mão esquerda longe): o descanso agora é a própria
            // postura de combate com os braços baixando no ombro (FBRig.Aim, lowerS) — as duas mãos continuam na arma
            // deitado: fuzil alinhado à frente do corpo, quase rente ao chão
            if (danW > .05f && prone && weaponGO && weaponGO.activeSelf)
            {
                var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(BarrelDir(), (transform.forward + Vector3.down * .06f).normalized), danW); weaponGO.transform.rotation = q * weaponGO.transform.rotation;
            }
            // mão esquerda / dedos: depois de recuo, descanso e deitado — pega a arma onde ela realmente ficou
            rig?.Hands(dt);
            ProneBody(dt);                                                 // r49: deitado — chão inclinado, parede, mãos da cura
            // pistola nos clipes do IE (andar/correr/sprint): o punho gira até o cano ficar à frente, um pouco para baixo
            pwW = Mathf.Lerp(pwW, IsPistol && sprint && weaponGO && weaponGO.activeSelf && alive && !spc && !handsBusy ? 1 - upperW : 0, 1 - Mathf.Exp(-dt * 10));   // só no sprint (uma mão); andando = duas mãos (FBRig)
            if (pwW > .01f && rHand)
            {
                var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(BarrelDir(), (transform.forward + Vector3.up * (sprint ? -.75f : -.3f)).normalized), pwW);
                rHand.rotation = q * rHand.rotation;
            }
            if (feet) feet.Tick(dt);                                       // pés depois do passo lateral da inclinação, antes do coldre da coxa
            DanceGround(dt);
            VaultInPlace();
            PlaceHolsters();
            // r57: o rig novo é o ÚLTIMO a escrever — depois da arma, do FBRig, dos pés e dos coldres. É a ordem que a r42
            // mostrou ser obrigatória: camada que roda antes da arma deixa a mão segurando o nada.
            fpsaf?.Avaliar(dt);
        }
        // r49 (pedido 30/10: "manter o alinhamento com o chão; curar deitado sem levantar automaticamente nem deformar os braços"):
        //  · rampa/calçada inclinada: a pose deitada é plana e o quadril fica no centro da cápsula — em ladeira a cabeça entrava no chão de um
        //    lado e os pés flutuavam do outro. Mede o chão 0,8 m à frente/atrás e 0,25 m dos lados e gira o corpo no quadril (até 25°/15°);
        //  · parede na frente/atrás (FBPlayer.ProneRoom): o corpo desliza para longe dela (ProneShift, já pesado pelo quanto está deitado);
        //  · curando deitado: mãos juntas na frente do rosto, apoiadas nos cotovelos, a direita aplicando (a cura antes tocava o clipe EM PÉ).
        [System.NonSerialized] public float ProneK, ProneHealW, ProneShift; float pPitch, pRoll, phW; Transform lFore;
        public float PronePitch => pPitch; public float ProneRoll => pRoll; public float ProneHealBlend => phW;   // laboratório
        void ProneBody(float dt)
        {
            float w = alive && !vehiclePose ? Mathf.Clamp01(ProneK) : 0, tp = 0, tr = 0;
            if (w > .01f && hips)
            {
                float k = scaleK; var f = transform.forward; var r = transform.right; var o = transform.position + Vector3.up * .6f * k; int m = 1 << FBWorld.LMap;
                float G(Vector3 off) => Physics.Raycast(o + off, Vector3.down, out var h, 1.5f * k, m, QueryTriggerInteraction.Ignore) ? h.point.y : float.NaN;
                float yf = G(f * .8f * k), yb = G(-f * .8f * k), yl = G(-r * .25f * k), yr = G(r * .25f * k);
                if (!float.IsNaN(yf) && !float.IsNaN(yb)) tp = Mathf.Clamp(Mathf.Atan2(yf - yb, 1.6f * k) * Mathf.Rad2Deg, -25, 25);
                if (!float.IsNaN(yl) && !float.IsNaN(yr)) tr = Mathf.Clamp(Mathf.Atan2(yr - yl, .5f * k) * Mathf.Rad2Deg, -15, 15);
            }
            float e = 1 - Mathf.Exp(-dt * 8); pPitch = Mathf.Lerp(pPitch, tp, e); pRoll = Mathf.Lerp(pRoll, tr, e);
            if (w > .01f && hips)
            {
                hips.position += transform.forward * ProneShift;
                if (Mathf.Abs(pPitch) > .1f || Mathf.Abs(pRoll) > .1f)
                    hips.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(-pPitch, transform.right) * Quaternion.AngleAxis(pRoll, transform.forward), w) * hips.rotation;
            }
            phW = Mathf.Lerp(phW, w > .9f ? Mathf.Clamp01(ProneHealW) : 0, 1 - Mathf.Exp(-dt * 6));
            if (phW < .01f || !chest || !rArm || !rFore || !rHand || !lArm || !lHand) return;
            if (!lFore) lFore = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); if (!lFore) return;
            {
                float k = scaleK; var f = transform.forward; var r = transform.right; var up = transform.up;
                // ponto de trabalho: na frente do peito, a um palmo do chão (o peito deitado fica ~20 cm acima dele)
                var c = chest.position + f * .32f * k; float gy = transform.position.y + .12f * k; if (c.y > gy + .12f * k) c.y = gy + .12f * k;
                float osc = Mathf.Sin(Time.time * 7f) * .025f * k;
                var tl = c - r * .07f * k; var trg = c + r * .05f * k + f * osc + up * Mathf.Abs(osc) * .5f;
                FBIK.TwoBonePole(lArm, lFore, lHand, tl, lFore.position - r * .3f * k - up * .25f * k, phW);
                FBIK.TwoBonePole(rArm, rFore, rHand, trg, rFore.position + r * .3f * k - up * .25f * k, phW);
            }
        }
        // pular janela/murinho: o clipe FF_SYS_Vault leva o quadril ~2 m para frente, e o controlador JÁ faz o percurso (FBPlayer.VaultStep):
        // somados, o boneco saía da frente da câmera e ia longe demais. Trava o quadril no lugar (XZ do descanso) e limita a subida do clipe.
        Vector3 hipRest; bool hipRestOk, vaultTail;   // vaultTail: o pulo acabou mas a camada ainda desvanece na última pose (quadril ~2 m à frente) — a trava continua até sumir
        void VaultInPlace()
        {
            if (!hips) return; bool inV = sys == "FF_SYS_Vault";
            if (inV) vaultTail = true; else if (danW < .03f || sys != null || dance != null || !alive) vaultTail = false;
            bool v = (inV || vaultTail) && alive;
            if (!v) { if (danW < .05f && !vehiclePose) { hipRest = transform.InverseTransformPoint(hips.position); hipRestOk = true; } return; }
            if (!hipRestOk) return; var l = transform.InverseTransformPoint(hips.position);
            hips.position = transform.TransformPoint(new Vector3(hipRest.x, Mathf.Min(l.y, hipRest.y + .15f), hipRest.z));
        }
        // emotes/danças (IK de pés desligado): se o clipe leva um pé abaixo do piso, sobe o corpo inteiro o que faltar (suave, só para cima).
        // NPCs do pacote novo (20/10: "os pés estão entrando no chão"): vale o TEMPO TODO (andando/parado) — o Avatar do Unity mede a própria
        // altura do quadril e o boneco convertido afundava mais do que os 6 cm que o IK dos pés pode subir. Mede calcanhar (osso do pé) e
        // ponta (dedos); sobe rápido e desce devagar (sem balançar a cada passada).
        float danceLift; Transform lToe, rToe; bool toesOk; public float GroundLift => danceLift;
        [System.NonSerialized] public bool seated;                                                   // NPC sentado (FBRoleNpc): o raio acharia o BANCO como chão
        // r46i: quem escreve POR CIMA da pose (veículo/IK, garupa, levantar da queda) precisa do Animator reescrevendo os ossos todo quadro.
        // Os prefabs usam "Cull Update Transforms": fora da câmera os ossos ficam com o que se escreveu no quadro anterior e o tronco do piloto,
        // o alcance do volante e o IK somavam de novo a cada quadro (sonda r46i: a mesma queda começava com a coxa torcida +1,86 numa execução e
        // +0,16 na outra — dependia de o NPC estar ou não na tela). Enquanto alguém segura, AlwaysAnimate; volta ao do prefab quando todos soltam
        readonly HashSet<object> animHold = new(); AnimatorCullingMode? cullSaved;
        public void HoldAnimate(object who, bool on)
        {
            if (!an || who == null) return;
            if (on) { if (animHold.Count == 0) cullSaved = an.cullingMode; animHold.Add(who); an.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            else if (animHold.Remove(who) && animHold.Count == 0 && cullSaved is AnimatorCullingMode m) { an.cullingMode = m; cullSaved = null; }
        }
        Vector3 dgMark; bool dgSet;   // r46g: quadril como o DanceGround deixou (para saber se o Animator reescreveu desde então)
        // r47 (pedido 30/10: "pés, tornozelos, pernas ... contato com o chão durante os emotes"): a sola era estimada com constantes — jogador =
        // tornozelo − 3,5 cm, NPC = tornozelo − 7,5 cm / dedos − 2,2 cm. Nos esqueletos regionais o osso do pé fica bem mais alto que 3,5 cm,
        // então o pé entrava no piso antes de o corpo subir. Agora cada esqueleto mede a PRÓPRIA altura de tornozelo e de ponta parado em pé
        // com o foot IK no chão (a cada 8 quadros, até 24 amostras) e o emote usa essa referência; sem medida ainda, as constantes de antes.
        // E o contrário (os dois pés acima do piso por mais de 0,45 s dançando = retarget com o quadril alto) desce o corpo até 10 cm, devagar.
        float restFoot, restToe, floatT; int restN, restSlot = -1; static int restSlots; Renderer restR; public float RestFootH => restN > 0 ? restFoot : -1;
        void RestSample()
        {
            if (restSlot < 0) restSlot = restSlots++ & 7;
            if (restN >= 24 || (Time.frameCount + restSlot) % 8 != 0) return;
            if (!alive || !grounded || crouch || vehiclePose || seated || danW > .02f || sys != null || dance != null || velocity.Flat().sqrMagnitude > .04f || !lFoot || !rFoot) return;   // Flat: no chão o jogador tem vy = −2 fixo
            // só com o Animator escrevendo os ossos: os prefabs usam "Cull Update Transforms" e fora da câmera o esqueleto fica congelado
            // (laboratório r47b: NPC fora da tela media tornozelo 0,216 m e depois, dançando na tela, subiria ~14 cm)
            if (!restR) restR = GetComponentInChildren<SkinnedMeshRenderer>();
            if (!an || !an.enabled || (an.cullingMode != AnimatorCullingMode.AlwaysAnimate && !(restR && restR.isVisible))) return;
            if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out var h, 3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return;
            if (!toesOk) { toesOk = true; lToe = an ? an.GetBoneTransform(HumanBodyBones.LeftToes) : null; rToe = an ? an.GetBoneTransform(HumanBodyBones.RightToes) : null; }
            float f = Mathf.Min(lFoot.position.y, rFoot.position.y) - h.point.y, t = lToe && rToe ? Mathf.Min(lToe.position.y, rToe.position.y) - h.point.y : f;
            if (f < 0 || f > .25f * ScaleK || t < -.02f * ScaleK) return;                            // degrau/escada embaixo: não é repouso
            restN++; restFoot = restN == 1 ? f : Mathf.Lerp(restFoot, f, 1f / restN); restToe = restN == 1 ? t : Mathf.Lerp(restToe, t, 1f / restN);
        }
        void DanceGround(float dt)
        {
            RestSample();
            // r46g: caindo/levantando da moto o apoio no chão é do FBFall (os dois juntos subiam o corpo duas vezes)
            if (FBFall.Busy(this)) { danceLift = 0; dgSet = false; return; }
            // r46g: os prefabs usam Animator em "Cull Update Transforms" — fora da câmera ele NÃO reescreve os ossos, e somar a subida de novo
            // sobre a do quadro anterior acumulava (sonda r46e–g: quadril subindo 39 u/s até 25 unidades acima da raiz e voltando de uma vez
            // quando o boneco reaparecia — os "saltos de 5–36 m" ao levantar). Quadril igual ao que deixamos = Animator parado: não soma
            if (dgSet && hips && hips.localPosition == dgMark) { CulledSkips++; return; }
            dgSet = false;
            bool npc = npcSet != null && !isPlayer;
            if ((danW < .3f && !npc) || sys == "FF_SYS_Vault" || vaultTail || seated || !alive || vehiclePose || !hips || !lFoot || !rFoot) { danceLift = seated ? 0 : Mathf.MoveTowards(danceLift, 0, dt * (vaultTail || sys == "FF_SYS_Vault" ? 4 : .5f)); return; }
            var o = transform.position + Vector3.up * 1.2f; if (!Physics.Raycast(o, Vector3.down, out var h, 3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return;
            if (!toesOk) { toesOk = true; lToe = an ? an.GetBoneTransform(HumanBodyBones.LeftToes) : null; rToe = an ? an.GetBoneTransform(HumanBodyBones.RightToes) : null; }
            // só nos EMOTES (dance, do PlayDance): nos clipes sys (subir/descer da moto, porta do carro) a referência medida subia ~8 cm o corpo das
            // personagens de salto (tornozelo em repouso 0,157 m) no meio da transição — laboratório r47c: quadril a 9–10 u/s descendo da moto
            float k = ScaleK, low; bool cal = restN >= 6 && dance != null;
            if (cal) { low = Mathf.Min(lFoot.position.y, rFoot.position.y) - restFoot; if (lToe && rToe) low = Mathf.Min(low, Mathf.Min(lToe.position.y, rToe.position.y) - restToe); }   // sola estimada pela medida
            else
            {
                low = Mathf.Min(lFoot.position.y, rFoot.position.y) - (npc ? .075f : .035f) * k;   // osso do pé = tornozelo (~7 cm do chão)
                if (npc && lToe && rToe) low = Mathf.Min(low, Mathf.Min(lToe.position.y, rToe.position.y) - .022f * k);
            }
            float gap = h.point.y - low, need = Mathf.Max(0, gap);                                  // gap > 0: sola abaixo do piso; < 0: os dois pés no ar
            if (cal && danW > .3f && gap < -.04f * k) floatT += dt; else floatT = 0;
            if (floatT > .45f) need = Mathf.Max(gap + .01f * k, -.10f * k);                          // r47: flutuando por retarget -> desce (no máx. 10 cm)
            // r47c: dançando, sobe NA HORA o que o pé afundou (a suavização de 70 ms perdia o pico: laboratório r47c, pé 8–14 cm abaixo do
            // repouso no mesmo instante do clipe com o corpo já subindo) e desce a 0,3 m/s; andando/parado (NPC) continua suave como antes
            bool dancing = danW > .3f && dance != null;
            danceLift = need > danceLift ? (dancing ? need : Mathf.Lerp(danceLift, need, 1 - Mathf.Exp(-dt * 14))) : Mathf.MoveTowards(danceLift, need, dt * (dancing && npc ? .3f : npc ? .05f : .5f));
            if (Mathf.Abs(danceLift) > .002f) { hips.position += Vector3.up * danceLift; dgMark = hips.localPosition; dgSet = true; }
        }
        public static int CulledSkips;
        public float PeekShift { get; private set; }
        // r52: PeekBase = passo da inclinação em si (a câmera acompanha só este); peekExtra (FBPlayer) = o corpo sai mais para o cano ter linha
        public float PeekBase { get; private set; }
        [System.NonSerialized] public float peekExtra; float peekExS;
        public static bool LeanFirst = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbespreitarantigo") < 0;
        // inclinar (Q/E): PuppetAimRig original — rollWeights [0,6; 0,4] em Spine/Chest, até 40°; Q também traz a arma à frente
        void Lean(float dt, bool spc)
        {
            leanS = Mathf.Lerp(leanS, alive && !spc ? lean : 0, 1 - Mathf.Exp(-dt / .12f * 2.3f));
            peekExS = Mathf.Lerp(peekExS, alive && !spc && Mathf.Abs(leanS) > .05f ? peekExtra : 0, 1 - Mathf.Exp(-dt / .1f * 2.3f));
            if (Mathf.Abs(leanS) > .005f && spine != null && spine.Length >= 2)
            {
                float total = leanS * 40; float[] w = { .6f, .4f };
                for (int i = 0; i < 2; i++) { spine[i].Rotate(transform.forward, -total * w[i], Space.World); if (i == 1 && leanS < -.05f) spine[i].Rotate(Vector3.up, leanS * .22f * Mathf.Rad2Deg, Space.World); }
                // espreitar físico: o corpo dá um passo lateral (até 0,25) para o lado da inclinação, medido contra a parede (peekRoom); r52: mais
                // peekExtra quando o cano ainda não tem linha até o ponto mirado (FBPlayer.PeekClear), dentro da mesma folga medida
                float room = Mathf.Max(0, peekRoom), b = Mathf.Min(.25f * Mathf.Abs(leanS), room), shift = Mathf.Sign(leanS) * Mathf.Min(b + peekExS, room);
                if (hips) hips.position += transform.right * shift;
                PeekShift = shift; PeekBase = Mathf.Sign(leanS) * b;
            }
            else PeekShift = PeekBase = 0;
        }

        // IK dos pés: FBFootIK (detecção por pé, pelve, joelho, normal, foot lock, LOD) — registrado no Awake, chamado no LateUpdate
        [System.NonSerialized] public FBFootIK feet;

        public Vector3 MuzzlePos => Muzzle ? Muzzle.position : transform.position + Vector3.up * 1.4f * scaleK;
        public bool HandsBusy
        {
            get
            {
                if (reloading || DrawBusy || DrawSettling) return true;                 // [parceiro] DrawSettling: fim do saque
                if (LU < 0 || !an || !an.isActiveAndEnabled || !an.runtimeAnimatorController || LU >= an.layerCount)
                    return false;

                var state = an.GetCurrentAnimatorStateInfo(LU);
                return state.IsName("Hit Reaction")
                    || state.IsName("Grenade Throw")
                    || state.IsName("Inspect Anim");
            }
        }
        public float UpperW => upperW; public float DanW => danW; public float StillT => stillT; public float ScaleK => scaleK;
        public bool IsRelaxedPose => UnarmedLocomotion;                                               // relaxado = só desarmado (rodada 37)
        public bool Resting => !UnarmedLocomotion && !Hot && stillT > PUP("relaxedEnterDelay", 3);   // armado parado há 3 s: arma ainda mais baixa
        public bool Special => special || danW > .05f;
    }

    public static class FBConst { public const float ModelScale = 2.2f; }
}
