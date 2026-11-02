**BURITIRO 10x10 (Orla): main-thread CPU plan**

The plan is built only from the verified findings and uses the skeptic's corrected savings and fixes. I checked the line numbers for the top items in the code (FBDecalFx.cs 546-552, FBCharacter.cs 3223 and 3507, FBBot.cs 1905-1910) and they match. All savings were estimated at about 45 FPS, on Ultra, in live rounds.

**How to measure (this corrects the task text)**

- **`medir orla` is not the right test for most items.** It freezes the match (`g.freeze = true`) with the player at each base, so bot AI costs read close to 0. Use it only for render counters: shadow casters, draw calls, visible skinned meshes, and its A/B test per map group (it includes the birds).
- **For per-part and per-script ms, use `BURITIRO.exe -fbtesteexe 10x10 orla 3`.** For each round and phase it prints:
  - `desempenho:` with CPU principal, the frame parts ("scripts no Update", "scripts no LateUpdate", "Animator", "malhas com osso", "render na thread principal") and "lixo de memória por quadro (bytes)";
  - `scripts nessa fase:`, the FBTempo top 14 in ms per frame;
  - a `desempenho.csv` file.
- **Compare like with like.** Compare the live phases of rounds 2-3 before and after a change. Run each A/B twice, because the number of fights and deaths changes between runs.
- **Use `piloto` for player-side items.** Add `piloto` (`10x10 orla 3 piloto`) for items that only show when the player moves and shoots: casings, HUD while aiming, weapon mods. The default player is parked and unarmed.
- **Set the quality to Ultra before the run.** The harness uses the saved quality level. Earlier runs were at "PC Alto", and rig-2 saves nothing below Ultra.
- **Small items will not show one by one.** The FBTempo top-14 list stops at about 0.011 ms. Ship small items as one batch and compare the total "scripts nessa fase" and the GC bytes per frame.

---

## (1) Safe quick wins: no behaviour change. Do these now.

They are ranked by saving and effort.

**1. fx-decal-fade: 0.11 ms**
- **Where:** FBDecalFx.cs 546-552 (Projector branch of Tick) and Place() 395/400.
- **Change:**
  - Move `slot.proj.fadeFactor = slot.fade;` inside the `if (num2 > slot.life)` branch, after the `Free` check, as `if (Projector) slot.proj.fadeFactor = slot.fade;`. `fade` only changes there; Place sets it to 1.
  - Add `bool dry` to Slot and set `dry = false` in Place next to line 395.
  - Change line 549 to `if (slot.blood && !slot.dry && num2 > 30f) { slot.proj.material = projBloodDry; slot.dry = true; }`.
- **Measure:** FBFxTicker.Update in the buy and live phases of round 2 and later. Today it is 0.085-0.165 ms; it should drop to about 0.01-0.03 ms.

**2. corpse-1 (dead characters skip the Animator-parameter Update): 0.06 ms**
- **Where:** FBCharacter.cs 3223.
- **Change:**
  - After the deltaTime check, add `if (!an.enabled && !alive) { hitCd = Mathf.Max(0, hitCd - dt); lastShot += dt; combatHold -= dt; TickSpecOnly(dt); return; }`.
  - Move the existing `if (specT >= 0f) {...}` block into TickSpecOnly and call it from both paths.
  - Guard on `!alive`, because alive knockdown ragdolls also disable the Animator.
  - Reset drawT, drawHid, emoteT, waitPose and danW in Revive. Death by explosion (FBDeath 135) can disable the Animator before any `!alive` Update runs.
- **Measure:** FBCharacter.Update ms in late live phases. Today it is about 10 us per call.

**3. anim-1 (hashed Animator setters and a shared HandsBusy value): 0.06 ms**
- **Where:** FBCharacter.cs 879-897 and 934-986; FBRig AimSpine, Hands and DesvirarTronco.
- **Change:**
  - In Awake, build the parameter hashes from `P(n)`, the real parameter name, with -1 when the parameter is missing. SF, SB and ST then call `an.SetX(int, …)`.
  - Add static hashes for the state names. Replace each IsName with a compare against both `shortNameHash` and `fullPathHash`.
  - Store the `handsBusy` already computed at LateUpdate__fbt 3511 in a field, and have the rig read that field. Do not cache it on `Time.frameCount`, because FBAnimBur 238 reads it earlier and the value can change mid-frame.
  - Cache the PUP values in statics.
- **Measure:** FBCharacter.Update and FBCharacter.LateUpdate ms.

**4. rig-3 (ClearBody reads each body matrix once): 0.10 ms alone, 0.04 ms after rig-1**
- **Where:** FBRig.cs 1124-1135 (ClearBody0) and FBCharacter.cs 2398-2427; BodyFastBegin is at 2459.
- **Change:**
  - Add `FrontPenetrationFast(p, pad)`. It loops over `fast[]`, skips entries whose kind is not 0 (the head is kind 2), and uses `w2l.MultiplyPoint3x4` and the cached scale.
  - Call `ch.BodyFastBegin()` once before the 17-sample loop.
  - Keep ClearMs, but compute it with `Stopwatch.GetTimestamp()`.
  - The result differs only in the last float bits.
- **Measure:** FBCharacter.LateUpdate ms.

**5. char-holster (skip Solve for unseen characters, plus allocation fixes): about 0.09 ms**
- **Where:** FBCharacter.cs 2694-2847 (flag4 at 2729, Solve at 2807 and 2825, the List at 2721, Stopwatch at 2730), 1557-1566 and MakeHolster 1625.
- **Change:**
  - Add a `Seen()` helper that checks body SMRs, the held weapon renderers (wbRend and the largest weapon parts) and the holster renderers. rig-1 reuses this helper.
  - Run Solve only when Seen() is true. Keep every-frame Solve for seen bots within about 12 m and keep the existing rule beyond that.
  - Always keep the SetActive logic running.
  - Reuse one member `List<Holstered>`.
  - Use `Stopwatch.GetTimestamp()` instead of `Stopwatch.StartNew()`.
  - Set `isColdre = true` (and keep the id) in MakeHolster, and test that instead of `go.name.StartsWith`.
  - Add a static `manualDbTried` flag so Resources.Load runs only once.
- **Measure:** FBCharacter.LateUpdate ms and GC bytes per frame.

**6. world-10 (weather tick chain): 0.05 ms**
- **Where:** FBWeather.cs 426-447 and 667-695; FBCarLights.cs Tick; FBVolumetric.cs 183-193; FBWindowLights.cs 429-549; FBAccentLights.cs 214-235.
- **Change:**
  - LampGlow skips only when k is exactly the same as last time. Invalidate that cache when lampMats is rebuilt (389-404). Lower-case the material names once, at build.
  - FBCarLights: remove dead keys from `rigs` at the top of Tick. Return early when no live car exists and the beam, tail and rev pools were already off last frame. Use static Lists and List.Sort instead of OrderBy().ToList().
  - Cache VolumetricAdditionalLight per Light in a Dictionary.
  - WindowLights: switch to the day state once, when day starts.
  - AccentLights: skip the loop when `num` has not changed.
- **Measure:** FBWeatherTicker.Update, today 0.12-0.16 ms.

**7. gc-1 (rig allocations): 0.04 ms, plus about 6-9 KB per frame less garbage**
- **Where:** FBRig.cs 1063, 1176, 1589, 1612, 1661, 1693 and 1755.
- **Change:**
  - Replace the Stopwatches with `Stopwatch.GetTimestamp()`. Keep TorsoMs and ClearMs, because the harness reads them.
  - Replace the Func chain in RifleLeftHand with a `GripShape` struct and an `Inside(in GripShape, p)` method. Keep the order: undo the lift, then subtract shift + shift2 as one translation, then run the ellipse or cylinder test.
- **Measure:** "lixo de memória por quadro" and FBCharacter.LateUpdate ms.

**8. bots-1 (bot vision casts): 0.10 ms. More work than the others.**
- **Where:** FBBot.cs 1990-2019 and FBBotSense.cs 68-101.
- **Change:**
  - Compute Eye(self) and SightMax once per think, not once per target.
  - Then pick one of two approaches:
    - **(a) Batch:** collect the candidate casts into a persistent 64-entry NativeArray and run `SpherecastCommand.ScheduleBatch(cmds.GetSubArray(0,k), hits.GetSubArray(0,k), 4, 1).Complete()`. Use QueryParameters(SolidMask, triggers ignored) and treat `colliderInstanceID == 0` as visible. Dispose the arrays in OnDestroy.
    - **(b) Early exit:** stop at the first clear point when aware >= 1.4, cast the chest first, and finish all casts for the chosen target so seenMask stays exact.
  - Do not ship the "slower think for far bots" part.
- **Measure:** FBBot.Update ms in live phases.

**9. bots-5, parts 1-2: about 0.035 ms of the 0.06 ms total**
- **Where:** FBBot.cs 2217 and FBWorld.cs 100-114.
- **Change:**
  - At 2217, test the cheap condition first: `(ch.Pos - dest).magnitude > 12f || !FBWorld.ClearLine(...)`.
  - Rewrite ClearLine as `Clear1(a,b,0.6f) && Clear1(a,b,1.5f)`, with no array.
- **Measure:** FBBot.Update ms and GC bytes.

**10. world-4 (hummingbirds): 0.03 ms**
- **Where:** FBLivingSky.cs 326 and 342; cloud loop 409-453.
- **Change:**
  - Set `updateWhenOffscreen = false` and keep the mesh's own localBounds, grown by 20% for the wing flap.
  - Set the Animator to `cullingMode = CullCompletely`.
  - Skip the cloud loop when the clouds root is inactive.
  - Do not deactivate birds at 80 m: the pop would be visible.
- **Measure:** "medir" A/B on the birds group, plus the "Animator" and "malhas com osso" parts.

**11. world-7 (pigeons): 0.03 ms**
- **Where:** FBBirds.cs 725-728 and Pose 894-927.
- **Change:**
  - Wrap the Pose loop in `if (!f.oculto)`.
  - Add a per-bird `wingsFolded` flag. Set it the first time a grounded Pose runs and clear it in the flight branch. While it is set, skip the constant wing writes.
- **Measure:** FBBirds.Update, today 0.079-0.092 ms.

**12. hud-update-alloc, without the input change: about 0.025 ms**
- **Where:** FBHUD.cs 1460, 1475, 1669-1711, 1716-1835 and 1889; SetMed 1022-1025.
- **Change:**
  - Delete the unused Spread call at 1460.
  - Cache the 4 crosshair Images, the cone Image and the CanvasGroups.
  - Squad: fill a reused List with a for-loop over FBMatch.All (`a && !a.civ && Ally(a)`), insert the player, and run an insertion sort on Slot(a).n every frame. Cache the Ally delegate.
  - Assign `.text` only when the int behind it changes.
  - Tags: read preferredWidth only when the label changes.
  - SetMed: cache the last kits value.
- **Measure:** FBHUD.Update, today 0.12-0.165 ms.

**13. bots-6 (10x10 Hud cache): 0.02 ms**
- **Where:** FBModo10x10.cs 1343-1353 and 1252.
- **Change:**
  - Return early when this cache key is unchanged: FBHUD.I (by reference), round, the two wins, phase, `Max(0, CeilToInt(buy ? pt : rt))`, alive0, alive1 and results.Count.
  - Count alive players with a for-loop using the same rule as Alive(): not null, `!civ`, alive and team == t.
  - Build the pips array only when results change.

**14. casings: 0.02 ms (casing shadows practically invisible)**
- **Where:** FBCasings.cs 38 and 72-85.
- **Change:**
  - Set `mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off`. This follows the game's own small-caster rule: these casings are created under DontDestroyOnLoad, so PruneCasters never reaches them.
  - Add a `rest` flag, set when the casing touches the ground and its speed is under about 0.05. While resting, keep the age check and the Rotate, but skip gravity, the GroundY ray and the position write.
  - Replace `live.RemoveAll(lambda)` with a reverse loop.
- **Measure:** use the `piloto` run.

**15. bots-9 (fb_data cache): about 0.02 ms**
- **Where:** FBBot.cs 299-321, 2152-2162, 2231-2260 and 2770.
- **Change:**
  - Lazily fill static fields for shootingFlagDuration, aiCrouch*, speedAiming, LimiteMira and PassoReal. Keep the PassoTeste and MiraTeste overrides.
  - Cache `ch.Pos` only inside the aim block at 2268-2276. A cache that lasts until Move() goes stale after DestinoSeguro or a relocation.

**16. bots-7 + shot-raycast-alloc (one item: both edit FBCombat.cs 65-66): about 0.012 ms**
- **Change:**
  - Use a static `RaycastHit[128]` buffer with RaycastNonAlloc and `Array.Sort(buf, 0, n, cachedComparer)`. Fall back to RaycastAll when `n == 128`.
  - Use `n` in both loops, including `k < n` at 84.
  - Move `GetComponentInParent<FBTarget>` (106) inside `if (shooter && shooter.isPlayer)`.
  - Add a lower-case impact name table indexed by Surf, and a static fallback array. FBUtil.Pick must still draw the same index.
  - FBPlayer.AimPoint 1158-1186 gets its own buffer: sort it and break at the first accepted hit.

**17. rig-1 side fix (FBRemote lookup): about 0.01 ms**
- **Where:** FBRig AimErr, TorsoErr and HeadLook.
- **Change:** add a `remoteChecked` bool so `GetComponent<FBRemote>()` runs once, not 2-4 times per bot per frame.

**18. Small garbage batch: about 0.05 ms in total. Ship as one change.**
- **bots-11:**
  - Turn the FBBotSense 12-18 tables into static readonly arrays.
  - Give AimTarget its own static Vector3[3] scratch array. It must not be FBBotSense.pts.
  - Make the strafe, radius and angle arrays static.
  - Update the INTEL object in place.
  - Replace Actors(), TeamOf() and Alive() with for-loops in the per-frame and per-think paths.
- **bots-8:** add `FBNav.Path(from, to, List into)` using GetCornersNonAlloc on `cantos`, with a reused per-bot list. Keep `path = null` as the "replan" signal.
- **world-11:**
  - FBAudio.Ear (101) re-finds the listener only when `!ear || !ear.isActiveAndEnabled`.
  - FBFootsteps.Listener (262) uses FBAudio.Ear.
- **world-12:**
  - Use a cached string[] of the SHOTS keys (FBAmbience 217).
  - Use the non-allocating `CalculateFrustumPlanes(cam, planes)`.
  - FBTransito.Update: replace the OrderBy and Count lambdas with loops and a reused List.Sort.
- **weapon-mods-cache:**
  - Cache the Mods, Scope and Zoom results per id, only when `c == null`, and clear an id's entry in Set().
  - Never cache or share the Choice object: FBArmaLab and FBMenu change it in place.
  - Make the Regex objects static. Make the FBScopes sniper dictionary static readonly.
- **hudweapons-strings:** set the text only when its int key changes (FBHudWeapons 273-334).
- **per-shot-scans + bots-3 (a)(c):**
  - Set `ch.bot = this` in **FBBot.Awake**, not FBCharacter.Awake. FBBot is added later, so FBCharacter.Awake would store null and bots would ignore gunfire.
  - Use it in Alert, OnHit, FBCombat and FBTactics, keeping the FBMatch.Bots order.
  - Add an optional shooter parameter to FBFx.Muzzle, with the scan as a fallback.
  - Rewrite the OnHit LINQ as a for-loop that keeps the 2 nearest, using strict `<`.
- **laser-orderby:** do two passes in place of OrderBy and store the FBBot in E at Bind.
- **loot-ring:** write ring.localScale only within about 30 m of the **camera**.

**Group 1 total: about 0.85 ms standalone.** If rig-1 also ships, rig-3 and the holster Solve overlap with it, so subtract about 0.14 ms.

---

## (2) Worth doing with care: low risk

**1. rig-1 (skip the procedural rig on characters nobody can see): 0.5 ms. The biggest item. Ruan must agree to it first.**
- **Where:** FBCharacter.cs 3502-3565 (LateUpdate__fbt) and FBRig.cs 465-481, 680-737 and 906-976.
- **Change:**
  - After the `an.enabled` guard and the proxy reset, add `if (!isPlayer && !Seen()) { if (feet) feet.Tick(dt); PeekShift = 0f; return; }`.
  - Use the Seen() from group 1 item 5, which includes the weapon and holster renderers.
  - Keep the PlaceHolsters SetActive work running. Weights() is optional.
- **Risk:** nothing changes on screen. Off-screen bots, though, stop having their spine, arms, hitboxes, muzzle and sight points pushed further every frame on frozen bones (spine pitch about 20° per frame today). That changes the results of off-screen bot-vs-bot fights, most likely for the better (it removes an existing bug), but it is a gameplay outcome change.
- **Measure:** FBCharacter.LateUpdate and "scripts no LateUpdate". The live phases of rounds 2-3 should fall from about 1.1-1.4 ms.

**2. rig-2 (detailed fingers by projected size): 0.3 ms alone, 0.15 ms after rig-1. Ultra only.**
- **Where:** FBRig.cs 487-497, 972-975, 1036-1039, 1782-1847.
- **Change:**
  - Fade the Wrap and Curl weight over a band of effective distance (distance / FBScopes.Zoom). Do not switch it off at a fixed 12 m.
  - Cache the Wrap solve every 4th frame per bot (add `th` to Finger, reset it in OnWeapon and when ikW < 0.02). Bend must still run every frame.
  - Cache the FingerAxis bones in Init.
- **Risk:** distant fingers show the animation clip's hand pose; up close, fingers can lag the weapon grip by millimetres.
- **Measure:** run at Ultra and compare "scripts no LateUpdate".

**3. bots-2 (dead bots skip the move): 0.1 ms**
- **Where:** FBBot.cs 1905-1910.
- **Change:**
  - Add a `deadIdle` counter. Skip Move when the bot is on the ground, `vel.y == 0`, deadIdle >= 3 and `(Time.frameCount + deadPhase) % 30 != 0`.
  - Set deadPhase from a static counter, not GetInstanceID or Random.
  - Keep `vel.x = vel.z = 0` and set `ch.grounded = onGround`.
  - Reset deadIdle in ResetRound (438) and OnRevived (468).
- **Risk:** a body whose ground disappears settles up to 30 frames later.
- **Measure:** FBBot.Update in late live phases.

**4. bots-3 (b) (throttle repeat alerts per bot): about 0.045 ms during fights**
- **Where:** FBBotBrain.cs 54-71 and FBBot.cs 516-527.
- **Change:**
  - Add `alertT` and `alertP` per bot. Re-roll `goalP = FlankPoint` and the lastSeen jitter only when 0.5 s have passed or the sound moved more than 5 m.
  - Keep goalT, `pauseT = 0` and TeamIntel updating on every shot.
- **Risk:** the flank point stops jittering on every shot.

**5. bots-4 (cover search): 0.04 ms, plus fewer 0.5 ms spikes**
- **Where:** FBBot.cs 537-574 and 2732.
- **Change:**
  - Run DentroLimite on x/z before the GroundY ray, and skip the cast when `num4 <= num`. Neither change alters the result.
  - Use a static `{4f,6f,8f}` radius array.
  - Remember a failed search for 0.75 s, unless the bot moved 1.5 m or more or the threat moved 2 m or more. Apply this only at 2732.
- **Risk:** cover can be noticed up to 0.75 s later.

**6. weapon-shadow (small weapon and holster parts on bots): 0.04 ms, more on the render thread**
- **Change:**
  - Add a helper that turns off shadows on child MeshRenderers with bounds.extents.magnitude < 0.06, for bots beyond about 15 m only. Never apply it to the player or the spectated character.
  - Re-check every 0.25-0.5 s, re-run after FBWeaponModel.Apply, and keep an undo list.
- **Measure:** "objetos fazendo sombra" in `medir`.

**7. footik-1: 0.03 ms**
- **Where:** FBQuality.cs 344-345 and FBFootIK.cs 481-503.
- **Change:**
  - Set `footIKInterval = V(3,2,2,2)` for bots.
  - Raise the re-cast threshold to 0.15 × s at both LOD levels; otherwise the interval has no effect.
- **Risk:** feet react to a step or kerb 1-2 frames later.

**8. bots-5, parts 3-4: about 0.025 ms**
- **Where:** FBBot.cs 669 and 2277.
- **Change:**
  - Retry the corner cut only when path[1] changed or after 0.1 s.
  - Re-run the jump probe when the bot moved 0.14 m or more, or turned more than 10°, since the last probe. Do not use the fixed 0.05 s timer: it starts jumps up to 0.3 m late.

**9. hud duplicate FBInput.Read: 0.01 ms**
- **Where:** FBHUD.cs 1586 and FBPlayer.cs 481.
- **Change:** FBPlayer stores `LastInput` and `LastInputFrame`. The HUD uses `LastInputFrame >= frameCount - 1 && LastInput.scoreboard`.
- **Risk:** a visible change. Toggle-aim (AimToggle) starts working; today the second Read flips it back on the same frame. Tell players or testers.

**10. bots-10 (FBNav.Aquecer warm-up): 0.01 ms average, removes 1-3 ms spikes in early rounds**
- **Change:**
  - Warm the NavMesh cells near this round's routes and bases during "buy", within a budget of about 1 ms per frame.
  - Keep a resumable index and check the Stopwatch after each Inside() call.
- **Risk:** a few cells that sit across a fence may be judged differently.

**Group 2 total: about 0.95 ms** (with rig-2 counted after rig-1).

---

## (3) Not worth it, or rejected

**Rejected sub-parts of accepted items:**
- bots-1 (3), slower think for far bots: it changes AI timing (`seeT += 0.2`).
- bots-5 (4) as a fixed 0.05 s timer; use the distance version above.
- bots-9: caching myPos until Move(), because it goes stale.
- char-holster: every-3rd-frame Solve for nearby visible bots, because the clipping is visible.
- world-4: deactivating birds at 80 m (visible pop).
- world-7: removing pigeon shadows, or updating pigeons every 3rd frame.
- loot-ring: removing dropped-weapon shadows (visible).
- casings: a rest state that also stops the spin (visible). Removing the duplicate FBShellSim (a visual change).
- shot-raycast: a 32-entry buffer (can drop the nearest hit) and skipping triggers without an FBHitbox (not proven identical).
- per-shot-scans: caching in FBCharacter.Awake, which breaks gunfire alerts.
- weapon-mods: a shared cached Choice.
- HUD: reading Tab/Select directly.
- rig-2: a fixed 12 m on/off switch.
- footik: skipping the toe ray during the swing (toes may clip stairs).

**Rejected findings:**
- bots-12: rare log spikes.
- rig-4: the fix lowers visible quality.
- corpse-skin: not a driver of EarlyUpdate.
- fp-pip: outside this scenario, and it changes what the player sees.
- muzzle-lights: GPU-side, a few microseconds.
- player-misc: no real saving.
- world-2: about 30-60 props, about 0.01 ms.
- world-3: about 18 bodies, 0.005 ms.
- world-5: not shown by the code or logs.
- world-6: below 0.01 ms.
- world-8: refuted by measurement.
- world-9: 0.011 ms in total.

**Ruan's decision, not a code optimization: world-1, street events in 10x10.** Events add 4-7 FBCharacter NPCs plus cars after about round 2. That costs about 0.6 ms on average, up to 1.2 ms while events run. It is the largest single item outside the rig. Turning events off in 10x10 removes visible content, so it needs his yes. rig-1 already makes their off-screen rig cheaper.

---

## Total and the 120 FPS question

- **Expected saving:** about 0.85 ms (group 1) + 0.95 ms (group 2) − 0.14 ms (overlap) ≈ **1.6-1.7 ms of main thread** in live rounds. Most findings are medium confidence, so the honest range is 1.0-2.2 ms.
- **Garbage:** about 10-15 KB per frame less, which matters for the 18-30 FPS dips if GC is part of them.
- **Where it lands:** "scripts no LateUpdate" grew from 0.71 ms (31/10) to 1.85 ms. The rig package (rig-1, rig-2, rig-3, holster, gc-1) should give back about 0.8-1.0 ms of that.
- **Frame-rate effect:** main thread goes from about 18.8 ms to about 17.2 ms, or **about 50 FPS to about 55-58 FPS**. The per-think and per-shot items are costs per second, so at higher FPS their per-frame share shrinks. The per-frame items (rig, decals, HUD, weather, animator parameters) keep their value.
- **120 FPS (8.3 ms) cannot be reached with CPU script work alone.** Even with every script at 0 ms (about 5.3 ms removed), the main thread is still about 13.5 ms, roughly 74 FPS.
  - The main thread's render submission is 6.19 ms by itself, 75% of the budget.
  - Skinning 1.2 ms + Animator 1.21 ms + EarlyUpdate 2.1 ms add more.
  - The render thread (about 10 ms) and the GPU (9-12 ms) are also each over 8.3 ms.
- **What 120 FPS needs (render side, not checked in code; measure with `medir` A/B and the existing `perf` mode):**
  - The GPU Resident Drawer with GPU occlusion culling (needs URP Forward+, the SRP Batcher, and BatchRendererGroup variants kept) to cut 2700-3500 draws and 250-290 SetPass calls.
  - Fewer shadow cascades and a shorter shadow distance. `perf` already tests 80 m with 3 cascades and small-caster pruning; about 900 casters × 4 cascades today.
  - Graphics Jobs and GPU (compute) skinning in Player Settings.
  - Profile EarlyUpdate (2.1 ms live against 0.8-1.07 ms in buy) with the Unity Profiler. The harness only gives the total, and no code finding explains it.