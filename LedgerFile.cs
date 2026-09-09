// 開発用。台帳を 1 つのテキストに落とし、読み直したあとに同じ内容で組み直せるようにする。
//
// ScriptEngine の F6 は静的な値を全部作り直すので、読み直すと台帳が空になる。
// 結果画面は空なら触らない作りなので、そのままでは「結果画面を出したまま見た目を詰める」が
// できない。そこで結果画面を組んだ時点の中身をファイルに残し、読み直したあとの F7 で読ませる。
//
// 配布版には要らない（[9. Dev] Enabled が切なら書きも読みもしない）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;

namespace LwfTaxLedger
{
    internal static class LedgerFile
    {
        private const string FileName = "LwfTaxLedger-last.txt";
        private const char Sep = '\t';

        private static string Path()
        {
            try { return System.IO.Path.Combine(Paths.BepInExRootPath, FileName); }
            catch (Exception) { return FileName; }
        }

        /// <summary>いまの台帳を残す。中身が無いときは何もしない。</summary>
        internal static void Save()
        {
            if (Ledger.Entries.Count == 0 || Ledger.IsDummy) { return; }
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                for (int i = 0; i < Ledger.Entries.Count; i++)
                {
                    TaxEntry e = Ledger.Entries[i];
                    sb.Append("entry").Append(Sep).Append(e.Slot).Append(Sep).Append(e.EffectID)
                        .Append(Sep).Append(e.Points).Append(Sep).Append(e.CancelledSales)
                        .Append(Sep).Append(e.PatronID).Append(Sep).Append(e.ExtraHits)
                        .Append(Sep).Append(e.ActiveSeconds.ToString("0.###", CultureInfo.InvariantCulture))
                        .Append(Sep).Append(e.RateText == null ? "" : e.RateText).Append('\n');
                    foreach (KeyValuePair<string, int> kv in e.LostResources)
                    {
                        sb.Append("res").Append(Sep).Append(kv.Key).Append(Sep).Append(kv.Value).Append('\n');
                    }
                    foreach (KeyValuePair<string, int> kv in e.LostTags)
                    {
                        sb.Append("tag").Append(Sep).Append(kv.Key).Append(Sep).Append(kv.Value).Append('\n');
                    }
                    foreach (KeyValuePair<string, ItemLoss> kv in e.Items)
                    {
                        ItemLoss loss = kv.Value;
                        sb.Append("item").Append(Sep).Append(loss.ItemID).Append(Sep).Append(loss.Count).Append('\n');
                        foreach (KeyValuePair<string, int> r in loss.Resources)
                        {
                            sb.Append("itemres").Append(Sep).Append(r.Key).Append(Sep).Append(r.Value).Append('\n');
                        }
                    }
                }
                File.WriteAllText(Path(), sb.ToString(), new UTF8Encoding(false));
                TaxLedgerPlugin.Trace("[dev] saved the ledger to " + FileName);
            }
            catch (Exception e)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] cannot save the ledger: " + e.Message);
            }
        }

        /// <summary>残してあった台帳を読み込む。いまの中身は捨てる。</summary>
        internal static bool Load()
        {
            string path = Path();
            try
            {
                if (!File.Exists(path)) { return false; }
                string[] lines = File.ReadAllLines(path);
                Ledger.Reset();
                TaxEntry entry = null;
                ItemLoss item = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split(Sep);
                    if (f.Length < 2) { continue; }
                    switch (f[0])
                    {
                        case "entry":
                            if (f.Length < 9) { break; }
                            entry = Ledger.Get(f[2], Int(f[1]));
                            entry.Points = Int(f[3]);
                            entry.CancelledSales = Int(f[4]);
                            entry.PatronID = Int(f[5]);
                            entry.ExtraHits = Int(f[6]);
                            entry.ActiveSeconds = Float(f[7]);
                            entry.RateText = f[8].Length > 0 ? f[8] : null;
                            item = null;
                            break;
                        case "res":
                            if (entry != null && f.Length >= 3) { entry.AddLostResource(f[1], Int(f[2])); }
                            break;
                        case "tag":
                            if (entry != null && f.Length >= 3) { entry.AddLostTag(f[1], Int(f[2])); }
                            break;
                        case "item":
                            if (entry == null || f.Length < 3) { break; }
                            item = entry.GetItem(f[1]);
                            item.Count += Int(f[2]);
                            break;
                        case "itemres":
                            if (item != null && f.Length >= 3) { item.AddTag(f[1], Int(f[2])); }
                            break;
                    }
                }
                TaxLedgerPlugin.Log.LogInfo("[dev] loaded the last ledger: entries=" + Ledger.Entries.Count);
                return Ledger.Entries.Count > 0;
            }
            catch (Exception e)
            {
                TaxLedgerPlugin.Log.LogWarning("[dev] cannot load the ledger: " + e.Message);
                return false;
            }
        }

        private static int Int(string s)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static float Float(string s)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }
    }
}
