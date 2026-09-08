// 開発用。次のシフトに掛かる課税を指定する。
//
// 15 種から 3 種の抽選なので、狙った課税を引くまで待てない。本体は 3 つの枠を
// TaxSlotService.GetOrCreateSlots() から読む（工場の予告・課税通知書・結果画面も同じ）ので、
// ここを差し替えれば、期の順も表示も台帳も本番と同じ流れのまま狙った課税になる。
//
// 選ぶのはゲーム中の一覧（Alt+F7）から。選んだ結果は cfg の [9. Dev] Taxes に残る。
// 空なら本体の抽選のまま。
//
// 検証を早めるために、期の時計を待たずにその場で掛ける口も持つ（一覧の F 列と「全部」）。
// 各課税の Apply は静的なサービスを触るだけで、効き先はほとんど別々なので、
// 15 種を同時に掛けても互いに潰し合わない。期（slotIndex）も引数なので、
// 第 2 期・第 3 期の値を待たずに試せる。
//
// 配布版には入れない。

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Tax;
using UnityEngine;
using Utility.Localization;

namespace LwfTaxLedger
{
    internal static class DevTax
    {
        private static TaxSlotData[] _slots;
        private static string _parsedFrom;

        // 本体は期の一覧をシフト開始時に控える。変えたら知らせないと反映されない
        private static FieldInfo _slotsChangedField;

        /// <summary>いま選んでいる 3 つ。空文字は未選択</summary>
        private static readonly string[] Picked = new string[3] { "", "", "" };

        /// <summary>期の時計を待たずに掛けたもの。EffectID -> 実体（外すのに要る）</summary>
        private static readonly Dictionary<string, ITaxEffect> Forced = new Dictionary<string, ITaxEffect>();

        /// <summary>まとめ掛けするときの期</summary>
        private static int _forceSlot;

        /// <summary>製作数を減らす 3 種は、減った分がどれのせいか分けられない。
        /// まとめ掛けでは 1 つだけにする</summary>
        private static readonly string[] ResultCutIDs = new string[3]
        { "SampleProvision", "ReduceItemCraftResult", "ReduceTagCraftResult" };

        internal static void Init()
        {
            // 既定では開発用一式を切る。課税の指定も一覧も出さない
            if (!TaxLedgerPlugin.DevMode.Value) { return; }
            _slotsChangedField = AccessTools.Field(typeof(TaxSlotService), "SlotsChangedSubject");
            if (_slotsChangedField == null)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] cannot notify the slot change; restart the shift to apply");
            }
            Parse(TaxLedgerPlugin.DevTaxes.Value);
        }

        /// <summary>本体に「期の一覧が変わった」と伝える。シフト中でもその場で切り替わる。</summary>
        private static void Notify()
        {
            if (_slotsChangedField == null) { return; }
            try
            {
                object subject = _slotsChangedField.GetValue(null);
                if (subject == null) { return; }
                MethodInfo onNext = AccessTools.Method(subject.GetType(), "OnNext");
                if (onNext == null) { return; }
                onNext.Invoke(subject, new object[] { default(R3.Unit) });
            }
            catch (Exception e)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] cannot notify the slot change: " + e.Message);
            }
        }

        /// <summary>cfg の指定を読む。書式が違えば本体の抽選に任せる。</summary>
        private static void Parse(string value)
        {
            _parsedFrom = value;
            _slots = null;
            if (string.IsNullOrEmpty(value)) { return; }

            string[] parts = value.Split(',');
            List<TaxSlotData> slots = new List<TaxSlotData>();
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (id.Length == 0) { continue; }
                if (!TaxEffectRegistry.IsKnownEffectID(id))
                {
                    TaxLedgerPlugin.Log.LogWarning("[dev] unknown tax id: " + id);
                    return;
                }
                slots.Add(new TaxSlotData(id));
            }
            if (slots.Count != 3)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] Taxes needs 3 ids, got " + slots.Count);
                return;
            }
            _slots = slots.ToArray();
            for (int i = 0; i < 3; i++) { Picked[i] = _slots[i].EffectID; }
            TaxLedgerPlugin.Log.LogInfo("[dev] taxes fixed to " + value);
        }

        // ---------------------------------------------------------------
        // その場で掛ける（検証用）
        // ---------------------------------------------------------------

        /// <summary>1 種を、指定の期の値で掛ける。すでに掛かっていれば掛け直す。</summary>
        internal static void Force(string effectID, int slot)
        {
            Unforce(effectID);
            TaxEffectContext context = TaxHooks.Context();
            if (context == null)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] no context yet; start a shift first");
                return;
            }
            ITaxEffect effect = TaxEffectRegistry.Create(effectID);
            if (effect == null) { return; }
            try
            {
                effect.Apply(context, slot);
                Forced[effectID] = effect;
            }
            catch (Exception e)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] cannot force " + effectID + ": " + e.Message);
            }
        }

        /// <summary>1 種を外す。本体の期で掛かっている分には触らない。</summary>
        internal static void Unforce(string effectID)
        {
            ITaxEffect effect;
            if (!Forced.TryGetValue(effectID, out effect)) { return; }
            Forced.Remove(effectID);
            try { effect.Remove(TaxHooks.Context()); }
            catch (Exception) { }
        }

        internal static bool IsForced(string effectID)
        {
            return Forced.ContainsKey(effectID);
        }

        /// <summary>製作数を減らす 3 種のうち、いま掛けている 1 つ。無ければ空。</summary>
        private static string ForcedResultCut()
        {
            for (int i = 0; i < ResultCutIDs.Length; i++)
            {
                if (Forced.ContainsKey(ResultCutIDs[i])) { return ResultCutIDs[i]; }
            }
            return "";
        }

        /// <summary>まとめ掛け。製作数を減らす 3 種は、いま選んでいる 1 つだけ残す。</summary>
        internal static void ForceAll(int slot)
        {
            string keep = ForcedResultCut();
            if (keep.Length == 0) { keep = ResultCutIDs[0]; }
            IReadOnlyList<string> ids = TaxEffectRegistry.EffectIDs;
            int n = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (Array.IndexOf(ResultCutIDs, id) >= 0 && id != keep) { continue; }
                Force(id, slot);
                n++;
            }
            TaxLedgerPlugin.Log.LogInfo("[dev] forced " + n + " taxes at slot" + slot
                + " (result cut: " + keep + ")");
        }

        /// <summary>その場で掛けた分を全部外す。シフトの切り替わりでも呼ばれる。</summary>
        internal static void ForceNone()
        {
            if (Forced.Count == 0) { return; }
            List<string> ids = new List<string>(Forced.Keys);
            for (int i = 0; i < ids.Count; i++) { Unforce(ids[i]); }
            TaxLedgerPlugin.Log.LogInfo("[dev] forced taxes cleared");
        }

        [HarmonyPatch(typeof(TaxSlotService), "GetOrCreateSlots")]
        private static class SlotsPatch
        {
            /// <summary>開発用を切っているときは、そもそも割り込みを当てない。</summary>
            private static bool Prepare()
            {
                return TaxLedgerPlugin.DevMode.Value;
            }

            private static void Postfix(ref TaxSlotData[] __result)
            {
                // cfg はゲーム中に書き換えられる。値が変わっていたら読み直す
                string current = TaxLedgerPlugin.DevTaxes.Value;
                if (current != _parsedFrom) { Parse(current); }
                if (_slots == null) { return; }
                __result = _slots;
            }
        }


        // ---------------------------------------------------------------
        // ゲーム中の一覧（開発用）
        // ---------------------------------------------------------------

        private static bool _open;
        private static Rect _window = new Rect(40f, 40f, 420f, 520f);
        private static Vector2 _scroll;
        private static GUIStyle _rowStyle;

        internal static bool IsOpen { get { return _open; } }

        internal static void Toggle()
        {
            _open = !_open;
        }

        internal static void Draw()
        {
            if (!_open) { return; }
            _window = GUI.Window(0x7A7E, _window, DrawWindow, "Tax Ledger — dev");
        }

        private static void DrawWindow(int id)
        {
            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(GUI.skin.label);
                _rowStyle.alignment = TextAnchor.MiddleLeft;
                _rowStyle.wordWrap = false;
            }

            GUILayout.BeginHorizontal();
            for (int slot = 0; slot < 3; slot++)
            {
                GUILayout.Label((slot + 1) + ": " + (Picked[slot].Length == 0 ? "-" : Name(Picked[slot])),
                    _rowStyle, GUILayout.Width(126f));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("force at", _rowStyle, GUILayout.Width(52f));
            for (int slot = 0; slot < 3; slot++)
            {
                if (GUILayout.Toggle(_forceSlot == slot, (slot + 1).ToString(),
                    GUI.skin.button, GUILayout.Width(30f)))
                {
                    _forceSlot = slot;
                }
            }
            if (GUILayout.Button("All")) { ForceAll(_forceSlot); }
            if (GUILayout.Button("Clear")) { ForceNone(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply")) { Commit(); }
            if (GUILayout.Button("Random"))
            {
                for (int i = 0; i < 3; i++) { Picked[i] = ""; }
                Commit();
            }
            if (GUILayout.Button("Close")) { _open = false; }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<string> ids = TaxEffectRegistry.EffectIDs;
            for (int i = 0; i < ids.Count; i++)
            {
                string effectID = ids[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(Name(effectID), _rowStyle, GUILayout.Width(240f));
                for (int slot = 0; slot < 3; slot++)
                {
                    bool on = Picked[slot] == effectID;
                    bool next = GUILayout.Toggle(on, (slot + 1).ToString(), GUI.skin.button, GUILayout.Width(30f));
                    if (next == on) { continue; }
                    if (next) { Pick(effectID, slot); }
                    else { Picked[slot] = ""; }
                }
                // F: 期の時計を待たずにその場で掛ける
                bool forced = Forced.ContainsKey(effectID);
                bool nextForced = GUILayout.Toggle(forced, "F", GUI.skin.button, GUILayout.Width(30f));
                if (nextForced != forced)
                {
                    if (nextForced) { ForceSingle(effectID); }
                    else { Unforce(effectID); }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        /// <summary>製作数を減らす 3 種は、掛けるときに前のものを外して 1 つに保つ。</summary>
        private static void ForceSingle(string effectID)
        {
            if (Array.IndexOf(ResultCutIDs, effectID) >= 0)
            {
                for (int i = 0; i < ResultCutIDs.Length; i++)
                {
                    if (ResultCutIDs[i] != effectID) { Unforce(ResultCutIDs[i]); }
                }
            }
            Force(effectID, _forceSlot);
        }

        /// <summary>同じ課税は 1 枠だけ。別の枠に入っていたら空ける。</summary>
        private static void Pick(string effectID, int slot)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Picked[i] == effectID) { Picked[i] = ""; }
            }
            Picked[slot] = effectID;
        }

        private static void Commit()
        {
            int filled = 0;
            for (int i = 0; i < 3; i++) { if (Picked[i].Length > 0) { filled++; } }
            string value = (filled == 3) ? (Picked[0] + "," + Picked[1] + "," + Picked[2]) : "";
            TaxLedgerPlugin.DevTaxes.Value = value;
            Parse(value);
            Notify();
            if (value.Length == 0) { TaxLedgerPlugin.Log.LogInfo("[dev] taxes back to random"); }
        }

        private static string Name(string effectID)
        {
            try
            {
                string s = LocalizedTextGetter.GetTaxData(effectID);
                if (string.IsNullOrEmpty(s)) { return effectID; }
                System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
                bool inTag = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '<') { inTag = true; continue; }
                    if (c == '>') { inTag = false; continue; }
                    if (!inTag) { sb.Append(c); }
                }
                return sb.ToString();
            }
            catch (Exception) { return effectID; }
        }
    }
}
