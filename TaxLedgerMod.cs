// LWF Tax Ledger — 課税で失ったものの累計を結果画面の「生産目標」枠に出す
//
// いまは骨組み。台帳はダミーデータで埋め、開発用の鍵で枠の複製を出して見た目を決める段階。
// 課税の実データを数える Harmony の割り込みはこのあと足す。

using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using GameState;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LwfTaxLedger
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TaxLedgerPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "kiyonakanata.lwftaxledger";
        internal const string PluginName = "LWF Tax Ledger";
        internal const string PluginVersion = "1.0.2";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> FontSize;
        internal static ConfigEntry<int> Padding;
        internal static ConfigEntry<int> IconSize;
        internal static ConfigEntry<int> RowSpacing;
        internal static ConfigEntry<int> ColumnSpacing;
        internal static ConfigEntry<int> Indent;
        internal static ConfigEntry<int> MarkerWidth;
        internal static ConfigEntry<bool> ExpandBreakdown;
        internal static ConfigEntry<int> ValueWidth;
        internal static ConfigEntry<int> ResourceWidth;
        internal static ConfigEntry<int> ScrollStep;
        internal static ConfigEntry<int> NameShrink;
        internal static ConfigEntry<int> BackgroundDarken;
        internal static ConfigEntry<int> ScrollbarWidth;
        internal static ConfigEntry<int> ResourceColumns;
        internal static ConfigEntry<int> NumberGap;
        internal static ConfigEntry<int> SubStep;
        internal static ConfigEntry<bool> DevMode;
        internal static ConfigEntry<string> PreviewKey;
        internal static ConfigEntry<string> DumpKey;
        internal static ConfigEntry<string> DevTaxes;

        private Key _previewKeyCode;
        private Key _dumpKeyCode;
        private int _dummySet;
        private float _nextResultPoll;

        // 本体は 0.29.1 から、起動中に例外が出るとゲームごと起動を止める（エラー画面）。
        // 本体の更新でパッチ先が消えても MOD だけ止まって起動は通るよう、全部を受け止める。
        // 一部の課税だけ拾う台帳は数字が狂うので、失敗したら当てた分も全部外す
        private void Awake()
        {
            Log = Logger;
            try
            {
                Boot();
            }
            catch (Exception e)
            {
                Log.LogWarning("[boot] " + PluginName + " " + PluginVersion
                    + " disabled (the game version may have changed): " + e);
                try { if (_harmony != null) { _harmony.UnpatchSelf(); } }
                catch (Exception e2) { Log.LogWarning("[boot] unpatch failed: " + e2.Message); }
                enabled = false;
            }
        }

        private void Boot()
        {
            Enabled = Config.Bind("1. General", "Enabled", true, "");

            FontSize = Config.Bind("2. Layout", "Font size", 24, "");
            Padding = Config.Bind("2. Layout", "Padding", 12, "");
            IconSize = Config.Bind("2. Layout", "Icon size", 28, "");
            RowSpacing = Config.Bind("2. Layout", "Row spacing", 2, "");
            ColumnSpacing = Config.Bind("2. Layout", "Column spacing", 6, "");
            Indent = Config.Bind("2. Layout", "Item indent", 20, "");
            MarkerWidth = Config.Bind("2. Layout", "Marker width", 20, "");
            ExpandBreakdown = Config.Bind("1. General", "Expand breakdown", false, "");
            ValueWidth = Config.Bind("2. Layout", "Value width", 88, "");
            ResourceWidth = Config.Bind("2. Layout", "Resource width", 76, "");
            ScrollStep = Config.Bind("2. Layout", "Scroll step", 30, "");
            NameShrink = Config.Bind("2. Layout", "Name shrink", 8, "");
            BackgroundDarken = Config.Bind("2. Layout", "Background brightness (%)", 25, "");
            ScrollbarWidth = Config.Bind("2. Layout", "Scrollbar width", 20, "");
            ResourceColumns = Config.Bind("2. Layout", "Resource columns", 2, "");
            NumberGap = Config.Bind("2. Layout", "Resource number gap", 8, "");
            SubStep = Config.Bind("2. Layout", "Breakdown size step", 4, "");

            // 開発用の一式（鍵・課税の指定・細かいログ）。既定は切。
            // 遊ぶだけなら要らないが、消さずに残しておく
            DevMode = Config.Bind("9. Dev", "Enabled", false, "");
            // F7 で枠を出す。Shift を添えるとダミーの組が進む
            // （F8 は economy_graph、F9・F10 は fps_boost が使っている）
            PreviewKey = Config.Bind("9. Dev", "Preview key", "F7", "");
            DumpKey = Config.Bind("9. Dev", "Dump key", "F5", "");
            // 開発用: 次のシフトの課税。ゲーム中は Alt+F7 の一覧で選ぶ
            DevTaxes = Config.Bind("9. Dev", "Taxes", "",
                "LongerCraft, SampleProvision, SlowConveyor, HarvestRockPenalty, HarvestTreePenalty, "
                + "ReduceTagCraftResult, ReduceItemCraftResult, CutLocalTag, CutBuffCash, Centralization, "
                + "FamCountPenalty, LandCountPenalty, LockRandomPatron, NoTagRandomPatron, OrderExecutePenalty");
            _previewKeyCode = ParseKey(PreviewKey.Value, Key.F7);
            _dumpKeyCode = ParseKey(DumpKey.Value, Key.F5);

            _harmony = new Harmony(PluginGuid);
            TaxHooks.Init(_harmony);
            DevTax.Init();
            _harmony.PatchAll(typeof(TaxLedgerPlugin).Assembly);
            int patches = 0;
            foreach (System.Reflection.MethodBase m in _harmony.GetPatchedMethods()) { patches++; }
            Log.LogInfo("[boot] " + PluginName + " " + PluginVersion + " patches=" + patches);
        }

        private void OnDestroy()
        {
            if (_harmony != null) { _harmony.UnpatchSelf(); }
        }

        private void Update()
        {
            if (!Enabled.Value) { return; }

            if (Time.unscaledTime >= _nextResultPoll)
            {
                _nextResultPoll = Time.unscaledTime + 0.5f;
                LedgerView.TrackResult();
            }

            if (!DevMode.Value) { return; }

            // ゲームは新しい Input System だけを使う設定。旧来の UnityEngine.Input は例外になる
            Keyboard kb = Keyboard.current;
            if (kb == null) { return; }

            if (kb[_dumpKeyCode].wasPressedThisFrame
                && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                // シフトを今すぐ終わらせて結果画面へ。本体の開発用の口をそのまま呼ぶ。
                // 記録には残るが countsForStatistics = false が付き、統計には数えられない
                EndRunNow();
                return;
            }
            if (kb[_dumpKeyCode].wasPressedThisFrame)
            {
                Ledger.Dump();
                TaxHooks.LogCheck();
                // 読み直しをまたいで同じ中身を見られるように残す
                LedgerFile.Save();
            }
            if (kb[_previewKeyCode].wasPressedThisFrame)
            {
                bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
                if (alt) { DevTax.Toggle(); return; }
                // F7 は実データだけ。空なら空のまま出す（取れているかの判定を邪魔しない）。
                // ダミーは Shift+F7 のときだけ
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (shift)
                {
                    Ledger.DummyPayTag = LedgerView.PayTag();
                    Ledger.FillDummy(_dummySet);
                    Log.LogInfo("[dev] dummy set " + (_dummySet % Ledger.DummySetCount + 1) + "/" + Ledger.DummySetCount);
                    _dummySet++;
                }
                else
                {
                    Ledger.DropDummy();
                    // 読み直しで台帳が消えているときは、前回の結果画面の中身を読む。
                    // 実データのまま見た目を詰められる
                    if (Ledger.Entries.Count == 0) { LedgerFile.Load(); }
                    Log.LogInfo("[dev] preview: entries=" + Ledger.Entries.Count);
                }
                // 結果画面が出ていれば本物の枠へ。出ていなければ複製を出す
                if (LedgerView.IsResultWindowUp()) { LedgerView.Reattach(); }
                else { LedgerView.ShowOrRefreshPreview(); }
            }
            if (LedgerView.IsPreviewShown && kb[Key.Escape].wasPressedThisFrame)
            {
                LedgerView.TogglePreview();
            }
        }

        private void OnGUI()
        {
            if (!DevMode.Value) { return; }
            DevTax.Draw();
        }

        /// <summary>いま遊んでいるシフトを打ち切って結果画面を出す（開発用）。</summary>
        private static void EndRunNow()
        {
            GameStateManager manager = GameStateManager.Instance;
            if (manager == null)
            {
                Log.LogWarning("[dev] not in a shift");
                return;
            }
            bool ok = manager.DebugLose();
            Log.LogInfo(ok ? "[dev] ending the shift now" : "[dev] the shift cannot be ended yet");
        }

        /// <summary>開発中だけ見たい細かいログ。既定では出さない。</summary>
        internal static void Trace(string line)
        {
            if (DevMode != null && DevMode.Value) { Log.LogInfo(line); }
        }

        private static Key ParseKey(string name, Key fallback)
        {
            try { return (Key)Enum.Parse(typeof(Key), name, true); }
            catch (Exception) { return fallback; }
        }
    }
}
