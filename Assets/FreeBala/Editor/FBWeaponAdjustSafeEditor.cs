#if UNITY_EDITOR
using FreeBala;
using FreeBalaWeaponAdjustSafe;
using UnityEditor;
using UnityEngine;

namespace FreeBalaWeaponAdjustSafeEditor
{
    public sealed class FBWeaponAdjustSafeEditor : EditorWindow
    {
        private const string DatabasePath = "Assets/FreeBala/Resources/FBWeaponAdjustSafeDatabase.asset";
        private AdjustPose chosenPose = AdjustPose.Hold;
        private bool shared = false;

        [MenuItem("Tools/FreeBala/Calibrador de armas (seguro)")]
        private static void Open() => GetWindow<FBWeaponAdjustSafeEditor>("Calibrar Armas");

        private void OnSelectionChange()
        {
            if (TrySelected(out var character, out _, out _, out var place))
                chosenPose = place == AdjustLocation.Holster ? AdjustPose.Hold : FBWeaponAdjustSafeRunner.CurrentPose(character);
            Repaint();
        }

        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Calibrador visual de armas — FreeBala", EditorStyles.boldLabel);
            if (EditorApplication.isPlaying)
            {
                bool enabled = FBWeaponAdjustSafeRunner.CalibrationEnabled;
                bool next = EditorGUILayout.ToggleLeft("Aplicar calibracoes no Play Mode (desmarque para diagnostico)", enabled);
                if (enabled != next) FBWeaponAdjustSafeRunner.CalibrationEnabled = next;
            }
            EditorGUILayout.HelpBox("PLAY > entre na pose desejada > PAUSE > selecione a arma > Usar pose detectada > W = mover, E = girar, R = escala > Salvar. Cada pose tem seus proprios valores.", MessageType.Info);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Entre no Play Mode para o jogo criar os personagens e as armas.", MessageType.Warning);
                if (GUILayout.Button("Criar/abrir banco de calibracoes")) PingDatabase();
                return;
            }

            if (!TrySelected(out var c, out var weaponTransform, out var weaponId, out var location))
            {
                EditorGUILayout.HelpBox("Selecione na Hierarchy um Coldre_<id> ou a arma equipada (ou um filho deles).", MessageType.Warning);
                if (GUILayout.Button("Criar/abrir banco de calibracoes")) PingDatabase();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Personagem", string.IsNullOrEmpty(c.skinId) ? "(sem skinId)" : c.skinId);
            EditorGUILayout.LabelField("Arma", weaponId);
            EditorGUILayout.LabelField("Local", location == AdjustLocation.Holster ? "Coldre (costas, peito ou coxa)" : "Na mao (terceira pessoa)");
            if (location == AdjustLocation.InHand)
            {
                var detectedPose = FBWeaponAdjustSafeRunner.CurrentPose(c);
                EditorGUILayout.LabelField("Pose detectada", detectedPose.ToString());
                if (GUILayout.Button("Usar pose detectada para salvar")) chosenPose = detectedPose;
                chosenPose = (AdjustPose)EditorGUILayout.EnumPopup("Salvar como pose", chosenPose);
                if (FBWeaponAdjustSafeRunner.CurrentPose(c) != chosenPose)
                    EditorGUILayout.HelpBox("A pose selecionada e diferente da detectada. Use o botao acima, ou pause na animacao correspondente.", MessageType.Warning);
            }
            else chosenPose = AdjustPose.Hold;

            shared = EditorGUILayout.ToggleLeft("Usar esta calibracao em todos os personagens (global)", shared);
            if (!FBWeaponAdjustSafeRunner.TryGetBase(weaponTransform, out var basePose))
                EditorGUILayout.HelpBox("Ainda nao ha captura da posicao base. Deixe o Play rodar por alguns frames e pause novamente.", MessageType.Warning);
            else
            {
                EditorGUILayout.LabelField("Escala-base", basePose.scale.ToString("F4"));
                EditorGUILayout.LabelField("Escala atual", weaponTransform.localScale.ToString("F4"));
                EditorGUILayout.HelpBox("Use R para ajustar a escala do objeto RAIZ da arma, nao apenas um mesh filho. Escala uniforme costuma evitar deformacoes.", MessageType.None);
            }
            if (!EditorApplication.isPaused)
                EditorGUILayout.HelpBox("Clique no botao PAUSE da Unity antes de mover a arma. Em Play sem Pause o jogo atualiza o Transform a cada frame.", MessageType.Warning);

            bool valid = EditorApplication.isPaused &&
                (shared || !string.IsNullOrEmpty(c.skinId)) &&
                FBWeaponAdjustSafeRunner.TryGetBase(weaponTransform, out basePose);
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("SALVAR POSICAO, ROTACAO E ESCALA", GUILayout.Height(38)))
                    Save(c, weaponTransform, weaponId, location, basePose, false);
                if (GUILayout.Button("Salvar SOMENTE escala"))
                    Save(c, weaponTransform, weaponId, location, basePose, true);
                if (GUILayout.Button("Remover calibracao desta arma/pose"))
                    Delete(c, weaponId, location);
            }
            if (GUILayout.Button("Abrir banco de calibracoes")) PingDatabase();
            EditorGUILayout.HelpBox("Ajusta o modelo da arma, NAO altera clipes Mixamo, IK dos dedos/bracos nem a arma de primeira pessoa. Teste mira, alinhamento do cano e tiro depois de salvar.", MessageType.None);
        }

        private static bool Under(Transform root, Transform selected) => root && selected && (selected == root || selected.IsChildOf(root));

        private static bool TrySelected(out FBCharacter c, out Transform weaponTransform, out string weaponId, out AdjustLocation location)
        {
            c = null; weaponTransform = null; weaponId = null; location = AdjustLocation.Holster;
            var selected = Selection.activeTransform;
            if (!selected) return false;
            c = selected.GetComponentInParent<FBCharacter>();
            if (!c) return false;

            if (c.weaponGO && c.weapon != null && Under(c.weaponGO.transform, selected))
            {
                weaponTransform = c.weaponGO.transform;
                weaponId = c.weapon.id;
                location = AdjustLocation.InHand;
                return !string.IsNullOrEmpty(weaponId);
            }
            foreach (var holster in c.HolsterObjects)
            {
                if (!holster || !Under(holster.transform, selected)) continue;
                if (!holster.name.StartsWith("Coldre_")) return false;
                weaponTransform = holster.transform;
                weaponId = holster.name.Substring("Coldre_".Length);
                location = AdjustLocation.Holster;
                return !string.IsNullOrEmpty(weaponId);
            }
            return false;
        }

        private static float ScaleRatio(float current, float original)
        {
            if (Mathf.Abs(original) < 0.000001f) return 1f;
            float ratio = current / original;
            return float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0f
                ? 1f : Mathf.Clamp(ratio, 0.01f, 100f);
        }

        private void Save(FBCharacter c, Transform weaponTransform, string weaponId, AdjustLocation location,
            FBWeaponAdjustSafeRunner.BasePose baseline, bool onlyScale)
        {
            var database = GetOrCreate();
            if (!database || !c || !weaponTransform) return;
            string skin = shared ? "*" : c.skinId;
            var deltaPos = weaponTransform.localPosition - baseline.position;
            var deltaRot = Quaternion.Inverse(baseline.rotation) * weaponTransform.localRotation;
            if (onlyScale && database.TryFind(skin, weaponId, location, chosenPose, out var previous))
            {
                // Preserva os offsets existentes deste perfil (ou o perfil herdado).
                deltaPos = previous.localPositionOffset;
                deltaRot = previous.localRotationOffset;
            }
            var mul = new Vector3(
                ScaleRatio(weaponTransform.localScale.x, baseline.scale.x),
                ScaleRatio(weaponTransform.localScale.y, baseline.scale.y),
                ScaleRatio(weaponTransform.localScale.z, baseline.scale.z));
            Undo.RecordObject(database, "Calibrar arma");
            database.Set(skin, weaponId, location, chosenPose, deltaPos, deltaRot, mul);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("[Calibrador] Salvo: " + weaponId + " / " + location + " / " + chosenPose + " / " + skin + " / Scale=" + mul);
            EditorUtility.DisplayDialog("Calibracao salva", "Posicao, rotacao e escala armazenadas no FBWeaponAdjustSafeDatabase.asset. Cada pose mantem seu proprio perfil.", "OK");
        }

        private void Delete(FBCharacter c, string weaponId, AdjustLocation location)
        {
            var database = GetOrCreate();
            if (!database) return;
            string skin = shared ? "*" : c.skinId;
            Undo.RecordObject(database, "Remover calibracao de arma");
            if (database.Remove(skin, weaponId, location, chosenPose))
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
        }

        private static FBWeaponAdjustSafeDatabase GetOrCreate()
        {
            var database = AssetDatabase.LoadAssetAtPath<FBWeaponAdjustSafeDatabase>(DatabasePath);
            if (database) return database;
            if (!AssetDatabase.IsValidFolder("Assets/FreeBala")) AssetDatabase.CreateFolder("Assets", "FreeBala");
            if (!AssetDatabase.IsValidFolder("Assets/FreeBala/Resources")) AssetDatabase.CreateFolder("Assets/FreeBala", "Resources");
            database = CreateInstance<FBWeaponAdjustSafeDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static void PingDatabase()
        {
            var database = GetOrCreate();
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }
    }
}
#endif
