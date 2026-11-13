// Jogador local (porte do updatePlayer / updateCamera / playerFire do main.js):
//  - movimento com os números do controlador original (fpdata.movement): aceleração/desaceleração/no ar, laterais e ré 90 %, corrida
//    automática (Shift liga/desliga, como no web), agachar, deitar (Z) e rastejar, pular janela/murinho (CrossOver do FF: 0,567 s),
//    pulo com coyote time e buffer de 0,12 s (IE), água deixa mais lento, dano de queda (IE CalculateFall), regeneração até 40.
//  - kit médico (H, 3 s de canalização, cancela ao correr/atirar/pular), reanimar aliado caído (segurar F, 5 m / 40°), inclinar Q/E com
//    passo lateral medido na parede, caído (rasteja de joelhos), recuo que move a mira (FBRecoil), mira assistida original (fraca, uma
//    trava por vez, cabeça exige esforço), tiro em 3ª pessoa saindo do cano até o ponto da mira (parede na frente pega o tiro).
//  - câmera 1ª pessoa (cabeça + arma de visão com balanço/coice e ADS pelos offsets do fpdata) e 3ª pessoa por cima do ombro (tpsCamera:
//    pivô, braço, lateral dinâmica, colisão, FOV de corrida, tremor de dano/pouso, troca de ombro ao inclinar).
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    [RequireComponent(typeof(CharacterController))]
    public class FBPlayer : MonoBehaviour
    {
        static FBPlayer inst; public static FBPlayer I { get => inst ? inst : null; set => inst = value; }
        public static float CamYawRad => I ? I.camYaw * Mathf.Deg2Rad : 0;
        [System.NonSerialized] public FBCharacter ch; [System.NonSerialized] public FBWeaponUser wu; [System.NonSerialized] public Camera cam; public bool firstPerson;
        CharacterController cc; public float camYaw, camPitch; float camYawS, camPitchS, vy, airT, fallTop, lastDamage = 99, jumpQ = -9, regen;
        // r49: deitar em etapas — 0 em pé/agachado · 1 deitando (FF_SYS_Prone_Down) · 2 deitado · 3 levantando (FF_SYS_KO_GetUp); as transições
        // duram o clipe inteiro e não se cancelam (pedido 30/10: "sem voltar instantaneamente ao combate"); prone = qualquer etapa ≠ 0
        int proneSt; float proneT, proneLen = 1; bool proneToCrouch;
        public int ProneState => proneSt; public float ProneT => proneT; public float ProneLen => proneLen;   // laboratório
        bool crouch, prone, autoRun, jumpedAir, inAir; Vector3 hv; float lean, tpsKick, tpsPunch, tpsShake, camShake, facing, tpsArm = 1.5f, tpsLat = .8f, pivY = 1.6f, lastMoveT;
        (float t, float dur, Vector3 a, Vector3 b, float h, int snd, float yaw, float wall)? vault; FBCharacter reviving, reviveCand; GameObject vm; Transform vmMuzzle; float vmKick, swayX, swayY, vmBob;
        FBFpRig fp; Camera vmCam; bool wasInWater; bool jumpedFlag;
        // [parceiro 31/10] estava dirigindo no quadro anterior / veículo de onde saiu (ResetMotion na troca; arma no coldre perto dele)
        bool wasDriving; FBCar lastCar, holsterCar;
        bool onGround = true; float slideV, stepLocal = -1; FBMotor.Ground ground;      // física de chão (FBMotor)
        // r49 (pedido 30/10: "apertar o pulo rápido faz flutuar e andar muito mais rápido"). Causas: (1) depois de pular janela/mureta o
        // onGround ficava o do COMEÇO do pulo (o FBMotor não roda no VaultStep) — no fim, mesmo no ar ou acima de uma queda, o pulo guardado no
        // buffer saía na hora e outro pulo de janela podia começar do ar; (2) o FBMotor conta como chão qualquer contato da parte de baixo da
        // cápsula (cc.isGrounded) — quina de parede, beiral, peitoril — sem piso firme embaixo: apertando sem parar, cada raspão virava um pulo
        // novo e o corpo subia parede; (3) o fim do pulo de janela jogava hv = deslocamento × 1,5 (metros como velocidade). Agora o pulo exige
        // chão FIRME (a sonda acha piso andável a até 8 cm), um tempo mínimo no chão, intervalo mínimo entre pulos; o pulo de janela também, e
        // termina "no ar" até o FBMotor confirmar o chão. JumpFix = false volta ao de antes (laboratório -fbmovlab compara os dois).
        public static bool JumpFix = true; public static int Jumps, Vaults, JumpsDenied; public static float JumpCooldown = .3f, JumpGroundTime = .05f;
        float lastJumpT = -9, groundT, vaultEndT = -9, lastFirmT = -9;
        bool FirmGround => onGround && (!JumpFix || (ground.ok && !ground.steep && ground.dist <= .08f * ch.ScaleK + cc.skinWidth * transform.lossyScale.y));
        public bool OnGroundNow => onGround; public float VY => vy; public Vector3 HV => hv;    // laboratório
        public bool OnGround => onGround; public FBMotor.Ground GroundInfo => ground;
        // câmera livre (rodada 37): deslocamento da câmera em relação à direção do corpo/movimento (camYaw fica congelado enquanto olha)
        bool flOn, flLatch; float flYaw, flPitch, flYawS, flPitchS;
        public bool FreeLooking => flOn;
        public float ViewYaw => camYawS + flYawS;                                           // para onde a câmera olha de verdade (HUD, indicadores)
        bool FlBlend => Mathf.Abs(flYawS) > .5f || Mathf.Abs(flPitchS) > .5f;
        Vector3 AnchorFwd => Quaternion.Euler(-camPitchS, camYawS, 0) * Vector3.forward;   // mira do corpo (não a da câmera livre)
        float M(string k, float d) => GameData.Move(k, d);
        static float T(string k, float d) => Json.F(GameData.Section("tpsCamera"), k, d);
        // testes PlayMode e vitrine -fbcam: troca a entrada do quadro (mirar/atirar/correr/agachar/inclinar sem teclado)
        public static System.Func<FBInputState, FBInputState> TestInput;
        // estado da mira para HUD/testes: ponto sob o retículo do último tiro, onde a bala parou, mira bloqueada (o cano não alcança o ponto)
        [System.NonSerialized] public Vector3 LastShotAim, LastShotHit; [System.NonSerialized] public int ShotCount; public bool AimBlocked { get; private set; }
        public float CrossGapPx => Mathf.Max(6, crossSpd / 3.2f);         // abertura do retículo em px (setCrosshair do webui.js)
        float crossSpd = 36, blkT, collD = -1; readonly float[] latClr = { -1, -1 };

        void Awake()
        {
            I = this; cc = GetComponent<CharacterController>(); ch = GetComponent<FBCharacter>(); wu = this.GetOrAdd<FBWeaponUser>();
            ch.isPlayer = true; ch.displayName = FBSession.nameChosen || FBSession.online ? FBSession.name : "Você";   // r43: nome de usuário escolhido camYaw = camYawS = transform.eulerAngles.y;
            cam = Camera.main; if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            if (!cam.GetComponent<AudioListener>() && !FindAnyObjectByType<AudioListener>()) cam.gameObject.AddComponent<AudioListener>();
            cam.nearClipPlane = FBGfx.Tier <= FBGfxTier.Web ? .1f : .05f; cam.farClipPlane = 1400; cam.fieldOfView = FovBase;   // celular/WebGL: profundidade de 24 bits -> near 0,1 (z-fighting de longe)
            // oclusão Umbra DESLIGADA em todos os mapas (rodada 40): a auditoria -fbrender (FBRenderAudit) achou corte falso também na Vila Jardim
            // (pérgola/estruturas sumindo à vista, 1,4–1,6 % da imagem) — mesmo defeito que já tinha feito desligar na Praça das Águas.
            // Visibilidade = frustum + LODs com transição pontilhada (FBQuality.SmoothLods); a Vila Jardim reconstruída nem tem dados assados
            cam.useOcclusionCulling = false;
            FBQuality.SmoothLods(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var ucd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (!ucd) ucd = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true; ucd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
            // r60: a 1ª pessoa VOLTOU. Na r45b ela foi tirada do jogo (firstPerson = false fixo, tecla V só nos laboratórios) a pedido, mas
            // a máquina toda ficou de pé: o FBFpRig logo abaixo, a câmera da arma (vmCam), o cano de 1ª pessoa do tiro (FpBore/FpShotOrigin)
            // e a lente PIP das lunetas. Agora a vista sai da preferência salva do jogador (FBSession.view, "fp" ou "tps"), que os
            // laboratórios já escreviam sem ninguém ler. O padrão continua 3ª pessoa: o jogo é um TPS.
            firstPerson = FBSession.view == "fp";
            // [parceiro] v18 (thread de física): uma exceção aqui (ex.: falta de memória ao ler as animações de primeira pessoa) parava o
            // Awake no meio e o jogador ficava sem controle. Agora o jogo segue em terceira pessoa.
            try { fp = FBFpRig.Create(null); }
            catch (System.Exception ex) { fp = null; Debug.LogWarning("[FreeBala][fp] braços de primeira pessoa não carregaram (" + ex.GetType().Name + ": " + ex.Message + "); seguindo em terceira pessoa"); }
            if (fp) { fp.transform.SetParent(null); FBCharArms.Attach(fp, ch); }   // braços com a malha do próprio personagem (fparms.js)
            var vg = new GameObject("CameraArma"); vg.transform.SetParent(cam.transform, false); vmCam = vg.AddComponent<Camera>(); vmCam.cullingMask = 1 << FBWorld.LFirstPerson; vmCam.nearClipPlane = .01f; vmCam.farClipPlane = 10;
            vmCam.fieldOfView = Json.F(GameData.Section("character"), "fieldOfViewWeapon", 65);
            var vd = vg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); vd.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay; ucd.cameraStack.Add(vmCam);
            cam.cullingMask &= ~(1 << FBWorld.LFirstPerson);
            wu.onEquip = () => { if (fp && wu.Cur != null) fp.SetWeapon(wu.Cur); if (firstPerson && !fp) BuildViewModel(); };
            if (!FBInput.Touch && !FBGarage.Open_) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }   // garagem do rolê aberta: cursor fica livre
            gameObject.AddComponent<FBTouch>();
        }
        public bool FirstPerson => firstPerson;
        public void TestJump() => jumpQ = Time.time;   // autoteste
        public float FovBase => 55 + FBSession.fov * 40;                                   // fovBase do main.js (padrão .375 = 70°)
        public void OnRespawn(float yawDeg)
        {
            facing = yawDeg;   // r68d: no rolê desarmado o corpo voltava para o rumo de antes do teleporte/renascer
            camYaw = camYawS = yawDeg; camPitch = camPitchS = 0; prone = crouch = false; proneSt = 0; proneToCrouch = false; ch.ProneK = 0; vault = null; vy = 0; hv = Vector3.zero;
            // [parceiro] fisica 31/10: teleporte de respawn/rodada nao e queda. Sem isto, quem estava no ar (pulo/queda) pousava na base
            // com o fallTop/airT do lugar antigo: baque ie_land e dano de queda na fase de compra.
            inAir = false; airT = 0; jumpedAir = false; jumpQ = -9;
            lastDamage = 99; autoRun = false;
        }
        // [parceiro] Correção: zera o movimento a pé ao entrar e ao sair de veículo. Antes, a velocidade, o pulo, o vault
        // e o agachado de antes de entrar voltavam depois de sair (personagem deslizando, dano de queda, vault velho).
        // (porte: também zera a etapa do deitar da r49 — proneSt/ProneK —, senão o prone voltaria no quadro seguinte)
        public void ResetMotion()
        {
            vault = null; ch.VaultRate = 0; vy = 0; hv = Vector3.zero; slideV = 0;
            inAir = false; airT = 0; onGround = true; jumpedAir = false; jumpQ = -9;
            prone = crouch = false; proneSt = 0; proneToCrouch = false; ch.ProneK = 0;
            if (autoRun) { autoRun = false; FBHUD.I?.SetRun(false); }
        }
        // [parceiro] Correção: ao sair do veículo a arma aparecia na mão já atravessando a lataria (porta aberta, tanque da moto).
        // Ela fica no coldre até o jogador se afastar do veículo e então é sacada; mirar ou atirar saca na hora.
        bool HolsterNearCar(WeaponDef cur, FBInputState inp)
        {
            if (!holsterCar) { holsterCar = null; return false; }
            bool act = inp.aim || inp.fire || inp.firePressed;
            if (cur == null || !ch.alive || act || FarFrom(holsterCar, cur))
            {
                holsterCar = null;
                if (cur != null && ch.alive && !act) ch.DrawAnim();
                return false;
            }
            return true;
        }
        // [parceiro 31/10] longe do veículo = peito (1,2 m) a mais de 0,55 m (pistola) / 1,1 m (arma longa) da caixa da lataria, no plano
        bool FarFrom(FBCar car, WeaponDef w)
        {
            var p = ch.transform.position + Vector3.up * (1.2f * ch.ScaleK); var T = car.transform;
            var q = T.TransformPoint(car.lbox.ClosestPoint(T.InverseTransformPoint(p)));
            return (q - p).Flat().magnitude > (w.IsPistol ? .55f : 1.1f) * ch.ScaleK;
        }
        public void OnKnocked() { prone = crouch = false; proneSt = 0; proneToCrouch = false; ch.ProneK = 0; vault = null; autoRun = false; FBMed.Cancel(); }
        // tranco do dano nas DUAS câmeras (rodada 40: em 1ª pessoa o dano não mexia a câmera): 3ª pessoa = senoide do tpsCamera; 1ª = tremor curto
        public void Trauma(float k) { if (!firstPerson) tpsShake = Mathf.Min(1, tpsShake + T("damageTraumaScale", .6f) * k); else camShake = Mathf.Max(camShake, Mathf.Min(1.2f, k * 2.2f)); lastDamage = 0; }
        public void SpectateLook(float yaw) { camYaw = yaw; camPitch = Mathf.Clamp(camPitch, -10, 20); flYaw = flPitch = 0; }   // r49: espectador vira para as costas do alvo novo
        public void ForceThirdPerson() { firstPerson = false; ApplyView(); }
        public void ForceFirstPerson() { firstPerson = true; ApplyView(); }                // testes (vitrine das pistolas)
        public FBFpRig Fp => fp; public Camera VmCam => vmCam; public bool BodyVisible => bodyVis;   // r51: teste da mira óptica no exe
        public bool Driving => FBVehicles.Driving(ch);

        // [parceiro 01/11] medidor de tempo por quadro (FBTempo): desligado = chamada direta, sem custo
        static readonly int __fbt_Update = FBTempo.Id("FBPlayer.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0 || !FBMatch.I) return;
            var inp = FBInput.Read(); if (TestInput != null) inp = TestInput(inp);
            if (FBMatch.Paused) return;
            flOn = false;
            var mt = FBMatch.I; bool play = mt.state == "play";
            // no veículo: o rig de 1ª pessoa (braços com a malha da skin) é desligado AQUI — este retorno vinha antes da linha que o desliga e
            // ele ficava ligado e congelado onde o jogador montou: braços soltos flutuando ao lado da moto (22/10, teste do usuário)
            if (play) FBDrive.ApproachTick(ch);                                                  // r50: chegou à porta andando -> entra
            if (Driving)
            {
                // [parceiro 31/10] lembra o veículo para a arma ficar no coldre ao sair. (porte: a entrada pela ida até a porta da r50, no
                // ApproachTick acima, não passa pelo F do parceiro — zera o movimento aqui no primeiro quadro dirigindo)
                if (!wasDriving) ResetMotion();
                wasDriving = true; lastCar = FBVehicles.CarOf(ch);
                if (fp && fp.gameObject.activeSelf) fp.gameObject.SetActive(false); FBVehicles.PlayerInput(ch, inp, dt); Look(inp, dt); return;
            }
            if (wasDriving) { wasDriving = false; ResetMotion(); holsterCar = lastCar; lastCar = null; }   // [parceiro 31/10] acabou de sair do veículo
            if (FBBattleRoyale.Active(ch) && ch.alive && FBBattleRoyale.StepPlayer(ch, dt, inp, camYaw)) { Look(inp, dt); ch.SetWeaponVisible(false); return; }
            var s = wu.Cur; bool holster = HolsterNearCar(s, inp);   // [parceiro 31/10] arma no coldre ainda encostado no veículo
            bool canMove =ch.alive && play && !mt.freeze && !ch.special && !FBFall.Busy(ch);   // r45d: caindo da moto
            if (FBKillstreak2.Tick(ch, dt, UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.gKey.isPressed))
            {                                                                      // atirador: só olha e atira (a arma do jogador, como no CF)
                Look(inp, dt); var rc = wu.recoil.Step(dt); camPitch += rc.x; camYaw += rc.y; ch.Yaw = camYaw; ch.aiming = inp.aim;
                if (s != null && (s.auto ? inp.fire : inp.firePressed) && wu.cool <= 0) Fire(inp.aim); if (inp.reload) StartReload(); return;
            }
            if (ch.getUpT > 0) ch.getUpT -= dt;
            ReviveStep(dt, canMove, inp);
            // r49: etapa do deitar/levantar (o tempo do clipe); acabou de levantar → saca a arma com a animação (não volta atirando)
            if (proneSt == 1 || proneSt == 3)
            {
                proneT += dt;
                if (!ch.alive || ch.knocked) { proneSt = 0; proneToCrouch = false; }
                else if (proneT >= proneLen)
                {
                    bool up = proneSt == 3; proneSt = up ? 0 : 2; ch.poseT = Time.time;
                    if (up) { if (proneToCrouch) crouch = true; if (s != null && !FBMed.Busy) wu.Equip(wu.cur); }
                    proneToCrouch = false;
                }
            }
            prone = proneSt != 0; bool proneBusy = proneSt == 1 || proneSt == 3; ch.ProneK = ProneAmount();
            Vector2 ax = inp.move; float len = ax.magnitude; if (len > 1) { ax /= len; len = 1; }
            if (ch.dance != null && play && (len > .1f || inp.fire || inp.aim || inp.jump)) { ch.StopDance(len > .1f || inp.jump); FBNet.I?.SendEvent("stopdance"); }
            bool aiming = s != null && inp.aim && ch.alive && wu.switching <= 0 && !ch.special && !ch.knocked && !reviving && !proneBusy;
            // CÂMERA LIVRE: olhar em volta sem mudar o rumo — parado, andando, correndo ou deslocando sem mirar (Alt segurar/alternar, R3
            // segurado no controle, botão OLHAR no toque). Movimento e frente do corpo ficam na direção de antes (camYaw congelado); mirar,
            // atirar, recarregar, curar ou cair encerra e a câmera volta suave para trás do personagem.
            if (FBSession.freeLookToggle && !FBInput.Touch) { if (inp.freeLookPressed) flLatch = !flLatch; } else flLatch = false;
            bool wantFL = (inp.freeLook && (!FBSession.freeLookToggle || FBInput.Touch)) || flLatch;
            if (aiming || inp.fire || inp.firePressed || inp.aimPressed || inp.reload || !ch.alive || ch.knocked || ch.special || FBMed.Busy || !play) { wantFL = false; flLatch = false; }
            flOn = wantFL; if (!flOn) { flYaw = 0; flPitch = 0; }
            Look(inp, dt);
            if (!ch.alive) FBSpectator.Tick(inp, dt);                                          // r49: espectador (trocar alvo, renascer, faixa)
            if (ch.alive && play) { var rc = wu.recoil.Step(dt); camPitch = Mathf.Clamp(camPitch + rc.x, firstPerson ? -86 : -34, firstPerson ? 86 : 54); camYaw += rc.y; AimAssist(dt, aiming, inp); BlockedCheck(dt); } else { wu.recoil.Reset(); AimBlocked = false; }
            // r45: cura SÓ PARADO — antes de qualquer ação deste quadro: andar, pular, correr, atirar, mirar, recarregar, trocar de arma,
            // mudar de postura, interagir, abrir loja/mochila ou sair do chão cancelam a cura na hora (animação e recuperação)
            if (FBMed.Busy) HealGuard(inp);
            // ações de botão
            if (play && ch.alive)
            {
                if (inp.sprintToggle) { autoRun = !autoRun; FBHUD.I?.SetRun(autoRun); }
                // agachar deitado = levantar até agachado (antes: prone = false, o corpo pulava da pose deitada para agachado no mesmo quadro)
                // [parceiro] fisica 31/10: testa o teto so quando a capsula vai crescer (agachado -> em pe, ou deitado -> agachado); descer continua livre
                // (porte: deitado -> agachado passa pelo SetProne(false), que já testa com o SemEspaco)
                if (inp.crouchToggle)
                {
                    if (proneSt == 2) { if (SetProne(false)) proneToCrouch = true; }
                    else if (proneSt == 0)
                    {
                        if (crouch && SemEspaco((Json.F(GameData.Section("controller"), "m_Height", 1.8f) + .05f) * ch.ScaleK)) FBHUD.I?.Center("SEM ESPAÇO PARA LEVANTAR", 1);
                        else { crouch = !crouch; ch.poseT = Time.time; FBAudio.Play2D("ie_crouch", .3f); }
                    }
                }
                if (inp.prone) SetProne(proneSt == 0);
                if (!proneBusy)                                                            // r49: deitando/levantando: mãos ocupadas (sem recarregar, curar, trocar)
                {
                    if (inp.reload) StartReload(); if (inp.heal) TryHeal();
                    if (inp.slot >= 0) { if (inp.slot == 4) mt.ks.Consume(Time.time); else wu.SwitchTo(inp.slot); } if (inp.cycle != 0) wu.Cycle(inp.cycle);
                    if (inp.inspect && s != null) { ch.Trigger("inspect"); if (fp && firstPerson && wu.reloadT <= 0) fp.Inspect(); }
                }
                else if (inp.heal) FBHUD.I?.Center("MUDANDO DE POSIÇÃO", .8f);
                if (inp.pickup) FBLoot.Pickup(ch);
                // r60: a tecla V alterna 1ª/3ª pessoa no JOGO outra vez (na r45b só valia nos laboratórios). A escolha vai para a
                // preferência salva, senão voltaria para a 3ª a cada renascimento. Dirigindo não alterna: o FP do volante é outro caminho.
                if (inp.toggleView && !ch.special && !Driving)
                {
                    viewBlendOff = cam.transform.position - ch.Pos; viewBlendRot = cam.transform.rotation; viewBlendT = ViewBlendTime;   // r60: de onde a câmera sai
                    firstPerson = !firstPerson; FBSession.view = firstPerson ? "fp" : "tps"; FBSession.Save(); ApplyView();
                }
                if (inp.interact && !reviveCand && !ch.knocked && !FBFall.Busy(ch) && proneSt == 0)   // r49: deitado não entra em veículo (o corpo saltava da pose)
                {
                    FBVehicles.Toggle(ch);
                    // r50 (CAUSA de "as animações de entrar/subir continuam quebradas na Development Build"): o F entrava pelo Enter AQUI, no meio do
                    // Update a pé, e o resto deste Update seguia como se o jogador estivesse a pé — a linha do ch.sys (cura/vault/deitado/null) apagava
                    // o clipe de entrar/subir que o StartCarIn/StartMount acabara de pôr, e ch.combat religava a pose armada. Nos quadros seguintes o
                    // Update sai cedo (Driving) e ninguém repunha o clipe: o FBDriverPose arrastava o boneco PARADO EM PÉ (IE_Unarmed_Idle01) pelo
                    // caminho porta→banco — cabeça atravessando o teto, em pé dentro da moto. Teste -fbjogo r50c: F direto = camada 2 vazia no meio
                    // da entrada em 8 de 8; vindo da ida até a porta (Enter no começo do Update, antes do "if (Driving) return") = clipe tocando.
                    // Os laboratórios chamavam FBDrive.Enter de uma corrotina, fora do FBPlayer, e nunca passaram por aqui.
                    // [parceiro] Correção: entrou no veículo; o resto do Update a pé apagava o clipe de entrar (ch.sys) e podia religar o
                    // colisor do jogador sentado. (porte: zera o movimento sempre; o retorno segue o SysGuard da r50, que o -fbsemclipe desliga)
                    if (Driving) { ResetMotion(); wasDriving = true; }
                    if (Driving && FBDrive.SysGuard) { if (fp && fp.gameObject.activeSelf) fp.gameObject.SetActive(false); Look(inp, dt); return; }
                }
                // r68: B no balcão da loja de armas do De Rolê abre a loja (longe dela continua sendo a roda de emotes)
                if (inp.buy) { if (FBShop.Open && FBShop.Store) FBShop.Show(false); else if (FBEconomy.buying) FBShop.Show(!FBShop.Open); else if (FBGunShop.Near) FBGunShop.OpenStore(); else FBEmoteWheel.Open(); }
                if (inp.buyReleased) FBEmoteWheel.Close(true);
                if (inp.arsenal && mt.mode != null && mt.mode.arsenal) { FBEconomy.buying = true; FBShop.Show(!FBShop.Open); }
                if (inp.bag) FBBag.Toggle(); if (inp.mark) FBMarks.Mark(cam); if (inp.multimidia) FBMultimidia.Toggle(); if (inp.map) FBMatchMap.Toggle();
                if (inp.gps) { if (mt.mode != null && mt.mode.role) FBGps.ToggleBig(); else FBKillstreak2.Call(); }
            }
            if (inp.map) FBMatchMap.Toggle();
            if (inp.jump) jumpQ = Time.time;
            // corrida
            if (autoRun && (!canMove || aiming || inp.fire || crouch || inp.move.y < .3f || Mathf.Abs(inp.move.x) > .6f)) { if (!(FBInput.Touch && inp.move.y > .3f)) { autoRun = false; FBHUD.I?.SetRun(false); } }
            if (autoRun && !FBInput.Touch && inp.move.sqrMagnitude < .01f) ax = new Vector2(0, 1);
            bool running = (autoRun || FBSession.holdRun && inp.sprintHeld) && ax.y > 0 && !aiming && !crouch && !inp.fire && !prone && !ch.knocked && !(s != null && wu.reloadT > 0 && wu.Ammo < s.mag);
            float runW = running ? (s != null ? s.move : 1.15f) : 1;
            bool crouchFast = crouch && !prone && inp.sprintHeld && ax.y > .3f && !aiming && !inp.fire && !ch.knocked;   // r42: agachado rápido
            ch.CrouchFast = crouchFast;
            float baseSp = ch.knocked ? FBMechanics.KNOCK.moveSpeed : (crouch ? (crouchFast ? FBAnimProfile.CrouchFastSpeed : M("speedCrouching", 3)) : aiming ? M("speedAiming", 3) : running ? M("speedRunning", 7.4f) : ch.UnarmedLocomotion ? FBAnimProfile.UnarmedWalkSpeed : WalkSp) * runW * (FBMed.Busy ? FBMed.MoveMul : 1) * (prone ? 1.1f / Mathf.Max(1, M("speedWalking", 5)) : 1);
            if (s != null) baseSp *= FBWeaponModel.ModsOf(s).move;                              // r45: bipé pesa
            float mulX = M("walkingMultiplierSideways", .9f), mulY = ax.y >= 0 ? M("walkingMultiplierForward", 1) : M("walkingMultiplierBackwards", .9f);
            var q = Quaternion.Euler(0, camYaw, 0); var tv = canMove && !reviving && !proneBusy ? q * new Vector3(ax.x * baseSp * mulX, 0, ax.y * baseSp * mulY) : Vector3.zero;
            tv *= ch.ScaleK;                                                    // personagens maiores andam proporcionalmente (escala do mapa)
            bool walkToCar = false; if (canMove && FBDrive.ApproachStep(ch, inp, WalkSp * ch.ScaleK, dt) is Vector3 av) { tv = av; walkToCar = true; }   // r50: andando até a porta do veículo
            if (proneSt == 2) { var pf = Quaternion.Euler(0, ch.Yaw, 0) * Vector3.forward; float al = Vector3.Dot(tv, pf); if ((al > 0 && proneFront < .5f * ch.ScaleK) || (al < 0 && proneBack < .5f * ch.ScaleK)) tv -= pf * al; }   // r49: rastejar com a cabeça/pés na parede
            var wy = FBWorld.WaterAt(transform.position.x, transform.position.z); bool inWater = wy != null && transform.position.y < wy + .15f; if (inWater) tv *= .62f; if (inWater && !wasInWater && vault == null && vy < -2.5f && airT > .3f) { FBAudio.PlayAt("pulo_agua", transform.position, .8f, 8); FBFx.Splash(new Vector3(transform.position.x, wy.Value, transform.position.z), 1.6f); } wasInWater = inWater;   // caiu na água
            float rate = onGround ? (tv.magnitude > hv.magnitude ? M("acceleration", 12) : M("deceleration", 11)) : M("accelerationInAir", 1.7f);
            hv = Vector3.Lerp(hv, tv, 1 - Mathf.Exp(-dt * rate));
            bool jumped = false;
            if (inp.jump && canMove && prone) SetProne(false);                           // deitado: pular = levantar (no meio da transição não faz nada)
            else if (inp.jump && canMove && vault == null && TryVault()) jumpQ = -9;
            if (onGround) jumpedAir = false;
            bool wantJump = Time.time - jumpQ <= FBMechanics.JumpBuffer, coyote = !onGround && !jumpedAir && airT <= FBMechanics.Coyote && vy <= 0;
            bool canJump = onGround || coyote;
            if (JumpFix)
            {   // chão firme há um instante (ou acabou de sair de chão firme andando: coyote), e não pulou há pouco
                canJump = (FirmGround && groundT >= JumpGroundTime) || (coyote && Time.time - lastFirmT <= FBMechanics.Coyote);
                if (Time.time - lastJumpT < JumpCooldown || Time.time - vaultEndT < .2f) canJump = false;
                if (wantJump && !canJump && inp.jump) JumpsDenied++;
            }
            if (wantJump && canJump && !crouch && !prone && canMove && vault == null && !ch.knocked && !reviving)
            { vy = Mathf.Sqrt(2 * M("jumpGravity", 35) * M("jumpForce", 1.5f)) * Mathf.Sqrt(ch.ScaleK); jumpedAir = true; jumpQ = -9; jumped = true; lastJumpT = Time.time; groundT = 0; Jumps++; FBAudio.Play2D("ie_jump", .4f, .1f); FBAudio.PlayAt((FBVoice.SexOf(ch) == "f" ? "f" : "m") + "_pulo", ch.Pos + Vector3.up * 1.6f, .55f, 6); }   // voz de esforço no pulo (gravação própria)
            float hs = hv.magnitude;
            ch.sprint = running && hs > Mathf.Max(.2f, baseSp * .22f); ch.crouch = (crouch || prone || ch.knocked || reviving) && ch.alive;   // curando: em pé ou agachado, o jogador escolhe com a tecla de agachar
            ch.prone = (prone || ch.knocked) && ch.alive;
            bool waiting = mt.mode != null && mt.mode.econ && s == null && ch.alive && onGround && hs < .18f && !aiming && !inp.fire && !crouch && !prone;
            var wp = waiting ? FBEmotes.WaitPose(ch.IsFemale) : null; if (wp != null) ch.PlayWaitPose(wp); else ch.StopWaitPose();   // r47: sem emote FF
            var rescue = ch.RescuePose(reviving && ch.alive && !ch.knocked, dt);       // reanimar aliado: Sigma (Crouch_Self_Rescue Start/Loop/End)
            ch.sys = !ch.alive ? null : ch.knocked ? FBCombat.KoPose(ch, hs > .4f) : ch.getUpT > 0 ? FBCombat.GetUpClip : HealPose() ?? rescue ?? (vault != null ? Has("FF_SYS_Vault") : prone ? ProneClip(hs) : null);
            ch.shooting = ch.lastShot < Json.F(GameData.Section("puppet"), "shootingFlagDuration", .15f); ch.aiming = aiming; ch.combat = s != null && !ch.knocked;
            lean = Mathf.Lerp(lean, canMove && !running && !prone && !ch.knocked ? (inp.leanR ? 1 : 0) - (inp.leanL ? 1 : 0) : 0, 1 - Mathf.Exp(-dt * 10));
            // movimento físico
            float H = vault != null ? .95f : ch.knocked ? .7f : prone ? Mathf.Lerp(Json.F(GameData.Section("controller"), "m_Height", 1.8f), .7f, ch.ProneK) : crouch ? M("crouchHeight", 1.4f) : Json.F(GameData.Section("controller"), "m_Height", 1.8f);
            float sk = 1f / transform.lossyScale.y; cc.height = Mathf.Lerp(cc.height, H * sk, 1 - Mathf.Exp(-dt * M("crouchTransitionSpeed", 9))); cc.center = new Vector3(0, cc.height * .5f, 0);
            if (vault != null) VaultStep(dt);
            else
            {
                vy = onGround && vy < 0 ? -2 : vy - M("gravity", 35) * Mathf.Sqrt(ch.ScaleK) * dt;
                // FBMotor: chão por SphereCast, aderência degrau abaixo/ladeira, stepOffset só no chão, rampa íngreme escorrega
                if (stepLocal < 0) stepLocal = cc.stepOffset;
                var flags = FBMotor.Move(cc, (hv + Vector3.up * vy) * dt, onGround, vy > 0, stepLocal, .45f * ch.ScaleK, ref slideV, out onGround, out ground);
                if ((flags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;
                if (ground.ok && ground.steep && !onGround) vy = Mathf.Max(vy, -6);       // escorregando: não acumula queda livre
            }
            if (FirmGround) { groundT += dt; lastFirmT = Time.time; } else groundT = 0;
            if (JumpFix) { float hcap = M("speedRunning", 7.4f) * 1.25f * ch.ScaleK; if (hv.sqrMagnitude > hcap * hcap) hv = hv.normalized * hcap; }   // nada passa do sprint (+25%)
            // dano de queda (do ponto mais alto até o pouso)
            if (!onGround) { if (!inAir) { inAir = true; fallTop = transform.position.y; } else if (transform.position.y > fallTop) fallTop = transform.position.y; }
            else if (inAir)
            {
                inAir = false; float fd = (fallTop - transform.position.y) / ch.ScaleK; bool soft = FBWorld.WaterAt(transform.position.x, transform.position.z) != null;
                float fdmg = ch.alive && !soft && play ? FBMechanics.FallDamage(fd, airT) : 0;
                if (fdmg > 0) { FBCombat.DamageActor(ch, fdmg, false, null, "fall"); FBAudio.Play2D("ie_bodyhit", .7f); camShake = Mathf.Max(camShake, Mathf.Min(1, fdmg / 60)); }
                if (airT > .32f && ch.alive) FBAudio.Play2D("ie_land", Mathf.Max(Mathf.Min(.55f, .18f + airT * .35f), FBMechanics.LandVolume(fd) * .8f), .1f);
                if (airT > .5f) tpsShake = Mathf.Min(1, tpsShake + T("landTraumaAmount", .45f) * Mathf.InverseLerp(T("landTraumaMinSpeed", 5), T("landTraumaMaxSpeed", 16), Mathf.Abs(vy)));
            }
            airT = onGround ? 0 : airT + dt;
            if (inWater && hs > .6f) { regen -= dt; if (regen <= 0) { FBFx.Splash(new Vector3(transform.position.x, wy.Value, transform.position.z), .55f); regen = .26f; } }
            ch.velocity = new Vector3(hv.x, vy, hv.z); ch.grounded = onGround;
            // corpo: 1ª pessoa / caído acompanham a câmera; 3ª pessoa acompanha a mira (Alt = olhar livre)
            if (ch.alive && !ch.special && ch.dance == null && !FBFall.Busy(ch))                  // r46: caído/levantando: a raiz é do FBFall
            {
                if (firstPerson || ch.knocked || adsW > 0) ch.Yaw = camYaw;                         // r51: na mira óptica o corpo segue o mouse sem atraso (a câmera sai do rig dele)
                else
                {
                    bool aimFace = !(mt.mode != null && mt.mode.role && s == null);   // câmera livre não mexe aqui: camYaw fica congelado
                    if (aimFace && !walkToCar) facing = camYaw; else if (hs > .75f * (walkToCar ? .4f : 1)) facing = Mathf.Atan2(hv.x, hv.z) * Mathf.Rad2Deg;
                    float st = aiming || inp.fire || ch.lastShot < T("shootingFacingHold", .35f) ? T("aimFacingSmoothTime", .04f) : T("freeFacingSmoothTime", .14f);
                    ch.Yaw = Mathf.LerpAngle(ch.Yaw, facing, 1 - Mathf.Exp(-dt / st * 2.3f));
                }
                ch.hasAim = true; ch.aimPoint = firstPerson ? cam.transform.position + cam.transform.forward * 50 : RigAimPoint();
            }
            // velocidade de animação remapeada (como os bots)
            var lv = Quaternion.Inverse(Quaternion.Euler(0, ch.Yaw, 0)) * hv / Mathf.Max(.01f, ch.ScaleK); float sp = RemapSpeed(hs / Mathf.Max(.01f, ch.ScaleK)), lm = new Vector2(lv.x, lv.z).magnitude;
            ch.animVel = lm > 1e-3f ? new Vector2(lv.x / lm * sp, lv.z / lm * sp) : Vector2.zero;
            ch.lean = lean;
            if (Mathf.Abs(lean) > .02f)
            {
                var sd = transform.right * Mathf.Sign(lean); float room = .25f + (PeekFix ? PeekMax * ch.ScaleK : 0);   // r52: até o passo + o extra do cano
                foreach (var hy in new[] { .5f, 1.2f, 1.7f }) if (FBWorld.RayHit(transform.position + Vector3.up * hy * ch.ScaleK, sd, room + .3f, out var hh)) room = Mathf.Min(room, Mathf.Max(0, hh.distance - .32f));
                ch.peekRoom = room;
            }
            else ch.peekRoom = .25f;
            // kit médico, troca de arma, tiro
            if (FBMed.Busy) { string why = !canMove ? "" : ch.sprint ? "CORREU" : inp.fire || inp.firePressed ? "ATIROU" : jumped ? "PULOU" : FBShop.Open ? "LOJA" : hv.Flat().magnitude > .45f * ch.ScaleK ? "SE MOVEU" : null; if (why != null) CancelHeal(why); }
            if (s != null)
            {
                if (wu.Ammo == 0 && wu.Reserve > 0 && wu.reloadT <= 0 && wu.switching <= 0) { regen += dt; if (regen > .25f) { regen = 0; StartReload(); } }
                bool want = s.auto ? inp.fire : inp.firePressed;
                if (canMove && want && wu.cool <= 0 && !ch.sprint && vault == null && !ch.knocked && !reviving && !FBMed.Busy && !proneBusy) Fire(aiming);
            }
            lastDamage += dt;
            // retículo dinâmico (updateHUD do main.js): velocidade, espingarda, no ar, logo após o tiro e a abertura acumulada do recuo
            crossSpd = 36 + hs / Mathf.Max(.01f, ch.ScaleK) * 7 + (s != null && s.pellets > 1 ? 30 : 0) + (!onGround ? 30 : 0) + (ch.lastShot < .15f ? 10 : 0) + wu.recoil.bloom * 14;
            float cap = FBSession.mode == "treino" ? 100 : FBMed.PassiveCap, regenDelay = Json.F(GameData.Section("target"), "regenDelaySeconds", 5), regenRate = Json.F(GameData.Section("target"), "regenHpPerSecond", 5);
            bool still = onGround && hv.Flat().magnitude < .3f * ch.ScaleK && !ch.shooting && !ch.vehiclePose;   // r45: recuperação de vida só PARADO (também a passiva)
            if (still && lastDamage > regenDelay && ch.alive && !ch.knocked && !FBEconomy.EconMode(FBSession.mode) && !(mt.mode != null && mt.mode.br) && ch.hp < cap) ch.hp = Mathf.Min(cap, ch.hp + dt * regenRate);
            if (FBMed.Update(dt, v => { if (ch.alive) ch.hp = Mathf.Min(ch.maxHp, ch.hp + v); }) == "done") { FBAudio.Synth("heal"); healEnd = .35f; if (s != null && ch.alive) wu.Equip(wu.cur, proneSt != 0); }   // deitado: sem o gesto de sacar em pé (torcia os braços)
            if (healEnd > 0) healEnd -= dt;
            bool handsBusyMed = FBMed.Busy || healEnd > 0 || ch.Rescuing;
            ch.SetWeaponVisible(!handsBusyMed && !proneBusy && !holster && ch.dance == null && !ch.knocked && !ch.UnarmedLocomotion && !ch.vehiclePose);   // no banco do veículo a arma fica guardada
            ch.StowCurrent((handsBusyMed || proneBusy || holster) && s != null && ch.alive);     // r49: deitando/levantando, a arma vai para as costas (as mãos apoiam no chão); [parceiro 31/10] e colado no veículo de onde saiu
            ch.ProneHealW = proneSt == 2 && FBMed.Busy ? 1 : 0;                       // r49: curar deitado — mãos juntas na frente do rosto (FBCharacter)
            ProneRoom(dt);                     // curando/reanimando: a arma vai para o coldre (costas/peito/coxa), não some
            FBHUD.I?.SetMed(FBMed.kits, FBMed.inf, FBMed.Progress, FBMed.Healing);
            FBLoot.Update(dt, ch);
            // r51 mira óptica (FBOptic): mirando com luneta/holográfico/ponto vermelho a câmera vai da 3ª para a 1ª pessoa (rig do LPSP com os
            // braços da skin); recarregar, trocar de arma, correr, curar, veículo ou soltar a mira voltam para a 3ª. Mira de ferro: 3ª pessoa.
            bool optic = !firstPerson && aiming && s != null && fp && FBOptic.Has(s) && ch.alive && !ch.special && !ch.vehiclePose && !ch.knocked && !ch.sprint
                && wu.reloadT <= 0 && wu.switching <= 0 && !FBMed.Busy && healEnd <= 0 && !reviving && vault == null && !proneBusy;
            OpticGate = !Diag || optic || !aiming || s == null || !FBOptic.Has(s) ? null : (ch.sprint ? "correndo " : "") + (wu.reloadT > 0 ? "recarga " : "") + (wu.switching > 0 ? "troca " : "") + (FBMed.Busy || healEnd > 0 ? "cura " : "") + (reviving ? "reanimando " : "") + (vault != null ? "pulo " : "") + (proneBusy ? "deitando " : "") + (ch.knocked ? "caído " : "") + (ch.special ? "especial " : "") + (firstPerson ? "1ª " : "");   // teste r51i
            adsW = Mathf.MoveTowards(adsW, optic ? 1 : 0, dt / (optic ? FBOptic.InTime : FBOptic.OutTime)); if (!ch.alive || ch.vehiclePose || firstPerson) adsW = 0;
            // 1ª pessoa: rig do LPSP (fp.js) segue o corpo
            bool fpOn = (firstPerson || adsW > 0) && ch.alive && !ch.special && !ch.vehiclePose && fp;
            bool fpWas = fp && fp.gameObject.activeSelf;
            if (fp) fp.gameObject.SetActive(fpOn && s != null && !FBMed.Busy && healEnd <= 0 && !ch.knocked && !reviving);
            if (fp && !fpWas && fp.gameObject.activeSelf && !firstPerson) fp.Settle();          // ligou para a mira óptica: sem o "sacar" parado no quadro 0
            if (fpOn && fp.gameObject.activeSelf)
            {
                fp.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0, ch.Yaw, 0)); fp.transform.localScale = Vector3.one * ch.ScaleK;
                float nominal = crouch || prone ? M("speedCrouching", 3) : aiming ? M("speedAiming", 3) : running ? M("speedRunning", 7.4f) : ch.UnarmedLocomotion ? FBAnimProfile.UnarmedWalkSpeed : WalkSp;
                fp.Tick(dt, new FBFpRig.St { aiming = aiming, axisX = ax.x, axisY = ax.y, moving = hs > .5f, lookX = inp.look.x * 3, running = ch.sprint, crouching = crouch || prone, lean = lean, pitch = camPitchS, height = cc.height * transform.lossyScale.y / ch.ScaleK, jumped = jumped, inAir = !onGround && airT > .1f, movementRate = Mathf.Clamp(baseSp / Mathf.Max(.01f, nominal * ch.ScaleK) * (inWater ? .62f : 1), .4f, 1.18f) });
            }
            // luneta (overlay 2D) e câmera livre virada: os braços + arma do rig de 1ª pessoa somem da tela (o modelo simples 'vm' já sumia;
            // o rig dos braços da skin não — a arma aparecia DENTRO da luneta). O rig continua ativo: a câmera de 1ª pessoa sai dele.
            // r51: na mira óptica os braços/arma de 1ª pessoa só aparecem da metade da transição em diante (antes disso o corpo em 3ª pessoa)
            bool fpHide = fp && fp.gameObject.activeSelf && ((!firstPerson && OpticE < .5f) || Mathf.Abs(flYawS) >= 12);
            if (fp && (fpHide != fpHidden || (fpHide && (fpHideT2 -= dt) <= 0)))
            {
                fpHidden = fpHide; fpHideT2 = .25f;                                                       // armas criadas depois (troca) também
                foreach (var r in fp.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = fpHide;
            }
        }
        public static bool Diag;                                                                   // FBJogoTest: monta o OpticGate (motivo da mira óptica negada)
        float healEnd, fpHideT2; bool fpHidden;
        // r60: transição 1ª↔3ª pessoa. Guarda a pose da câmera do quadro da troca (como deslocamento do corpo, para acompanhar quem anda)
        // e sai dela até a pose do ramo novo em ViewBlendTime segundos. Só a câmera: tiro, rig e HUD trocam na hora, como já era.
        const float ViewBlendTime = .18f; float viewBlendT; Vector3 viewBlendOff; Quaternion viewBlendRot;
        float adsW; public float OpticE => adsW * adsW * (3 - 2 * adsW); public string OpticGate { get; private set; }                      // r51: transição da mira óptica (0 = 3ª pessoa, 1 = na mira)
        // cura do Sigma: em pé = Stand_Self_Rescue (FF_SYS_Heal_*), agachado = Crouch_Self_Rescue_Loop (FF_SYS_KO_Revive); alterna a qualquer momento com agachar
        string HealPose()
        {
            if (proneSt != 0 && (FBMed.Busy || healEnd > 0)) return ProneClip(0);     // r49: curar deitado — continua deitado (antes tocava a cura EM PÉ e o corpo levantava)
            if (FBMed.Busy) return crouch && !prone ? Has("FF_SYS_KO_Revive") ?? Has("FF_SYS_Heal_Loop") : FBMed.t < .35f ? Has("FF_SYS_Heal_Start") : Has("FF_SYS_Heal_Loop");
            if (healEnd > 0 && !crouch) return Has("FF_SYS_Heal_End"); return null;
        }
        static string Has(string c) => FBRegistry.I && FBRegistry.I.Clip(c) ? c : null;
        float RemapSpeed(float s) { var P = GameData.Section("puppet"); float pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5); return s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw); }

        void Look(FBInputState inp, float dt)
        {
            var s = wu.Cur; float k = inp.aim && s != null ? Mathf.Min(Json.F(FBWeaponModel.Scope(s), "multiplierMouseSensitivity", .8f), 1.15f / FBScopes.Zoom(s)) * (.4f + FBInput.AdsSens * 1.2f) : 1;   // com zoom o mouse fica mais fino
            float dyaw = inp.look.x * k * assistFric, dp = inp.look.y * k * assistFric;
            if (flOn)
            {   // câmera livre: gira só a câmera (1ª pessoa: até ±105°, como virar a cabeça; 3ª: volta quase inteira em volta do personagem)
                float lim = firstPerson ? 105 : 178, pmin = (firstPerson ? -86 : -34) - camPitch, pmax = (firstPerson ? 86 : 54) - camPitch;
                flYaw = Mathf.Clamp(flYaw + dyaw, -lim, lim); flPitch = Mathf.Clamp(flPitch + dp, pmin, pmax); return;
            }
            camYaw += dyaw; camPitch = Mathf.Clamp(camPitch + dp, firstPerson ? -86 : -34, firstPerson ? 86 : 54);
            assistPull += dyaw; wu.recoil.Look(dyaw, dp); swayX = Mathf.Lerp(swayX, inp.look.x, 1 - Mathf.Exp(-dt * 8)); swayY = Mathf.Lerp(swayY, inp.look.y, 1 - Mathf.Exp(-dt * 8));
        }

        // ---- tiro (playerFire)
        void Fire(bool aiming)
        {
            var s = wu.Cur; var md = FBWeaponModel.ModsOf(s, null, crouch || prone);
            var rc = new FBRecoil.State { aiming = aiming, crouch = crouch, moving = hv.magnitude > .5f, air = !onGround, zoom = aiming ? FBScopes.Zoom(s) : 1, grip = 1, recV = md.recV, recH = md.recH, spreadHip = md.spreadHip, spreadAds = md.spreadAds };
            ShotGeom(out var o, out var d, out float near, out var from); bool ok; Vector3? aimP = null;   // câmera livre voltando: o tiro vai para onde o corpo mira
            System.Func<Vector3, Vector3, Vector3> aimTo = (oo, dd) => { var P = AimPoint(oo, dd, near, 300); aimP ??= P; return P; };
            if (firstPerson || OpticE >= .5f)
            {
                Vector3? tracer = fp && fp.gameObject.activeSelf ? fp.Muzzle.position : vmMuzzle ? vmMuzzle.position : ch.MuzzlePos;
                Vector3 fh; ok = FpBore ? wu.Fire(o, d, rc, out fh, 0, tracer, 0, aimTo, from) : wu.Fire(o, d, rc, out fh, 0, tracer); if (ok) { LastShotAim = o + d * Vector3.Distance(o, fh); LastShotHit = fh; } if (ok && fp) fp.Fire(false, s.auto);
            }
            else
            {
                // 3ª pessoa: a câmera (atrás do ombro) só escolhe o ponto mirado; o tiro sai do cano até esse ponto (parede na frente do cano pega o tiro)
                ok = wu.Fire(o, d, rc, out var hp, 0, null, near, aimTo, from);
                if (ok) { LastShotAim = aimP ?? o + d * 300; LastShotHit = hp; }
            }
            if (!ok) return;
            ShotCount++;
            bool heavy = s.cls == "escopeta" || s.cls == "sniper"; camShake = Mathf.Min(1.5f, camShake + (heavy ? .45f : .1f));
            // coice visual igual nas duas câmeras: (0,7° + 0,9 × subida do recuo), metade na luneta, até 7°; o empurrão para trás é só da 3ª
            tpsKick = Mathf.Min(tpsKick + (.7f + wu.recoil.lastKick * .9f) * (FBScopes.Scoped ? .5f : 1), 7); if (!firstPerson) tpsPunch = Mathf.Min(.3f, tpsPunch + (heavy ? .16f : .05f));
            vmKick = Mathf.Min(1, vmKick + (s.IsPistol ? .7f : .35f));
            if (FBScopes.Scoped) FBHUD.I?.ScopeKick();
            FBNet.I?.SendShot(ch);
        }
        // ponto que a câmera mira (aimPoint do main.js): parede mais próxima depois de 'near' (pula o trecho câmera→corpo) ou inimigo/alvo de
        // treino mais próximo em qualquer ponto da linha da câmera
        Vector3 AimPoint(Vector3 o, Vector3 d, float near, float far)
        {
            var hits = Physics.RaycastAll(o, d, far, FBWorld.ShotMask, QueryTriggerInteraction.Collide); float best = far;
            foreach (var h in hits)
            {
                if (h.distance >= best) continue;
                var hb = h.collider.GetComponent<FBHitbox>();
                if (hb) { if (hb.owner == ch || !hb.owner.alive || (!hb.owner.civ && !FBMatch.Hostile(hb.owner, ch))) continue; best = h.distance; continue; }
                if (h.collider.GetComponentInParent<FBTarget>()) { best = h.distance; continue; }           // alvo de papel do estande
                if (h.collider.isTrigger || h.collider.gameObject.layer == FBWorld.LGlass || h.collider.GetComponentInParent<FBCharacter>() == ch) continue;
                if (h.distance >= near) best = h.distance;
            }
            return o + d * best;
        }
        float Near(Vector3 camPos) => Mathf.Max(0, (camPos - ch.Pos).magnitude - 1.2f * ch.ScaleK);
        // de onde o tiro sai de verdade na 3ª pessoa (shotOrigin): o cano — ou a cabeça, se o cano estiver enfiado na parede
        Vector3 ShotOrigin()
        {
            var m = ch.MuzzlePos; var e = ch.Head ? ch.Head.position : ch.Pos + Vector3.up * 1.6f * ch.ScaleK; var d = m - e; float L = d.magnitude;
            return L > .01f && FBWorld.RayHit(e, d / L, L) ? e : m;
        }
        // r52: origem e direção do tiro do jogador — a MESMA no Fire e no indicador de impacto: a câmera escolhe o ponto mirado (o ponto sob o
        // retículo), a bala sai do cano até ele
        void ShotGeom(out Vector3 o, out Vector3 d, out float near, out Vector3 from)
        {
            o = cam.transform.position; d = FlBlend ? AnchorFwd : cam.transform.forward;
            if (firstPerson || OpticE >= .5f) { near = 0; from = FpBore ? FpShotOrigin(o, d) : o; }
            else { near = Near(o); from = ShotOrigin(); }
        }
        // r52: 1ª pessoa / mira óptica — antes a bala saía do OLHO; o cano (uns 6–8 cm abaixo da visada) não contava: com a visada passando
        // rente à quina (capô, muro, batente) a bala passava por onde a arma não passaria. Agora sai do cano da arma do rig de 1ª pessoa até o
        // ponto sob o retículo, como na 3ª; cano enfiado na parede (encostado nela) = sai do olho. -fbespreitarantigo = do olho
        public static bool FpBore = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbespreitarantigo") < 0;
        public Vector3 FpMuzzle(Vector3 o, Vector3 d)
        {
            float S = ch.ScaleK; var m = o + d * .8f * S - cam.transform.up * .08f * S;                 // sem rig: cano ~55 cm à frente, abaixo da visada
            if (fp && fp.gameObject.activeSelf && fp.Muzzle) { var fm = fp.Muzzle.position; var rel = fm - o; float a = Vector3.Dot(rel, d); if (a > .2f * S && a < 1.6f * S && (rel - d * a).magnitude < .35f * S) m = fm; }
            return m;
        }
        Vector3 FpShotOrigin(Vector3 o, Vector3 d) { var m = FpMuzzle(o, d); var dm = m - o; float L = dm.magnitude; return L > .01f && FBWorld.RayHit(o, dm / L, L) ? o : m; }
        // r52 INDICADOR DE IMPACTO: onde o próximo tiro para, pela mesma conta do Fire (ShotGeom + AimPoint + BulletFrom sem efeitos), sem a
        // dispersão aleatória, com a câmera FINAL do quadro. Na luneta (≥ 2,5x) é o X vermelho e branco dentro da lente; com o cano sem linha
        // (3ª pessoa, 1ª pessoa encostado na quina) o X do HUD vai para o ponto real do impacto em vez do centro da mira.
        public bool ImpactOn { get; private set; } public bool ImpactBlocked { get; private set; } public Vector3 ImpactPoint { get; private set; } public Vector3 ImpactFrom { get; private set; } public FBCharacter ImpactHit { get; private set; } public Collider ImpactCol { get; private set; }
        void PredictImpact()
        {
            var s = wu.Cur; ImpactOn = ImpactBlocked = false; ImpactHit = null; ImpactCol = null;
            if (s == null || !ch.alive || Driving || ch.vehiclePose || ch.special) { AimBlocked = false; return; }
            ShotGeom(out var o, out var d, out float near, out var from);
            var P = AimPoint(o, d, near, 300); var dv = P - from; float L = dv.magnitude; if (L < .05f) { AimBlocked = false; return; }
            var r = FBCombat.BulletFrom(from, dv / L, from, ch, s, 0, false, L + .05f, 1, true);
            ImpactOn = true; ImpactPoint = r.end; ImpactFrom = from; ImpactHit = r.hit; ImpactCol = r.col; ImpactBlocked = !r.hit && r.dist < L - .25f * ch.ScaleK;
            AimBlocked = ImpactBlocked && L > 1.5f;
        }
        // para onde a arma aponta (tronco/braços do FBRig): o mundo sob o retículo; colado na câmera (< 3,5 m) vale um ponto 120 m à frente
        Vector3 RigAimPoint()
        {
            var o = cam.transform.position; var d = flOn || FlBlend ? AnchorFwd : cam.transform.forward;   // câmera livre: a arma não segue a câmera
            return FBWorld.RayHit(o, d, 250, out var h) && h.distance >= 3.5f ? h.point : o + d * 120;
        }
        // MIRA BLOQUEADA: o ponto que a câmera mira não é alcançável pelo cano → retículo vermelho (hud.setBlocked). r52: vem do PredictImpact
        // (a mesma conta do tiro, todo quadro, com a câmera final); aqui fica o espreitar com o cano (20×/s)
        void BlockedCheck(float dt)
        {
            if (wu.Cur == null || !ch.alive) { AimBlocked = false; ch.peekExtra = 0; return; }
            blkT -= dt; if (blkT > 0) return; blkT = .05f;
            PeekClear();
        }
        // r52 ESPREITAR COM O CANO (pedido: "os disparos acertam o carro/parede mesmo com a mira no inimigo e visão livre"). A câmera de 3ª
        // pessoa fica atrás e AO LADO da cabeça (Q: além do ombro esquerdo; E: além do direito, mais o passo do corpo) e enxerga além da quina
        // antes do cano — a arma fica no ombro direito, perto do corpo. Com Q/E e arma: se a linha cano → ponto mirado bate num obstáculo PERTO
        // do cano (a própria cobertura), o corpo sai mais para o lado: o MÍNIMO para o cano ter linha, até PeekMax e dentro da folga medida
        // contra a parede (peekRoom). Sem linha possível, não sai — o tiro respeita a colisão e o indicador de impacto mostra onde ele para.
        // A câmera segue só o passo da inclinação (PeekBase): o cano alcança o que ela mostra, sem a câmera fugir junto.
        public static bool PeekFix = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbespreitarantigo") < 0; public const float PeekMax = .6f;
        void PeekClear()
        {
            float want = 0, sg = Mathf.Sign(lean), S = ch.ScaleK;
            if (PeekFix && Mathf.Abs(lean) > .3f && !firstPerson && OpticE < .5f && !Driving)
            {
                var co = cam.transform.position; var P = AimPoint(co, FlBlend ? AnchorFwd : cam.transform.forward, Near(co), 300);
                var r = transform.right * sg; var m0 = ch.MuzzlePos - transform.right * (ch.PeekShift - ch.PeekBase);   // cano sem o extra já aplicado
                float max = Mathf.Min(PeekMax * S, Mathf.Max(0, ch.peekRoom - Mathf.Abs(ch.PeekBase)));
                if ((P - m0).magnitude > 2.5f * S)
                    for (float k = 0; k <= max + 1e-3f; k += .05f * S)
                    {
                        var m = m0 + r * k; var dv = P - m; float L = dv.magnitude;
                        if (!(FBWorld.RayHit(m, dv / L, L - .25f, out var h) && h.distance < 2.5f * S)) { want = k > 0 ? Mathf.Min(max, k + .03f * S) : 0; break; }
                    }
            }
            ch.peekExtra = want;
        }
        // zoom óptico de verdade (zoomFov do main.js): 2× = metade da tangente do meio-FOV
        public static float ZoomFov(float fov, float z) => z <= 1 ? fov : 2 * Mathf.Atan(Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / z) * Mathf.Rad2Deg;

        // ---- mira assistida ORIGINAL (padrão) — trava em um alvo, perdeu = 0,6 s sem assistência, cabeça exige esforço
        FBCharacter assistTgt; float assistCool, assistPull, assistFric = 1;
        void AimAssist(float dt, bool aiming, FBInputState inp)
        {
            assistFric = 1; float pull = assistPull; assistPull = 0; assistCool = Mathf.Max(0, assistCool - dt);
            if (FBSession.aimAssist == "manual" || wu.Cur == null || wu.Cur.IsSniper || !(aiming || inp.fire) || !ch.alive || ch.special || FBMatch.I.freeze) { assistTgt = null; return; }
            if (FBSession.aimAssist == "ff") { FFAssist(dt, aiming, inp); return; }
            var co = cam.transform.position; (float ex, float ey, float d, FBCharacter a)? best = assistTgt ? Probe(assistTgt, co, aiming, 1.6f) : null;
            if (assistTgt && (best == null || pull * Mathf.Sign(best.Value.ex) < -.012f * Mathf.Rad2Deg)) { assistTgt = null; best = null; assistCool = .6f; }
            if (best == null) { if (assistCool > 0) return; float bd = 1e9f; foreach (var a in FBMatch.Actors()) { var p = Probe(a, co, aiming, 1); if (p != null && p.Value.d < bd) { bd = p.Value.d; best = p; } } if (best == null) return; assistTgt = best.Value.a; }
            var b = best.Value; float cap = (aiming ? 22 : 12) * dt, g = Mathf.Min(1, dt * (aiming ? 6 : 4));
            camYaw += Mathf.Clamp(b.ex * g * Mathf.Rad2Deg, -cap, cap);
            if (b.ey > 0) camPitch += Mathf.Clamp(b.ey * g * Mathf.Rad2Deg, -cap, cap);
            else { var hl = cam.transform.InverseTransformPoint(b.a.Head.position + Vector3.up * .12f); float eyHead = Mathf.Atan2(hl.y, new Vector2(hl.x, hl.z).magnitude); float down = (eyHead < 0 ? 9 : 5) * dt; camPitch += Mathf.Max(b.ey * g * .35f * Mathf.Rad2Deg, -down); }
            assistFric = .8f;
        }
        (float ex, float ey, float d, FBCharacter a)? Probe(FBCharacter a, Vector3 co, bool aiming, float coneK)
        {
            if (!a || a == ch || !a.alive || !FBMatch.Hostile(a, ch) || !a.Head) return null;
            var c = Vector3.Lerp(a.Pos, a.Head.position, .72f); var l = cam.transform.InverseTransformPoint(c); if (l.z < 1) return null; float dist = l.z; if (dist > 90) return null;
            float ex = Mathf.Atan2(l.x, dist), ey = Mathf.Atan2(l.y, new Vector2(l.x, dist).magnitude), cone = ((aiming ? 2.2f : 1.4f) * Mathf.Deg2Rad + Mathf.Atan(.3f / dist)) * coneK, d = new Vector2(ex, ey).magnitude;
            if (d > cone) return null; var dd = c - co; if (FBWorld.RayHit(co, dd.normalized, dd.magnitude - .4f)) return null; return (ex, ey, d, a);
        }
        float ffFireT;
        void FFAssist(float dt, bool aiming, FBInputState inp)                // Free Fire (aimassist.js): puxão forte no começo da rajada, cai em 1,5 s
        {
            var co = cam.transform.position; FBCharacter best = null; float bs = -1;
            foreach (var a in FBMatch.Actors()) { if (!a || a == ch || !a.alive || !FBMatch.Hostile(a, ch)) continue; var p = Vector3.Lerp(a.Pos, a.Head.position, .55f); var v = cam.WorldToViewportPoint(p); if (v.z < .6f || v.z > 80 || Mathf.Abs(v.x - .5f) > .12f || Mathf.Abs(v.y - .5f) > .16f) continue; if (FBWorld.RayHit(co, (p - co).normalized, (p - co).magnitude - .4f)) continue; float sc = 1 / (1 + v.z * .02f) - Vector3.Angle(cam.transform.forward, p - co) * .01f; if (sc > bs) { bs = sc; best = a; } }
            if (best != assistTgt) ffFireT = 0; assistTgt = best; if (!best) { ffFireT = 0; return; }
            if (aiming) assistFric = .65f; if (!inp.fire) { ffFireT = 0; return; }
            float w = Mathf.Clamp01(1 - ffFireT / 1.5f); ffFireT += dt; if (w <= 0) return; float f = 1 - Mathf.Exp(-dt * 10 * w);
            var dd = Vector3.Lerp(best.Pos, best.Head.position, .55f) - co; float yaw = Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg, pit = Mathf.Atan2(dd.y, dd.Flat().magnitude) * Mathf.Rad2Deg;
            camYaw += Mathf.DeltaAngle(camYaw, yaw) * f; camPitch += (pit - camPitch) * f;
        }

        // ---- recarga, kit, deitar, pular janela, reanimar
        void StartReload() { if (FBMed.Busy) return; bool empty = wu.Ammo == 0; if (wu.Reload() && fp && firstPerson && wu.Cur != null) { if (wu.Cur.cycled) fp.ReloadCycled(Mathf.Min(wu.Cur.mag - wu.Ammo, wu.Reserve), null); else fp.Reload(empty); } }
        void TryHeal()
        {
            if (FBMed.Busy) { CancelHeal("", true); return; } if (ch.knocked) return;
            var w = FBMed.Why(ch); if (w == null && (hv.Flat().magnitude > .6f * ch.ScaleK || !onGround || ch.vehiclePose)) w = "PARE PARA SE CURAR"; if (w == null && (proneSt == 1 || proneSt == 3)) w = "MUDANDO DE POSIÇÃO";
            if (w != null) { if (w != "morto") { FBHUD.I?.Center(w, 1.2f); FBAudio.Synth("deny"); } return; }
            if (autoRun) { autoRun = false; FBHUD.I?.SetRun(false); } if (wu.reloadT > 0) wu.reloadT = 0; if (wu.switching > 0) return;
            FBMed.Begin(); FBAudio.Synth("medopen");
        }
        void CancelHeal(string why, bool redraw = true)
        {
            bool spent = FBMed.spent; if (!FBMed.Cancel()) return; FBAudio.Synth("medcancel"); healEnd = 0; HealCancels++;
            if (!string.IsNullOrEmpty(why)) FBHUD.I?.Center(why + (spent ? " · CURA INTERROMPIDA" : " · KIT NÃO FOI GASTO"), 1.4f);
        }
        public static int HealCancels; public static string LastHealCancel;                     // laboratório (-fbcuralab)
        void HealGuard(in FBInputState inp)
        {
            string why = !ch.alive || ch.knocked ? "" : inp.move.sqrMagnitude > .04f || autoRun ? "ANDOU" : inp.jump ? "PULOU" : inp.sprintHeld && inp.move.y > .1f ? "CORREU"
                : inp.fire || inp.firePressed ? "ATIROU" : inp.aimPressed || inp.aim ? "MIROU" : inp.reload ? "RECARREGOU" : inp.slot >= 0 || inp.cycle != 0 ? "TROCOU DE ARMA"
                : inp.crouchToggle || inp.prone ? "MUDOU DE POSIÇÃO" : inp.interact ? "INTERAGIU" : inp.buy || inp.bag || inp.arsenal ? "ABRIU O MENU" : inp.inspect ? "INSPECIONOU"
                : !onGround ? "SAIU DO CHÃO" : ch.vehiclePose ? "ENTROU NO VEÍCULO" : null;
            if (why != null) { LastHealCancel = why == "" ? "caiu/morreu" : why; CancelHeal(why); }
        }
        const string ProneDownClip = "FF_SYS_Prone_Down", ProneUpClip = "FF_SYS_KO_GetUp";
        static float ClipLen(string c, float fb) { var k = FBRegistry.I ? FBRegistry.I.Clip(c) : null; return k ? k.length : fb; }
        // [parceiro] fisica 31/10: tem teto entre os pes e a altura topo (m, ja escalada)? Antes era um raio fino no centro da capsula: agachado com o corpo
        // meio debaixo de balcao/banco/toldo/retrovisor o raio passava livre, o cc.height crescia para dentro da beirada e o CC expulsava o
        // jogador de lado/para baixo (tranco) ou prendia. Agora SphereCast com 0,9 do raio da capsula e o mesmo topo. Mureta encostada nao
        // bloqueia: a varredura e vertical, fica ~3 cm dentro da pele, e o SphereCast ignora o que ja sobrepoe na largada.
        bool SemEspaco(float topo)
        {
            float rr = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * .9f, o0 = .4f * ch.ScaleK;
            return FBWorld.RayHit(transform.position + Vector3.up * o0, Vector3.up, Mathf.Max(.05f, topo - o0 - rr), rr);
        }
        bool SetProne(bool on)
        {
            if (!ch.alive || vault != null || ch.knocked) return false;
            if (on)
            {
                if (proneSt != 0 || !onGround) return false;                                    // levantando: não deita de novo no meio
                proneSt = 1; proneLen = Has(ProneDownClip) != null ? ClipLen(ProneDownClip, 1.2f) : .45f; crouch = false; autoRun = false; FBHUD.I?.SetRun(false); FBAudio.Play2D("ie_slide", .5f);
            }
            else
            {
                if (proneSt != 2) return false;                                                 // deitando: termina de deitar antes
                if (SemEspaco((Json.F(GameData.Section("controller"), "m_Height", 1.8f) + .2f) * ch.ScaleK)) { FBHUD.I?.Center("SEM ESPAÇO PARA LEVANTAR", 1); return false; }   // [parceiro 31/10] mesmo topo, agora com a largura da cápsula
                proneSt = 3; proneLen = Has(ProneUpClip) != null ? ClipLen(ProneUpClip, 1.2f) : .45f; FBAudio.Play2D("ie_crouch", .35f);
            }
            proneT = 0; prone = true; ch.poseT = Time.time; return true;
        }
        // quanto do corpo está deitado (0 em pé, 1 no chão): altura da cápsula, pivô da câmera e alinhamento ao chão. O levantar do FF fica
        // ~25% do tempo no chão (empurra com as mãos) e sobe até ~90%; o deitar (o mesmo clipe ao contrário) desce no espelho disso
        float ProneAmount()
        {
            float u = proneLen > 0 ? Mathf.Clamp01(proneT / proneLen) : 1;
            return proneSt == 1 ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .75f, u)) : proneSt == 2 ? 1 : proneSt == 3 ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .9f, u)) : 0;
        }
        string ProneClip(float hs) => proneSt == 1 ? Has(ProneDownClip) ?? "FF_SYS_Prone" : proneSt == 3 ? Has(ProneUpClip) ?? "FF_SYS_Prone" : hs > .4f && Has("FF_SYS_Crawl") != null ? "FF_SYS_Crawl" : "FF_SYS_Prone";
        // deitado o corpo ocupa ~1,9 m no chão (cabeça/braços ~0,95 m à frente do quadril, pés ~1,05 m atrás) e a cápsula só 0,3 m em volta do
        // centro: parede na frente ou atrás = cabeça/pés DENTRO dela. Mede os dois lados rente ao chão e desloca o corpo (FBCharacter.ProneShift)
        // para longe; rastejar contra a parede trava a componente que entraria nela
        public static float ProneHead = .95f, ProneFeet = 1.05f; float proneFront = 9, proneBack = 9;
        void ProneRoom(float dt)
        {
            float k = ch.ScaleK, want = 0; proneFront = proneBack = 9;
            if (proneSt != 0 && ch.alive)
            {
                var f = Quaternion.Euler(0, ch.Yaw, 0) * Vector3.forward; float head = ProneHead * k, feet = ProneFeet * k;
                foreach (var hy in new[] { .15f, .35f })
                {
                    var o = transform.position + Vector3.up * hy * k;
                    if (FBWorld.RayHit(o, f, head, out var hf)) proneFront = Mathf.Min(proneFront, hf.distance);
                    if (FBWorld.RayHit(o, -f, feet, out var hb)) proneBack = Mathf.Min(proneBack, hb.distance);
                }
                float inF = proneFront < head ? head - proneFront : 0, inB = proneBack < feet ? feet - proneBack : 0;
                want = Mathf.Clamp(inB - inF, -.8f * k, .8f * k) * ch.ProneK;
            }
            ch.ProneShift = Mathf.Lerp(ch.ProneShift, want, 1 - Mathf.Exp(-dt * 10));
        }
        bool TryVault()
        {
            if (!FirmGround || prone || ch.special || FBMed.Busy) return false; if (JumpFix && (Time.time - vaultEndT < .35f || Time.time - lastJumpT < .25f)) return false; float S = ch.ScaleK;
            var f = Quaternion.Euler(0, camYaw, 0) * Vector3.forward; var b = transform.position;
            if (!FBWorld.RayHit(b + Vector3.up * .5f * S, f, 1.2f * S, out var knee)) return false;
            float d = knee.distance + .07f; var top = new Vector3(b.x + f.x * d, b.y + 1.95f * S, b.z + f.z * d);
            if (!FBWorld.RayHit(top, Vector3.down, 1.95f * S, out var ht)) return false;
            float ledge = top.y - ht.distance - b.y; if (ledge < .45f * S || ledge > 1.75f * S) return false;
            if (FBWorld.RayHit(b + Vector3.up * (ledge + .3f * S), f, d + 1.2f * S)) return false;
            var side = new Vector3(f.z, 0, -f.x);
            foreach (var hy in new[] { .35f, .75f }) for (float o = -.22f; o <= .2201f; o += .04f) if (FBWorld.RayHit(b + side * o * S + Vector3.up * (ledge + hy * S), f, d + .35f * S)) return false;
            var land = new Vector3(b.x + f.x * (d + 1.1f * S), b.y + ledge, b.z + f.z * (d + 1.1f * S));
            // pouso no piso de verdade: obstáculo fino (mureta, cerca) = cai do outro lado; mesa/laje = fica em cima; vazio/queda alta = não pula
            if (!FBWorld.RayHit(land + Vector3.up * .5f * S, Vector3.down, ledge + .5f * S + 1.3f * S, out var lh)) return false;
            if (lh.point.y < b.y - 1.2f * S || lh.point.y > b.y + ledge + .2f * S) return false; land.y = lh.point.y;
            vault = (0, .567f * (1 + Mathf.Max(0, ledge / S - 1) * .35f), b, land, ledge + .3f * S, 0, camYaw, d); autoRun = false; FBHUD.I?.SetRun(false); hv = Vector3.zero; Vaults++; return true;
        }
        void VaultStep(float dt)
        {
            var v = vault.Value; v.t += dt; float u = Mathf.Min(1, v.t / v.dur), e = u * u * (3 - 2 * u); ch.Yaw = v.yaw;
            var vc = FBRegistry.I ? FBRegistry.I.Clip("FF_SYS_Vault") : null; ch.VaultRate = vc ? Mathf.Clamp(vc.length * .92f / Mathf.Max(.2f, v.dur), 1, 3) : 0;
            if (v.snd == 0 && v.t >= .068f * v.dur / .567f) { v.snd = 1; FBAudio.Play2D("sg_getover", .7f, .04f); }
            // pouso: passo/pouso do PISO de verdade (o sg_land do Wwise do Sigma é uma variante de superfície — soava como pulo na água)
            if (v.snd == 1 && v.t >= .578f * v.dur / .567f - .02f) { v.snd = 2; FBFootsteps.LandNow(ch, .55f); FBAudio.Play2D("ie_land", .32f, .08f); }
            var p = new Vector3(v.a.x + (v.b.x - v.a.x) * e, VaultY(v, e), v.a.z + (v.b.z - v.a.z) * e);
            foreach (var g in Physics.OverlapSphere(p + Vector3.up * .7f, .5f, 1 << FBWorld.LGlass))
            {
                var gl = g.GetComponent<FBGlass>(); var mf = g.GetComponent<MeshFilter>();
                if (gl && (!mf || !mf.sharedMesh || mf.sharedMesh.subMeshCount <= 1)) gl.Break(p, (v.b - v.a).Flat().normalized);
            }
            cc.enabled = false; transform.position = p; cc.enabled = true;
            vy = 0;                                                                                // pulo de janela não acumula queda
            if (u >= 1)
            {
                vault = null; ch.VaultRate = 0; vy = 0; var wy2 = FBWorld.WaterAt(p.x, p.z); wasInWater = wy2 != null && p.y < wy2 + .15f;
                if (JumpFix) { hv = (v.b - v.a).Flat() / Mathf.Max(.2f, v.dur) * .5f; hv = Vector3.ClampMagnitude(hv, WalkSp * ch.ScaleK); onGround = false; groundT = 0; vaultEndT = Time.time; jumpQ = -9; }   // velocidade de verdade (m/s), e "no ar" até o FBMotor achar o chão
                else hv = (v.b - v.a).Flat() * 1.5f;
            }
            else vault = v;
        }
        // altura do percurso do pulo guiada pelo AVANÇO HORIZONTAL e (e = 0..1 da trajetória), não pelo tempo: o pé já está acima da quina quando chega
        // na parede (antes subia pela fase do tempo e o corpo atravessava a quina), passa por cima, e só desce depois de passar, até o piso do pouso
        float VaultY((float t, float dur, Vector3 a, Vector3 b, float h, int snd, float yaw, float wall) v, float e)
        {
            float S = ch.ScaleK, dist = Mathf.Max(.01f, (v.b - v.a).Flat().magnitude), wf = Mathf.Clamp01(v.wall / dist);
            float riseEnd = Mathf.Clamp(wf * .8f, .08f, .6f), fallStart = Mathf.Clamp(wf + .45f * S / dist, .4f, .85f);
            float rise = Mathf.SmoothStep(0, 1, e / riseEnd), fall = 1 - Mathf.SmoothStep(0, 1, (e - fallStart) / Mathf.Max(.05f, 1 - fallStart));
            float ledgeH = v.h - .3f * S, baseW = Mathf.Lerp(v.a.y, v.b.y, wf);                       // altura da quina e da reta início→pouso na parede
            float arc = Mathf.Max(.05f * S, v.a.y + ledgeH + .18f * S - baseW);                       // quanto passar por cima da reta (pés 18 cm acima da quina)
            return Mathf.Lerp(v.a.y, v.b.y, e) + arc * rise * fall;
        }
        void ReviveStep(float dt, bool canMove, FBInputState inp)
        {
            FBCharacter cand = null; float now = Time.time;
            if (FBMatch.KnockOn && canMove && ch.alive && !ch.knocked)
            {
                float bd = 9; foreach (var o in FBMatch.TeamOf(ch)) if (o.alive && o.knocked && !(o.revivedBy != null && o.revivedBy != (object)ch && now - o.reviveTick < .3f))
                    { var dx = o.Pos - ch.Pos; float d = dx.Flat().magnitude; if (d < bd && FBMechanics.CanReviveTarget(dx.x, dx.z, d / ch.ScaleK, camYaw * Mathf.Deg2Rad)) { bd = d; cand = o; } }
            }
            reviveCand = cand;
            if (cand && inp.interactHeld) { reviving = cand; cand.revivedBy = ch; cand.reviveTick = now; if (FBMechanics.ReviveStep(cand, dt, FBSession.mode)) { FBCombat.CompleteRevive(cand, ch); reviving = null; cand = null; } }
            else reviving = null;
            FBHUD.I?.Revive(cand ? (reviving ? "REANIMANDO " : "SEGURE F · REANIMAR ") + cand.Name : null, cand ? cand.reviveP : 0);
        }

        // ---- câmeras
        void ApplyView()
        {
            if (fp && wu.Cur != null) fp.SetWeapon(wu.Cur);
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            if (firstPerson) BuildViewModel(); else if (vm) Destroy(vm);
        }
        void BuildViewModel()
        {
            if (vm) Destroy(vm); vmMuzzle = null; var s = wu.Cur; var model = s != null && FBRegistry.I ? FBRegistry.I.Weapon(s.id) : null; if (!model || !firstPerson) return;
            vm = FBWeaponModel.Make(s, cam.transform); vm.name = "ArmaVisao"; foreach (var c in vm.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in vm.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.gameObject.layer = 0; }
            foreach (var t in vm.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("SOCKET_Muzzle")) vmMuzzle = t;
            FBFpArms.Attach(vm, ch, s);
        }

        static readonly int __fbt_LateUpdate = FBTempo.Id("FBPlayer.LateUpdate");   // [parceiro 01/11] medidor FBTempo
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            float dt = Time.deltaTime; if (dt <= 0) return;
            StepSmooth(dt);
            float ik = 1 - Mathf.Exp(-dt * Json.F(GameData.Section("cameraLook"), "interpolationSpeed", 45));
            camYawS = Mathf.LerpAngle(camYawS, camYaw, ik); camPitchS = Mathf.Lerp(camPitchS, camPitch, ik);
            float fk = 1 - Mathf.Exp(-dt * (flOn ? 30 : 9));                                    // entra colado no mouse, volta suave (~0,3 s)
            flYawS = Mathf.Lerp(flYawS, flYaw, fk); flPitchS = Mathf.Lerp(flPitchS, flPitch, fk); if (!flOn && !FlBlend) flYawS = flPitchS = 0;
            tpsKick *= Mathf.Exp(-dt * 9); tpsPunch *= Mathf.Exp(-dt * 11); vmKick = Mathf.Max(0, vmKick - dt * 6);
            // r60: drena aqui, antes dos retornos da oficina/garagem/BR/veículo — senão o contador ficaria preso e o corpo continuaria
            // visível na 1ª pessoa para sempre
            if (viewBlendT > 0) viewBlendT = Mathf.Max(0, viewBlendT - dt);
            ImpactOn = false; AimBlocked = false; FBHUD.I?.PlaceImpact(false, default);
            if (FBOficina.Camera(cam, dt) || FBGarage.Camera(cam, dt)) return;
            // no veículo a câmera é sempre de 3ª pessoa: vindo da 1ª pessoa o corpo ficava só com a sombra (SetBodyVisible(false) e o laço
            // fpHideT não eram desfeitos — este retorno vinha antes do ramo da 3ª pessoa) e o modelo de 1ª pessoa continuava ligado.
            // 22/10: piloto "sem braço"/peças do corpo sumidas na moto depois de usar a 1ª pessoa.
            if (Driving && ch.alive) { if (vm) vm.SetActive(false); SetBodyVisible(true); FBVehicles.Camera(cam, dt, camYawS, camPitchS); return; }
            if (FBBattleRoyale.Camera(cam, dt, camYawS, camPitchS, ch)) return;
            var s = wu.Cur; bool aim = ch.aiming && ch.alive; var target = ch.alive ? ch : FBSpectator.CamTarget(ch);                                // r49: espectador (FBSpectator) — morto, segue um vivo
            float fov0 = FovBase; var rot = Quaternion.Euler(-(camPitchS + flPitchS) - tpsKick, camYawS + flYawS, 0);
            float oe = target == ch ? OpticE : 0; FBScopes.Set(oe >= .5f, oe, FBScopes.Zoom(s));   // r51: estado da mira óptica (HUD, coice, tremor)
            if (firstPerson && ch.alive && !ch.special && target == ch)
            {
                // arma/coldre criados DEPOIS de entrar na 1ª pessoa (troca de arma, saque) nasciam visíveis: duas armas na tela
                if ((fpHideT -= dt) <= 0) { fpHideT = .2f; foreach (var r in ch.GetComponentsInChildren<Renderer>(false)) if ((!vm || !r.transform.IsChildOf(vm.transform)) && (!fp || !r.transform.IsChildOf(fp.transform)) && r.gameObject.layer != FBWorld.LFirstPerson) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; }
                var head = ch.Head ? ch.Head.position : transform.position + Vector3.up * 1.7f * ch.ScaleK;
                head += Vector3.up * (camOff - (ch.feet ? ch.feet.HipsOffset : 0));   // a pelve do IK já absorveu o degrau no osso da cabeça: tira o dela e põe o amortecimento da câmera (não amortece duas vezes)
                if (fp && fp.gameObject.activeSelf)
                {
                    FpCameraPose(out var lp, out var lq);
                    cam.transform.SetPositionAndRotation(lp, lq * Quaternion.Euler(-tpsKick, 0, 0));
                    if (FlBlend) { cam.transform.rotation = Quaternion.AngleAxis(flYawS, Vector3.up) * cam.transform.rotation; cam.transform.Rotate(-flPitchS, 0, 0, Space.Self); }   // câmera livre: só a cabeça vira; a arma fica à frente
                }
                else
                {
                    cam.transform.SetPositionAndRotation(head + rot * new Vector3(0, .07f, .08f) * ch.ScaleK, rot);
                    // espreitar além da quina com colisão
                    if (Mathf.Abs(lean) > .01f) { float lx = lean * .35f; if (FBWorld.RayHit(cam.transform.position, cam.transform.right * Mathf.Sign(lx), Mathf.Abs(lx) + .15f, out var h, .05f)) lx = Mathf.Sign(lx) * Mathf.Max(0, h.distance - .15f); cam.transform.position += cam.transform.right * lx; cam.transform.Rotate(0, 0, -lean * 8, Space.Self); }
                }
                if (camShake > 0) { camShake = Mathf.Max(0, camShake - dt * 2); float zk = FBScopes.Scoped && s != null ? 1 / FBScopes.Zoom(s) : 1; cam.transform.Rotate(Random.Range(-1f, 1f) * camShake * .7f * zk, Random.Range(-1f, 1f) * camShake * .7f * zk, 0, Space.Self); }
                float fov = ZoomFov(fov0 * (ch.sprint ? Json.F(GameData.Section("character"), "fieldOfViewRunningMultiplier", 1.1f) : 1), aim && s != null ? FBScopes.AdsZoom(s) : 1);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1 - Mathf.Exp(-dt * 10));
                if (vm && fp) Destroy(vm);
                if (vm && s != null)
                {
                    vm.SetActive(!FBMed.Busy && healEnd <= 0 && !ch.knocked && !reviving && !FBScopes.Scoped && Mathf.Abs(flYawS) < 12);
                    // offsets do fpdata (itemOffsets: parado/mirando/correndo/agachado), balanço do olhar e do andar, coice
                    var loc = new Vector3(.16f, -.19f, .38f); var ads = FBScopes.AdsOffset(vm, cam); var w = aim ? ads : loc;
                    vmBob += dt * new Vector2(hv.x, hv.z).magnitude * 1.6f; var bob = !aim ? new Vector3(Mathf.Sin(vmBob) * .008f, Mathf.Abs(Mathf.Cos(vmBob)) * .01f, 0) : Vector3.zero;
                    var sway = new Vector3(-swayX * .0015f, -swayY * .0015f, 0) * (aim ? .3f : 1);
                    vm.transform.localPosition = Vector3.Lerp(vm.transform.localPosition, w + bob + sway + new Vector3(0, 0, -vmKick * .035f) + (ch.sprint ? new Vector3(.04f, -.04f, -.03f) : Vector3.zero), 1 - Mathf.Exp(-dt * 18));
                    vm.transform.localRotation = Quaternion.Slerp(vm.transform.localRotation, Quaternion.Euler(-vmKick * 4 + (ch.sprint ? 10 : 0), 180 + (ch.sprint ? -25 : 0), 0) * Quaternion.Euler(0, 0, swayX * .4f), 1 - Mathf.Exp(-dt * 16));
                    vm.transform.localScale = Vector3.one * .6175f;
                }
                else if (s != null && !vm && !fp) BuildViewModel();
                if (vm && s == null) Destroy(vm);
                SetBodyVisible(viewBlendT > 0 && target == ch);   // r60: vindo da 3ª, o corpo fica até a câmera chegar no olho
            }
            else
            {
                if (vm) vm.SetActive(false); SetBodyVisible(true);
                // 3ª pessoa por cima do ombro (updateCamera do main.js, tpsCamera do fpdata): braço/lateral/pivô por estado, suavizados
                bool tAim = aim && target == ch; float hs = target.velocity.Flat().magnitude; bool moving = hs > .5f; lastMoveT = moving ? 0 : lastMoveT + dt; float S = target.ScaleK;
                // mira: valores originais (0,5 m) são da escala do Unity original; nossos modelos são maiores → mínimo por cima do ombro (1,2 / 0,7)
                float armT = !target.alive ? 4 : target.dance != null ? 3.4f : tAim ? Mathf.Max(T("aimArmLength", .5f), 1.2f) : T("armLength", 1.5f) - (lastMoveT > 1 ? T("idleArmPull", .3f) : 0) + (target.sprint ? T("runArmLengthBoost", .4f) : 0) + (target != ch ? 1.2f : 0);
                float baseLat = tAim ? Mathf.Max(T("aimLateralOffset", .5f), .7f) : moving ? T("lateralOffset", .8f) : T("idleLateralOffset", .45f);
                // Q (esquerda): passa para o ombro esquerdo acompanhando o passo lateral real; E (direita): além do ombro direito, só o quanto o corpo saiu
                float pb = Mathf.Abs(PeekFix ? ch.PeekBase : ch.PeekShift);   // r52: o extra do cano (PeekClear) não leva a câmera junto
                float latT = lean < -.05f ? -baseLat + (lean + 1) * .3f - pb * .8f : lean > .05f ? baseLat + pb * 1.1f : baseLat;
                float latSt = Mathf.Abs(lean) > .05f ? T("leanShiftSmoothTime", .12f) : tAim ? 1 / T("aimTransitionSpeed", 12) : T("lateralSmoothTime", .45f);
                tpsArm = Mathf.Lerp(tpsArm, armT * S, 1 - Mathf.Exp(-dt / (tAim ? 1 / T("aimTransitionSpeed", 12) : T("distanceSmoothTime", .4f)) * 2.3f));
                tpsLat = Mathf.Lerp(tpsLat, latT * S, 1 - Mathf.Exp(-dt / latSt * 2.3f));
                float fov = ZoomFov(fov0 * T("tpsAimFovMultiplier", 1), tAim && s != null ? FBScopes.AdsZoom(s) : 1) + (target.sprint && T("enableRunFov", 1) > 0 ? T("runFovBoost", 5) : 0);
                if (oe > 0 && s != null) fov = Mathf.Lerp(fov, ZoomFov(fov0, FBScopes.AdsZoom(s)), oe);   // r51: mira óptica — FOV da 1ª pessoa (aumento leve; a luneta amplia na lente)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1 - Mathf.Exp(-dt * (oe > 0 ? 14 : 8)));
                var f = rot * Vector3.forward; var r = rot * Vector3.right; r.y = 0; r.Normalize();
                float pk = target == ch ? Mathf.Max(ch.ProneK, ch.knocked ? 1 : 0) : target.prone ? 1 : 0;   // r49: deitando/levantando, o pivô desce/sobe junto com o corpo
                pivY = Mathf.Lerp(pivY, ((pk > .001f ? Mathf.Lerp(T("pivotHeight", 1.6f), .45f, pk) : target.crouch ? T("crouchPivotHeight", .95f) : T("pivotHeight", 1.6f)) + (tAim ? T("aimPivotHeightOffset", .12f) : 0)) * S, 1 - Mathf.Exp(-dt / T("verticalPivotSmoothTime", .08f) * 2.3f));
                var piv = FBFall.CamPos(target) + Vector3.up * (pivY + (target == ch ? camOff : 0)); float colR = T("collisionRadius", .25f);
                if (!ch.alive) piv = FBSpectator.Pivot(target, piv, dt);                       // r49: troca de alvo do espectador sem corte seco
                // ombro não atravessa a parede; braço da câmera não atravessa parede/veículo/prédio (encosta na hora, recupera suave)
                float side = Mathf.Sign(tpsLat); if (side == 0) side = 1;
                // folga medida até um comprimento fixo (independe do braço/lateral, que já são suavizados): mudar de estado não atrasa a câmera
                int si = side > 0 ? 1 : 0; latClr[si] = Recover(latClr[si], Probe(piv, r * side, 2 * S, colR), dt); latClr[1 - si] = -1;
                piv += r * side * Mathf.Min(latClr[si], Mathf.Abs(tpsLat));
                var dir = -f; float L = tpsArm;
                collD = Recover(collD, Probe(piv, dir, 6 * S, colR), dt); float d = Mathf.Min(L, collD);   // sem o mínimo de 0,2 m do web: com o pivô perto da parede ele empurrava a câmera para dentro dela (corpo some, ver abaixo)
                cam.transform.position = piv + dir * (d + (target == ch ? tpsPunch * Mathf.Min(1, d) : 0)); cam.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
                cam.transform.Rotate(0, 0, -lean * T("leanCameraRoll", 5), Space.Self);   // E (lean > 0) rola para a direita, como o rotateZ(−lean) do three.js e a 1ª pessoa
                // r51 mira óptica: a câmera vai da posição atrás do ombro até o olho do rig de 1ª pessoa (eixo da mira) com a mesma direção —
                // o ponto mirado não muda durante a transição; a volta faz o caminho inverso
                if (oe > 0 && fp && fp.gameObject.activeSelf)
                {
                    FpCameraPose(out var lp, out var lq);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(cam.transform.position, lp, oe), Quaternion.Slerp(cam.transform.rotation, lq * Quaternion.Euler(-tpsKick, 0, 0), oe));
                }
                if (target == ch && ch.alive)
                {                                                                // câmera dentro do corpo (encostado na parede) ou mira óptica: esconde o modelo
                    var hx = (cam.transform.position - ch.Pos).Flat().magnitude; float cy = cam.transform.position.y - ch.Pos.y, rad = cc.radius * transform.lossyScale.x + .12f, hh = cc.height * transform.lossyScale.y;
                    SetBodyVisible(ch.special || !(oe >= .5f || hx < rad && cy > -.2f && cy < hh + .2f));
                }
                float zk = FBScopes.Scoped && s != null ? 1 / Mathf.Max(1, FBScopes.Zoom(s)) : 1;   // na luneta o tremor é atenuado pelo aumento
                if (camShake > 0 && target == ch) { camShake = Mathf.Max(0, camShake - dt * 2); cam.transform.Rotate(Random.Range(-1f, 1f) * camShake * .7f * zk, Random.Range(-1f, 1f) * camShake * .7f * zk, 0, Space.Self); }
                if (tpsShake > 0) { tpsShake = Mathf.Max(0, tpsShake - dt * T("shakeDecaySpeed", 1.6f)); float a = tpsShake * tpsShake, t = Time.time * T("shakeFrequency", 18); var sm = Json.O(GameData.Section("tpsCamera"), "shakeMaxAngles"); cam.transform.Rotate(Mathf.Sin(t * 1.1f) * a * Json.F(sm, "x", 1.2f), Mathf.Sin(t * 1.3f + 1) * a * Json.F(sm, "y", 1), 0, Space.Self); }
            }
            // r60: sai da pose guardada até a pose do ramo novo, com suavização de entrada/saída. Antes a tecla V dava um salto seco de
            // uns 2 m (olho <-> atrás do ombro). Vem depois do tremor para o tranco não ser suavizado junto.
            if (viewBlendT > 0 && target == ch && ch.alive)
            {
                float u = Mathf.Clamp01(1 - viewBlendT / ViewBlendTime); u = u * u * (3 - 2 * u);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(ch.Pos + viewBlendOff, cam.transform.position, u), Quaternion.Slerp(viewBlendRot, cam.transform.rotation, u));
            }
            if (vmCam) { vmCam.fieldOfView = Json.F(GameData.Section("character"), "fieldOfViewWeapon", 65) * (aim && s != null ? Json.F(FBWeaponModel.Scope(s), "fieldOfViewMultiplierAimWeapon", 1) : 1); vmCam.enabled = (firstPerson || oe >= .5f) && !ch.vehiclePose; }
            if (target == ch) PredictImpact(); else AimBlocked = false;                                 // r52: com a câmera final deste quadro
            FBHUD.I?.PlaceImpact(ImpactOn && AimBlocked && !FBScopes.Scoped, ImpactOn ? cam.WorldToScreenPoint(ImpactPoint) : default);
            if (fp) fp.PipUpdate(cam, vmCam, fov0, FBScopes.Zoom(s), fp.gameObject.activeSelf && target == ch && ch.alive && (firstPerson ? aim : oe >= .5f), ImpactOn ? ImpactPoint : (Vector3?)null);   // r51: câmera da lente no fim (câmera final)
        }
        // olho do rig de 1ª pessoa (sem o coice da arma) + inclinar Q/E. Rodada 39 ("ao espreitar só a câmera inclina"): o deslocamento/rolagem
        // do LPSP e o passo além da quina iam só na câmera; a arma ficava para trás e saía da tela — o RIG inteiro (braços + câmera) vai junto.
        // r51: na mira óptica a 3ª pessoa usa este mesmo olho; yaw/pitch do rig recebem os valores suavizados DESTE quadro (o Tick roda no Update)
        void FpCameraPose(out Vector3 lp, out Quaternion lq)
        {
            fp.transform.position += Vector3.up * camOff;                // rig (braços + câmera) sobe junto, sem o tranco do degrau
            if (!firstPerson) { fp.transform.rotation = Quaternion.Euler(0, camYawS, 0); fp.pitchNode.localRotation = Quaternion.Euler(-camPitchS, 0, 0); }
            fp.CameraPose(out var cp, out var cq);
            lq = cq * Quaternion.Euler(0, 0, fp.camLeanRot.z); lp = cp + cq * new Vector3(fp.camLeanLoc.x, fp.camLeanLoc.y, 0);
            if (Mathf.Abs(lean) > .01f) { var rt = lq * Vector3.right; float lx = lean * .35f; if (FBWorld.RayHit(lp, rt * Mathf.Sign(lx), Mathf.Abs(lx) + .15f, out var h, .05f)) lx = Mathf.Sign(lx) * Mathf.Max(0, h.distance - .15f); lp += rt * lx; lq *= Quaternion.Euler(0, 0, -lean * 8); }
            var dq = lq * Quaternion.Inverse(cq); fp.transform.SetPositionAndRotation(lp + dq * (fp.transform.position - cp), dq * fp.transform.rotation);
        }
        // distância livre de o até L na direção dir (raio fino + esfera para o plano próximo não entrar na parede): parede/veículo/prédio
        static float Probe(Vector3 o, Vector3 dir, float L, float r)
        {
            float free = L;
            if (FBWorld.RayHit(o, dir, L + r, out var h)) free = Mathf.Min(free, h.distance - r);
            if (FBWorld.RayHit(o, dir, L, out var hs, r * .5f)) free = Mathf.Min(free, hs.distance);
            return Mathf.Max(0, free);
        }
        // colisão com recuperação suave: encosta na hora (nunca atravessa), volta para a distância livre com collisionRecoverSpeed
        static float Recover(float cur, float free, float dt) => cur < 0 || free <= cur ? free : Mathf.Lerp(cur, free, 1 - Mathf.Exp(-dt * T("collisionRecoverSpeed", 10)));
        bool bodyVis = true; float fpHideT, bodyHideT;
        // degraus/escada: o CharacterController sobe o degrau num quadro só (tranco de 15–45 cm) e a câmera pulava junto. A câmera segue a
        // inclinação MÉDIA do terreno (subida real ÷ deslocamento, filtrada) e só o EXCESSO de cada degrau vira um deslocamento (camOff) que volta a zero
        // com mola amortecida (velocidade contínua). No ar / pulo de janela / veículo não absorve nada (camOff volta a zero).
        float camOff, camOffV, camSlope; Vector3 camPp; bool camPpOk;
        void StepSmooth(float dt)
        {
            float S = ch.ScaleK; var p = transform.position; float dy = p.y - camPp.y, hz = new Vector2(p.x - camPp.x, p.z - camPp.z).magnitude;
            bool ok = camPpOk && onGround && vault == null && ch.alive && !ch.vehiclePose && Mathf.Abs(dy) < .7f * S && hz < 2.5f * S;
            camPp = p; camPpOk = true;
            if (ok)
            {
                if (hz > 1e-3f) camSlope = Mathf.Lerp(camSlope, Mathf.Clamp(dy / hz, -1.5f, 1.5f), 1 - Mathf.Exp(-dt * 3));
                camOff = Mathf.Clamp(camOff - (dy - camSlope * hz), -.6f * S, .6f * S);
            }
            else if (!onGround) camSlope = Mathf.Lerp(camSlope, 0, 1 - Mathf.Exp(-dt * 6));
            camOff = Mathf.SmoothDamp(camOff, 0, ref camOffV, .1f, Mathf.Infinity, dt);
        }
        // andar armado: o speedWalking do kit (5 m/s) é ritmo de trote para um corpo de 1,9 m — o clipe de andar era acelerado ~1,9× e as
        // pernas "picotavam". 3,1 m/s casa com o passo natural do clipe (FBCharacter.LocoRate perto de 1)
        // andar armado fora de ação = trote do IE na PatrolLocomotion (como antes da rodada 37); em ação, o andar de combate do Complexo
        // (reposicionado pela velocidade natural no FBAnimImport.AdaptMain); 3,4/3,7 m/s = ritmo de jogo (pistola um pouco mais rápida)
        // andar armado (rodada 38, "a caminhada com armas está muito lenta, parecendo câmera lenta"): o IE não tem clipe de andar — o trote
        // IE_Rifle_Run_F (ciclo 0,875 s, passada ~1,5 m) é o andar e a corrida dele. Medido no laboratório: a 3,4 m/s tocava no ritmo natural
        // (1,00) = ~137 passos/min, passos longos e lentos. A 4,2 m/s (pistola 4,4) o mesmo clipe toca a ~1,24× (~170 passos/min) com a
        // passada casando com o deslocamento (sem escorregar); a corrida aprovada (sprint) não muda
        // em AÇÃO (mirou/atirou/recarregou há < 3 s: CombatLocomotion do Complexo) o passo de combate volta a 3,4/3,7 m/s — a 4,2 os clipes de
        // andar de combate batiam no teto do ritmo (1,45) e os pés escorregavam
        float WalkSp => ch && ch.IsPistol ? (ch.Hot ? 3.7f : 4.4f) : (ch && ch.Hot ? 3.4f : 4.2f);
        void SetBodyVisible(bool v)
        {
            // r51e (foto do exe: bloco preto parado na tela na frente da lente da Barret, que a arma de 1ª pessoa não explica): com o corpo escondido
            // a lista só era aplicada na troca — renderizador criado/religado depois (arma, coldre) nascia visível, como na 1ª pessoa (fpHideT).
            // Escondido, reaplica a cada 0,2 s
            if (bodyVis == v && (v || (bodyHideT -= Time.deltaTime) > 0)) return; bodyVis = v; bodyHideT = .2f;
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true)) if (!vm || !r.transform.IsChildOf(vm.transform)) r.shadowCastingMode = v ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }
}
