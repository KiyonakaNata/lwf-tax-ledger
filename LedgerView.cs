// 結果画面の「生産目標」枠（AchievementBody）に台帳を描く。
//
// 本番: 結果画面が上がったら、枠の中の "Under Developent" 文字を隠し、
//       枠の Scroll View に行を積む。
// 開発: 同じ枠を複製して自前の Canvas に出す（結果画面まで行かずに見るため）。
//       行の組み方は両方とも BuildRows() で同じ。
//
// アイテムと資源はゲームのアイコン（Sprite）で出す。名前を並べると横に伸びて読めない。

using System;
using System.Collections.Generic;
using BaseSystem;
using GameState;
using GameState.StaticStateHolders;
using Items;
using Items.Tag;
using TMPro;
using UI.SelectableWindow.Managers;
using UnityEngine;
using UnityEngine.UI;
using Utility.LWFApp;
using Utility.Localization;
using Tax;

namespace LwfTaxLedger
{
    internal static class LedgerView
    {
        private const string BodyName = "AchievementBody";
        private const string TitleName = "AchievementTitle";
        private const string PlaceholderName = "TMPTemporary";
        private const string FrameName = "FrameAchievement";
        private const string RowsName = "TaxLedgerRows";
        private const string PreviewName = "TaxLedgerPreview";

        private const string CashTag = "Cash";

        /// <summary>そのシフトで返済に使う資源。特別任務では現金とは限らない
        /// （培養プラントは薬品）。枠を組むたびに引き直す</summary>
        private static string _payTag = CashTag;
        private const string FixedCostTag = "RepaymentLoseCash";
        private const string HitItem = "Pickaxe";
        private const string ConveyorItem = "Summon-Conveyor";
        private const string GehennaSmile = "portrait_patron_gehenna_smile";

        /// <summary>内訳に並べる資源の順。行ごとに順が変わると読めなくなる</summary>
        private static readonly string[] ResourceOrder = new string[]
        {
            "Construction", "Grocery", "Luxury", "Fertilizer", "Chemical", "Magic", "Fuel"
        };

        /// <summary>枠の地は暗い赤。白は眩しく、暗い茶は沈むので、ゲームの金茶で濃淡をつける</summary>
        private static readonly Color NameColor = LWFColors.HEADER_COLOR;
        private static readonly Color SectionColor = Fade(LWFColors.HEADER_COLOR, 0.72f);
        private static readonly Color ValueColor = Fade(LWFColors.RED_COLOR, 0.85f);
        private static readonly Color SubColor = Fade(LWFColors.HEADER_COLOR, 0.7f);
        private static readonly Color SubValueColor = Fade(LWFColors.HEADER_COLOR, 0.92f);

        private static Color Fade(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }

        private static GameObject _preview;
        private static bool _attached;
        private static TextMeshProUGUI _template;

        // 結果画面は場面ごとに 1 つ。毎秒の全走査は重いので、見つけたら控えておく
        private static ResultUIManager _result;

        // 本体は未実装の欄を薄く塗っている。こちらの行まで沈むので押し戻す
        private static CanvasGroup[] _dimmed;

        // 薄塗りを外すと地の赤がそのまま出て、隣の欄より明るくなる。
        // ゲームの色は触らず（触ると読み直しのたびに効きが重なる）、自前の暗幕を 1 枚敷く
        private const string BackdropName = "TaxLedgerBackdrop";

        // ---------------------------------------------------------------
        // 本番: 結果画面の枠へ
        // ---------------------------------------------------------------

        public static bool IsResultWindowUp()
        {
            ResultUIManager result = Result();
            return result != null && result.gameObject.activeInHierarchy;
        }

        /// <summary>結果画面の管理役。控えが切れている間だけ探す。</summary>
        private static ResultUIManager Result()
        {
            if (_result != null) { return _result; }
            try
            {
                ResultUIManager[] found = UnityEngine.Object.FindObjectsByType<ResultUIManager>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                _result = found.Length > 0 ? found[0] : null;
            }
            catch (Exception) { _result = null; }
            return _result;
        }

        /// <summary>結果画面の枠を組み直す（開発用。ダミーを本物の枠に載せて確かめる）。</summary>
        public static void Reattach()
        {
            _attached = false;
            _dimmed = null;
            TrackResult();
        }

        /// <summary>結果画面が出ている間に毎秒呼ぶ。最初の 1 回だけ枠を組む。</summary>
        public static void TrackResult()
        {
            if (!IsResultWindowUp()) { _attached = false; _dimmed = null; return; }
            if (_attached) { Undim(); return; }
            // 課税があるのはインフェルノだけ。本体と同じ判定を使い、
            // 難易度やモードが違うシフトでは枠に一切触らない
            // （開発用を入れているあいだは、確かめられるよう素通しにする）
            if (!IsTaxRun() && !TaxLedgerPlugin.DevMode.Value) { _attached = true; return; }
            Transform body = FindResultBody();
            if (body == null) { return; }
            _attached = true;
            // 課税が 1 つも無いシフト（Hell3 以下）や、読み直しで台帳が空のときは本体のまま
            if (Ledger.Entries.Count == 0)
            {
                TaxLedgerPlugin.Trace("[view] nothing to show; left the frame untouched");
                return;
            }
            try
            {
                Build(body, false);
                _dimmed = body.GetComponentsInChildren<CanvasGroup>(true);
                Undim();
                TaxLedgerPlugin.Trace("[view] attached to the result frame ("
                    + _dimmed.Length + " canvas groups)");
                // 読み直したあとにも同じ画面を組み直せるように残す（開発用）
                if (TaxLedgerPlugin.DevMode.Value) { LedgerFile.Save(); }
            }
            catch (Exception e)
            {
                TaxLedgerPlugin.Log.LogWarning("[view] cannot build the result frame: " + e.Message);
            }
        }

        /// <summary>このシフトに課税があるか。本体の判定（インフェルノかつ対象のモード）に合わせる。</summary>
        private static bool IsTaxRun()
        {
            try { return TaxUnlockPolicy.CanApplyRuntimeEffects(CurrentGameMode.Get); }
            catch (Exception) { return false; }
        }

        /// <summary>地を隣の欄と同じ暗さに落とす。ゲームの色は変えず、黒い幕を 1 枚挟む。
        /// 枠と送り枠の両方に入れる。送り枠は自前の地を持っていて、枠側の幕を隠すため。</summary>
        private static void DarkenFrame(Transform frame)
        {
            if (frame == null) { return; }

            Transform old = frame.Find(BackdropName);
            while (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                old = frame.Find(BackdropName);
            }

            float alpha = 1f - Mathf.Clamp01(TaxLedgerPlugin.BackgroundDarken.Value / 100f);
            if (alpha <= 0f) { return; }

            // 地が既に暗ければ幕は要らない（読み直しで色が潰れている場合など）
            Image ground = frame.GetComponent<Image>();
            if (ground != null && ground.color.r + ground.color.g + ground.color.b < 0.2f) { return; }

            GameObject go = new GameObject(BackdropName, typeof(RectTransform));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(frame, false);
            r.localScale = Vector3.one;
            Stretch(r);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;
            // 地のすぐ上、送りの部品や行より下に置く
            r.SetSiblingIndex(0);
        }

        /// <summary>薄塗りを外す。開く演出で戻されることがあるので、出ている間は押し戻し続ける。</summary>
        private static void Undim()
        {
            if (_dimmed == null) { return; }
            for (int i = 0; i < _dimmed.Length; i++)
            {
                CanvasGroup g = _dimmed[i];
                if (g == null) { continue; }
                if (g.alpha < 1f) { g.alpha = 1f; }
                // 本体は未実装の欄なのでクリックを止めている。内訳を開けるように通す
                if (!g.interactable) { g.interactable = true; }
                if (!g.blocksRaycasts) { g.blocksRaycasts = true; }
            }
        }

        private static Transform FindResultBody()
        {
            ResultUIManager result = Result();
            if (result == null) { return null; }
            return FindChildRecursive(result.transform, BodyName);
        }

        // ---------------------------------------------------------------
        // 開発: 枠の複製を自前の Canvas へ
        // ---------------------------------------------------------------

        public static bool IsPreviewShown { get { return _preview != null; } }

        public static void TogglePreview()
        {
            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview);
                _preview = null;
                return;
            }
            ShowPreview();
        }

        public static void ShowOrRefreshPreview()
        {
            if (_preview != null)
            {
                UnityEngine.Object.DestroyImmediate(_preview);
                _preview = null;
            }
            ShowPreview();
        }

        private static void ShowPreview()
        {
            Transform body = FindResultBody();
            if (body == null)
            {
                TaxLedgerPlugin.Log.LogWarning("[preview] " + BodyName + " not found (ResultUIManager missing in this scene)");
                return;
            }

            GameObject stale = GameObject.Find(PreviewName);
            if (stale != null) { UnityEngine.Object.DestroyImmediate(stale); }

            _preview = new GameObject(PreviewName);
            Canvas canvas = _preview.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CopyScaler(body, _preview.AddComponent<CanvasScaler>());
            _preview.AddComponent<GraphicRaycaster>();

            GameObject dim = new GameObject("Dim");
            dim.transform.SetParent(_preview.transform, false);
            Image dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(dim.GetComponent<RectTransform>());

            Transform title = FindChildRecursive(body.parent, TitleName);
            RectTransform bodyRect = body as RectTransform;
            Vector2 bodySize = bodyRect != null ? bodyRect.rect.size : new Vector2(600f, 420f);
            float titleHeight = 0f;
            if (title != null)
            {
                RectTransform t = Center(CloneInto(title, _preview.transform, "Title"), (title as RectTransform).rect.size);
                titleHeight = t.rect.height;
                t.anchoredPosition = new Vector2(0f, bodySize.y * 0.5f + titleHeight * 0.5f);
                SetTitleText(t);
            }
            RectTransform clone = Center(CloneInto(body, _preview.transform, "Body"), bodySize);
            clone.anchoredPosition = new Vector2(0f, -titleHeight * 0.5f);

            // 結果画面は開く演出で薄い状態から始まるので、複製は最初から見える状態に
            CanvasGroup[] groups = clone.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].alpha = 1f;
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }

            Build(clone, true);
            TaxLedgerPlugin.Trace("[preview] shown (" + bodySize.x + "x" + bodySize.y + ")");
        }

        private static void CopyScaler(Transform body, CanvasScaler mine)
        {
            CanvasScaler src = body.GetComponentInParent<CanvasScaler>();
            if (src == null)
            {
                mine.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                mine.referenceResolution = new Vector2(1920f, 1080f);
                mine.matchWidthOrHeight = 1f;
                return;
            }
            mine.uiScaleMode = src.uiScaleMode;
            mine.referenceResolution = src.referenceResolution;
            mine.screenMatchMode = src.screenMatchMode;
            mine.matchWidthOrHeight = src.matchWidthOrHeight;
            mine.referencePixelsPerUnit = src.referencePixelsPerUnit;
        }

        private static GameObject CloneInto(Transform source, Transform parent, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
            clone.name = name;
            clone.SetActive(true);
            DisableLocalizers(clone);
            return clone;
        }

        private static RectTransform Center(GameObject go, Vector2 size)
        {
            RectTransform r = go.GetComponent<RectTransform>();
            if (r == null) { r = go.AddComponent<RectTransform>(); }
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
            r.localScale = Vector3.one;
            return r;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------
        // 枠の組み立て
        // ---------------------------------------------------------------

        private static void Build(Transform body, bool isPreview)
        {
            Transform placeholder = FindChildRecursive(body, PlaceholderName);
            _template = placeholder != null ? placeholder.GetComponent<TextMeshProUGUI>() : null;
            if (_template == null)
            {
                _template = body.GetComponentInChildren<TextMeshProUGUI>(true);
                TaxLedgerPlugin.Log.LogWarning("[view] " + PlaceholderName + " not found; using "
                    + (_template != null ? _template.name : "nothing") + " as the text template");
            }
            if (_template == null) { throw new InvalidOperationException("no TextMeshProUGUI under " + BodyName); }

            _payTag = CurrentPayTag();
            TaxLedgerPlugin.Trace("[view] pay tag = " + _payTag);
            Transform frame = FindChildRecursive(body, FrameName);
            ScrollRect scroll = (frame != null ? frame : body).GetComponentInChildren<ScrollRect>(true);

            if (!isPreview)
            {
                Transform title = FindChildRecursive(body.parent, TitleName);
                if (title != null) { SetTitleText(title as RectTransform); }
            }
            if (frame != null) { frame.gameObject.SetActive(true); }
            if (scroll != null) { scroll.gameObject.SetActive(true); }

            // 枠に元から入っている見本の文字・絵を消す。枠そのもの（背景と巻物）は残す
            HideOthers(body, frame);

            // 中身の入れ物は本体のものを使わず、自前で用意して送りの対象にする。
            // 本体の入れ物は並べ方の指定を持っていて、開く演出や作り直しで巻き戻される
            RectTransform holder = scroll != null ? scroll.viewport : null;
            if (holder == null) { holder = body as RectTransform; }

            Transform stale = holder.Find(RowsName);
            while (stale != null)
            {
                UnityEngine.Object.DestroyImmediate(stale.gameObject);
                stale = holder.Find(RowsName);
            }

            GameObject rowsGo = new GameObject(RowsName, typeof(RectTransform));
            RectTransform rows = rowsGo.GetComponent<RectTransform>();
            rows.SetParent(holder, false);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0f, 1f);
            rows.anchoredPosition = Vector2.zero;
            rows.offsetMin = new Vector2(0f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(0f, rows.offsetMax.y);
            rows.localScale = Vector3.one;

            VerticalLayoutGroup column = rowsGo.AddComponent<VerticalLayoutGroup>();
            int pad = TaxLedgerPlugin.Padding.Value;
            // スクロールバーの幅は初めから空けておく。あとから出ても中身がずれない
            column.padding = new RectOffset(pad, pad + BarWidth(scroll), pad, pad);
            column.spacing = TaxLedgerPlugin.RowSpacing.Value;
            column.childForceExpandHeight = false;
            column.childForceExpandWidth = true;
            column.childControlHeight = true;
            column.childControlWidth = true;
            column.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = rowsGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            BuildRows(rows);
            // 幕は行より下、地より上に。枠の直下に置くと、送り枠が持つ赤い地に隠れる。
            // 複製（開発用の下見）にも同じように入れる。入れないと暗さを比べられない
            DarkenFrame(frame);
            DarkenFrame(holder);

            if (scroll != null)
            {
                // 本体の入れ物は使わない。空にして、こちらの入れ物を送りの対象にする
                RectTransform old = scroll.content;
                if (old != null && old != rows) { old.gameObject.SetActive(false); }
                scroll.content = rows;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.inertia = false;
                scroll.elasticity = 0f;
                scroll.scrollSensitivity = TaxLedgerPlugin.ScrollStep.Value;
                // バーの幅は上の余白で先に確保してあるので、出入りしても行は横に動かない。
                // 収まっているときまで出しておくと、ただの飾りになって邪魔になる
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalNormalizedPosition = 1f;
            }

            TaxLedgerPlugin.Trace("[view] template=" + _template.name + " size=" + _template.fontSize
                + " holder=" + holder.name + " (" + holder.rect.width.ToString("0") + "x"
                + holder.rect.height.ToString("0") + ") bar=" + BarWidth(scroll)
                + " entries=" + Ledger.Entries.Count);
        }

        /// <summary>親の子を、残すものひとつを除いて隠す（こちらが作った行は触らない）。</summary>
        private static void HideOthers(Transform parent, Transform keep)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == keep) { continue; }
                if (child.name == RowsName) { continue; }
                if (!child.gameObject.activeSelf) { continue; }
                child.gameObject.SetActive(false);
                TaxLedgerPlugin.Trace("[view] hid " + parent.name + "/" + child.name);
            }
        }

        private static void BuildRows(RectTransform rows)
        {
            List<TaxEntry> entries = Ledger.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                TaxEntry e = entries[i];

                if (i > 0) { AddSeparator(rows); }

                RectTransform head = NewRow(rows, 0, TaxLedgerPlugin.IconSize.Value);
                TextMeshProUGUI marker = AddMarker(head);
                AddFixedText(head, SectionName(e.Slot), TaxLedgerPlugin.FontSize.Value, SectionColor);
                // 課税名は余りを全部もらう。長い名前（トリプルチェックの義務化）でも
                // 値に被らないよう、入りきらないときは字を詰める
                AddName(head, TaxName(e.EffectID), TaxLedgerPlugin.FontSize.Value);
                if (e.PatronID > 0)
                {
                    AddIcon(head, PatronSprite(e.PatronID), null,
                        TaxLedgerPlugin.IconSize.Value, TaxLedgerPlugin.FontSize.Value);
                }
                AddHeadValue(head, e);

                // 内訳。合計より一段小さく暗くして、上の行にぶら下がって見えるようにする
                int subFont = SubFontSize();
                int subIcon = SubIconSize();
                List<GameObject> breakdown = new List<GameObject>();
                foreach (KeyValuePair<string, ItemLoss> kv in e.Items)
                {
                    ItemLoss loss = kv.Value;
                    RectTransform row = NewRow(rows, TaxLedgerPlugin.Indent.Value, subIcon);
                    AddIcon(row, ItemSprite(loss.ItemID), loss.ItemID, subIcon, subFont);
                    AddFixedText(row, "x" + Format(loss.Count), subFont, SubValueColor);
                    AddFlex(row);
                    AddOtherResources(row, loss.Resources, subFont, subIcon);
                    AddValue(row, TagSprite(_payTag), _payTag, "-" + Format(Pay(loss.Resources)),
                        subFont, subIcon, SubValueColor);
                    breakdown.Add(row.gameObject);
                }

                // 返済にならないバフ・レリックの減り。現金の欄は「-」にして、
                // 払った額と読み違えないようにする
                foreach (KeyValuePair<string, int> kv in e.LostTags)
                {
                    RectTransform row = NewRow(rows, TaxLedgerPlugin.Indent.Value, subIcon);
                    AddIcon(row, TagSprite(kv.Key), kv.Key, subIcon, subFont);
                    AddFixedText(row, "x" + Format(kv.Value), subFont, SubValueColor);
                    AddFlex(row);
                    AddOtherResources(row, NoResources, subFont, subIcon);
                    AddValue(row, TagSprite(_payTag), _payTag, "-", subFont, subIcon, SubColor);
                    breakdown.Add(row.gameObject);
                }

                // 徴発のように資源が多い課税は、合計行に並べきれない（3 つで右端いっぱい）。
                // 資源ごとに 1 行ずつ内訳へ落とし、合計行には返済に使う資源の分だけ残す
                if (ResourcesInBreakdown(e))
                {
                    // アイコンと数字は右端にまとめて置く（離すと読めない）。赤字＝失った額。
                    // 桁は一番大きい額に合わせて揃える（K と素の数が混じると読みにくい）
                    int maxAbs = 0;
                    foreach (KeyValuePair<string, int> kv in e.LostResources) { if (kv.Value > maxAbs) { maxAbs = kv.Value; } }
                    foreach (string tag in LostResourceOrder(e))
                    {
                        RectTransform row = NewRow(rows, TaxLedgerPlugin.Indent.Value, subIcon);
                        AddFlex(row);
                        AddValue(row, TagSprite(tag), tag, "-" + FormatLike(e.LostResources[tag], maxAbs), subFont, subIcon, ValueColor);
                        breakdown.Add(row.gameObject);
                    }
                }
                MakeExpandable(head, marker, breakdown);
            }
        }

        /// <summary>行の左端の目印。内訳を持つ課税だけ + と - が出る。</summary>
        private static TextMeshProUGUI AddMarker(RectTransform row)
        {
            GameObject go = NewText(row, " ", TaxLedgerPlugin.FontSize.Value, SectionColor, false);
            go.name = "Marker";
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Midline;
            LayoutElement le = go.AddComponent<LayoutElement>();
            int width = TaxLedgerPlugin.MarkerWidth.Value;
            le.preferredWidth = width;
            le.minWidth = width;
            return t;
        }

        /// <summary>見出しの行を押すと内訳が開く。明細が縦に伸びると読めなくなるため、既定は閉じる。</summary>
        private static void MakeExpandable(RectTransform head, TextMeshProUGUI marker, List<GameObject> rows)
        {
            if (rows.Count == 0) { return; }

            bool open = TaxLedgerPlugin.ExpandBreakdown.Value;
            for (int i = 0; i < rows.Count; i++) { rows[i].SetActive(open); }
            marker.text = open ? "-" : "+";

            Image hit = head.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            Button button = head.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            List<GameObject> captured = rows;
            TextMeshProUGUI capturedMarker = marker;
            button.onClick.AddListener(delegate
            {
                bool next = captured.Count > 0 && !captured[0].activeSelf;
                for (int i = 0; i < captured.Count; i++) { captured[i].SetActive(next); }
                capturedMarker.text = next ? "-" : "+";
            });
        }

        /// <summary>課税の行の右側。課税の種類で出るものが違う。</summary>
        private static void AddHeadValue(RectTransform row, TaxEntry e)
        {
            int font = TaxLedgerPlugin.FontSize.Value;
            int icon = TaxLedgerPlugin.IconSize.Value;
            int before = row.childCount;

            // 点数は率。返済のたびにこの数だけ資源が減る。払った額は別に積んである
            if (e.Points != 0)
            {
                AddValue(row, TagSprite(FixedCostTag), FixedCostTag, "+" + Format(e.Points),
                    font, icon, SubValueColor);
            }
            if (ResourcesInBreakdown(e))
            {
                // 資源ごとの額は内訳行にある。返済に使う資源が減っていればそれを、
                // 減っていなければ（徴発・源泉徴収は現金を削らない）全資源の印で合計を出す
                int pay;
                e.LostResources.TryGetValue(_payTag, out pay);
                // 減っていなければ（徴発・源泉徴収は現金を削らない）合計行には何も出さない。
                // 資源をまとめた額を出すと「全資源がその分減った」ように読めてしまう
                if (pay != 0)
                {
                    AddValue(row, TagSprite(_payTag), _payTag, "-" + Format(pay), font, icon, ValueColor);
                }
            }
            else
            {
                foreach (KeyValuePair<string, int> kv in e.LostResources)
                {
                    AddValue(row, TagSprite(kv.Key), kv.Key, "-" + Format(kv.Value), font, icon, ValueColor);
                }
            }
            if (e.CancelledSales != 0)
            {
                // ゲヘナ以外での売却が消える課税。相手の顔で「何回ゲヘナに流れたか」を示す
                // もう結構（0.30）は相手が居ないので顔は出さない
                Sprite face = e.EffectID == "Centralization" ? SpriteByName(GehennaSmile) : null;
                AddValue(row, face, null, "x" + Format(e.CancelledSales), font, icon, ValueColor);
            }
            if (e.ExtraHits != 0)
            {
                AddShrinkingLabel(row, Message("RequireMiningHit"), font);
                AddValue(row, ItemSprite(HitItem), null, "+" + Format(e.ExtraHits), font, icon, ValueColor);
            }
            if (!string.IsNullOrEmpty(e.RateText))
            {
                // 失われた個数を測れないものは、課税が下げた率をそのまま出す。
                // 節水令は完成品置き場が無制限で使い魔が止まらないため、
                // 逮捕は遅くなる先が製作とは限らないため（運搬・採掘・召喚など）
                Sprite rateIcon = e.EffectID == "SlowConveyor" ? ItemSprite(ConveyorItem) : null;
                AddValue(row, rateIcon, null, e.RateText, font, icon, ValueColor);
            }
            if (e.Items.Count > 0)
            {
                AddValue(row, TagSprite(_payTag), _payTag, "-" + Format(TotalPay(e)), font, icon, ValueColor);
            }
            // 何も出なかった行（まだ払っていない固定費用など）は空欄にしない
            if (row.childCount == before)
            {
                AddValue(row, null, null, "-", font, icon, SubColor);
            }
        }

        /// <summary>失った資源を合計行に並べると入りきらないか。資源の枠（既定 2）を超えたら内訳へ。
        /// 3 つ並べると課税名を詰めても右端いっぱいになる。徴発（現金以外の全資源）と
        /// 源泉徴収（バフが出す資源すべて）はほぼ常にこちらになる</summary>
        private static bool ResourcesInBreakdown(TaxEntry e)
        {
            return e.LostResources.Count > TaxLedgerPlugin.ResourceColumns.Value;
        }

        /// <summary>失った資源を決まった順（建材…燃料、最後に現金など）で返す。辞書の順は積んだ順なので揃える</summary>
        private static List<string> LostResourceOrder(TaxEntry e)
        {
            List<string> tags = new List<string>();
            for (int i = 0; i < ResourceOrder.Length; i++)
            {
                if (e.LostResources.ContainsKey(ResourceOrder[i])) { tags.Add(ResourceOrder[i]); }
            }
            foreach (KeyValuePair<string, int> kv in e.LostResources)
            {
                if (!tags.Contains(kv.Key)) { tags.Add(kv.Key); }
            }
            return tags;
        }

        private static int TotalPay(TaxEntry e)
        {
            int total = 0;
            foreach (KeyValuePair<string, ItemLoss> kv in e.Items) { total += Pay(kv.Value.Resources); }
            return total;
        }

        /// <summary>そのシフトの返済資源を本体から引く。最後の 1 回だけアイテム納品になる任務では
        /// タグが無い（"None"）ので、その場合は現金に倒す。</summary>
        /// <summary>そのシフトの返済資源（ダミーを作る側からも使う）。</summary>
        internal static string PayTag()
        {
            return CurrentPayTag();
        }

        private static string CurrentPayTag()
        {
            try
            {
                GameStateManager manager = GameStateManager.Instance;
                WinCondition win = manager != null ? manager.GetWinCondition() : null;
                string tag = win != null ? win.GetTargetTag() : null;
                if (string.IsNullOrEmpty(tag) || tag == "None") { return CashTag; }
                return tag;
            }
            catch (Exception) { return CashTag; }
        }

        private static int Pay(Dictionary<string, int> res)
        {
            int amount;
            res.TryGetValue(_payTag, out amount);
            return amount;
        }

        /// <summary>資源を持たない行のための空の表（枠の数だけ空きを置くために渡す）。</summary>
        private static readonly Dictionary<string, int> NoResources = new Dictionary<string, int>();

        /// <summary>現金以外の資源を、決まった順・決まった幅で並べる。
        /// 現金はこのあとに置くので、桁が増えても現金の位置は動かない。</summary>
        private static void AddOtherResources(RectTransform row, Dictionary<string, int> res, int font, int icon)
        {
            // 資源の数は行ごとに違う。そのまま並べるとアイコンの位置が行ごとにずれるので、
            // 枠の数を決め打ちにして左から詰め、余った枠は空ける
            int slots = TaxLedgerPlugin.ResourceColumns.Value;
            int used = 0;
            for (int i = 0; i < ResourceOrder.Length && used < slots; i++)
            {
                string tag = ResourceOrder[i];
                if (tag == _payTag) { continue; }   // 右端に出すので二重に並べない
                int amount;
                if (!res.TryGetValue(tag, out amount) || amount == 0) { continue; }
                AddIcon(row, TagSprite(tag), tag, icon, font);
                // 右に隙間を空ける。詰めると次の資源のアイコンとくっついて読めない
                AddNumber(row, "-" + Format(amount), font, SubValueColor,
                    TaxLedgerPlugin.ResourceWidth.Value, TaxLedgerPlugin.NumberGap.Value);
                used++;
            }
            // 空きは部品 1 つで作る。2 つ置くと部品の間の隙間が余計に入り、行が広がる。
            // 埋まった枠 1 つ分 = アイコン + 隙間 + 数字。空きの幅はそれに合わせる
            int empty = slots - used;
            int slotWidth = icon + TaxLedgerPlugin.ColumnSpacing.Value
                + TaxLedgerPlugin.ResourceWidth.Value + TaxLedgerPlugin.NumberGap.Value;
            for (int i = 0; i < empty; i++)
            {
                AddGap(row, slotWidth);
            }
        }

        /// <summary>読み込み済みの絵を名前で引く（本体に取り出す口が無いもの用）。</summary>
        private static Sprite SpriteByName(string name)
        {
            Sprite cached;
            if (SpriteCache.TryGetValue(name, out cached) && cached != null) { return cached; }
            try
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == name)
                    {
                        SpriteCache[name] = all[i];
                        return all[i];
                    }
                }
            }
            catch (Exception) { }
            TaxLedgerPlugin.Log.LogWarning("[view] sprite not loaded: " + name);
            return null;
        }

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        private static Sprite PatronSprite(int patronID)
        {
            try { return Patrons.GetFaceIcon((Patron)patronID); }
            catch (Exception) { return null; }
        }

        private static int SubFontSize()
        {
            return Mathf.Max(8, TaxLedgerPlugin.FontSize.Value - TaxLedgerPlugin.SubStep.Value);
        }

        private static int SubIconSize()
        {
            return Mathf.Max(8, TaxLedgerPlugin.IconSize.Value - TaxLedgerPlugin.SubStep.Value);
        }

        /// <summary>大きい数は K・M に丸める（桁が伸びると列が崩れるため）。</summary>
        /// <summary>同じ表の中で桁の書き方を揃える。reference（表の最大値）が K なら小さい値も K で出す</summary>
        private static string FormatLike(int value, int reference)
        {
            int abs = value < 0 ? -value : value;
            string sign = value < 0 ? "-" : "";
            if (reference < 10000) { return value.ToString("N0"); }
            if (reference < 1000000) { return sign + (abs / 1000f).ToString("0.#") + "K"; }
            return sign + (abs / 1000000f).ToString("0.##") + "M";
        }

        private static string Format(int value)
        {
            int abs = value < 0 ? -value : value;
            string sign = value < 0 ? "-" : "";
            if (abs < 10000) { return value.ToString("N0"); }
            if (abs < 1000000) { return sign + (abs / 1000f).ToString("0.#") + "K"; }
            return sign + (abs / 1000000f).ToString("0.##") + "M";
        }

        /// <summary>右端の「アイコン + 数字」。数字は決め打ちの幅で右に寄せ、
        /// 絵の無いものも同じ幅の枠を置く。こうしないと桁数で絵の位置がずれる。</summary>
        private static void AddValue(RectTransform row, Sprite sprite, string fallbackID, string value,
            int font, int icon, Color color)
        {
            if (sprite != null)
            {
                AddIcon(row, sprite, fallbackID, icon, font);
            }
            else if (fallbackID != null)
            {
                AddLabel(row, Name(fallbackID), font);
                AddGap(row, icon);
            }
            else
            {
                AddGap(row, icon);
            }
            AddNumber(row, value, font, color);
        }

        /// <summary>課税名。行の余りを埋め、入りきらなければ字を小さくする。</summary>
        private static void AddName(RectTransform row, string value, int font)
        {
            GameObject go = NewText(row, value, font, NameColor, true);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            int min = Mathf.Max(10, font - TaxLedgerPlugin.NameShrink.Value);
            t.enableAutoSizing = true;
            t.fontSizeMax = font;
            t.fontSizeMin = min;
            t.overflowMode = TextOverflowModes.Ellipsis;

            // 課税名は字を詰めるところまでで踏みとどまる。横のラベル（必要打数）より
            // 先に譲ると、名前が「Agricultural …」のように切れて読めなくなる
            float full = t.GetPreferredValues(value).x;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredWidth = full;
            le.minWidth = full * min / Mathf.Max(1, font);
        }

        /// <summary>幅が足りなければ自分の字を縮めて譲るラベル。
        /// 英語の "Required Hits" は日本語の「必要打数」より長く、そのままだと課税名を押し出す。</summary>
        private static void AddShrinkingLabel(RectTransform row, string value, int font)
        {
            if (string.IsNullOrEmpty(value)) { return; }
            GameObject go = NewText(row, value, font, SubColor, false);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            float full = t.GetPreferredValues(value).x;
            t.enableAutoSizing = true;
            t.fontSizeMax = font;
            t.fontSizeMin = Mathf.Max(9, font - TaxLedgerPlugin.NameShrink.Value * 2);
            t.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = full;
            le.minWidth = 0f;
            le.flexibleWidth = 0f;
        }

        private static void AddLabel(RectTransform row, string value, int font)
        {
            AddFixedText(row, value, font, SubColor);
        }

        /// <summary>縮まない文字。行が詰まると本体の並べ方が文字箱を潰し、
        /// 隣の文字とくっついて見えるので、必要な幅を下限にする。</summary>
        private static void AddFixedText(RectTransform row, string value, int font, Color color)
        {
            if (string.IsNullOrEmpty(value)) { return; }
            GameObject go = NewText(row, value, font, color, false);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            float width = t.GetPreferredValues(value).x;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
        }

        private static void AddNumber(RectTransform row, string value, int font, Color color)
        {
            AddNumber(row, value, font, color, TaxLedgerPlugin.ValueWidth.Value);
        }

        private static void AddNumber(RectTransform row, string value, int font, Color color, int width)
        {
            AddNumber(row, value, font, color, width, 0);
        }

        private static void AddNumber(RectTransform row, string value, int font, Color color, int width, int rightGap)
        {
            GameObject go = NewText(row, value, font, color, false);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineRight;
            t.margin = new Vector4(0f, 0f, rightGap, 0f);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width + rightGap;
            le.minWidth = width + rightGap;
        }

        private static void AddGap(RectTransform row, int width)
        {
            GameObject go = new GameObject("Gap", typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(row, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
        }

        /// <summary>課税と課税の間の細い線。どこまでが 1 つの課税か分かるように。</summary>
        private static void AddSeparator(RectTransform rows)
        {
            GameObject go = new GameObject("Separator", typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(rows, false);
            Image image = go.AddComponent<Image>();
            Color c = LWFColors.HEADER_COLOR;
            c.a = 0.14f;
            image.color = c;
            image.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.minHeight = 1f;
        }

        // ---------------------------------------------------------------
        // 行の部品
        // ---------------------------------------------------------------

        private static RectTransform NewRow(RectTransform parent, int indent, int height)
        {
            GameObject go = new GameObject("Row", typeof(RectTransform));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.localScale = Vector3.one;
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = TaxLedgerPlugin.ColumnSpacing.Value;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(indent, 0, 0, 0);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            return r;
        }

        private static void AddText(RectTransform row, string value, int font, Color color, bool bold)
        {
            NewText(row, value, font, color, bold);
        }

        private static GameObject NewText(RectTransform row, string value, int font, Color color, bool bold)
        {
            GameObject go = UnityEngine.Object.Instantiate(_template.gameObject, row, false);
            go.name = "Text";
            go.SetActive(true);
            DisableLocalizers(go);
            StripLayout(go);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.text = value;
            t.color = color;
            t.fontSize = font;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.margin = Vector4.zero;
            go.GetComponent<RectTransform>().localScale = Vector3.one;
            return go;
        }

        private static void AddIcon(RectTransform row, Sprite sprite, string fallbackID, int size, int font)
        {
            if (sprite == null)
            {
                // 絵が無いものは名前で出す（名前も無いものは何も置かない）
                if (fallbackID != null) { AddText(row, Name(fallbackID), font, NameColor, false); }
                return;
            }
            GameObject go = new GameObject("Icon", typeof(RectTransform));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(row, false);
            r.localScale = Vector3.one;
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
        }

        private static void AddFlex(RectTransform row)
        {
            GameObject go = new GameObject("Flex", typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(row, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = TaxLedgerPlugin.ColumnSpacing.Value;
        }

        /// <summary>複製元に付いている配置の指示と中身を落とす（親の並べ方に任せる）。</summary>
        private static void StripLayout(GameObject go)
        {
            ContentSizeFitter fit = go.GetComponent<ContentSizeFitter>();
            if (fit != null) { UnityEngine.Object.DestroyImmediate(fit); }
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le != null) { UnityEngine.Object.DestroyImmediate(le); }
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            }
        }

        // ---------------------------------------------------------------
        // ゲームの文字と絵
        // ---------------------------------------------------------------

        /// <summary>縦のスクロールバーの幅。組む時点では実測が 0 になることがあるので、
        /// その場合は cfg の値を使う。ここが 0 だと開いたときに文字が動く。</summary>
        private static int BarWidth(ScrollRect scroll)
        {
            int fallback = TaxLedgerPlugin.ScrollbarWidth.Value;
            if (scroll == null || scroll.verticalScrollbar == null) { return fallback; }
            RectTransform bar = scroll.verticalScrollbar.transform as RectTransform;
            if (bar == null) { return fallback; }
            int w = (int)bar.rect.width;
            if (w <= 0) { w = (int)bar.sizeDelta.x; }
            return w > 0 ? w : fallback;
        }

        private static void SetTitleText(RectTransform title)
        {
            if (title == null) { return; }
            TextMeshProUGUI t = title.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t == null) { return; }
            DisableLocalizers(title.gameObject);
            t.text = Message("Tax");
        }

        private static string SectionName(int slot)
        {
            return Message("TaxSection" + slot);
        }

        private static string Message(string key)
        {
            return Safe(delegate { return LocalizedTextGetter.GetMessageData(key); }, key);
        }

        private static string TaxName(string effectID)
        {
            return Safe(delegate { return StripRichText(LocalizedTextGetter.GetTaxData(effectID)); }, effectID);
        }

        private static string Name(string id)
        {
            return Safe(delegate
            {
                string s = TagParamGetter.HasTag(id) ? TagParamGetter.GetName(id) : ItemParamGetter.GetName(id);
                return StripRichText(s);
            }, id);
        }

        private static Sprite TagSprite(string tagID)
        {
            try { return TagParamGetter.GetSprite(tagID); }
            catch (Exception) { return null; }
        }

        private static Sprite ItemSprite(string itemID)
        {
            try { return ItemParamGetter.GetSprite(itemID); }
            catch (Exception) { return null; }
        }

        /// <summary>ゲームの名前には色や下線の指定が付く。行では邪魔なので外す。</summary>
        private static string StripRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
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

        private static string Safe(Func<string> get, string fallback)
        {
            try
            {
                string s = get();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch (Exception) { return fallback; }
        }

        // ---------------------------------------------------------------
        // 道具
        // ---------------------------------------------------------------

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) { return null; }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal)) { return child; }
                Transform found = FindChildRecursive(child, name);
                if (found != null) { return found; }
            }
            return null;
        }

        private static void DisableLocalizers(GameObject go)
        {
            UnityEngine.Localization.Components.LocalizeStringEvent[] events =
                go.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true);
            for (int i = 0; i < events.Length; i++) { events[i].enabled = false; }
        }

        /// <summary>枠の中身をログに書く（開発用。名前と大きさを確かめる）。</summary>
        public static void DumpResultFrame()
        {
            Transform body = FindResultBody();
            if (body == null) { TaxLedgerPlugin.Log.LogInfo("[dump] " + BodyName + " not found"); return; }
            Transform root = body.parent != null ? body.parent : body;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[dump] ").Append(GetPath(root)).Append('\n');
            Dump(root, 0, sb);
            TaxLedgerPlugin.Log.LogInfo(sb.ToString());
        }

        private static void Dump(Transform t, int depth, System.Text.StringBuilder sb)
        {
            RectTransform r = t as RectTransform;
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
            if (r != null) { sb.Append(" ").Append(r.rect.width.ToString("0")).Append('x').Append(r.rect.height.ToString("0")); }
            Component[] cs = t.GetComponents<Component>();
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] == null || cs[i] is Transform) { continue; }
                sb.Append(" [").Append(cs[i].GetType().Name).Append(']');
                TextMeshProUGUI tmp = cs[i] as TextMeshProUGUI;
                if (tmp != null) { sb.Append(" \"").Append(tmp.text).Append("\" ").Append(tmp.fontSize); }
            }
            sb.Append('\n');
            for (int i = 0; i < t.childCount; i++) { Dump(t.GetChild(i), depth + 1, sb); }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
