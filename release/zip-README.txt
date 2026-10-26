LWF Tax Ledger
Lazy Witch's Factory の給与明細に課税欄を足す MOD
Adds a Taxation section to the Payslip in Lazy Witch's Factory

────────────────────────────────
入れかた
────────────────────────────────

1. BepInEx 5 を落として、ゲームのフォルダ
   （LazyWitchsFactory.exe と同じ場所）へ展開する

     https://github.com/BepInEx/BepInEx/releases
     BepInEx_win_x64_5.4.x.zip

2. 一度ゲームを起動して終了すると、BepInEx/plugins などが作られる

3. LwfTaxLedger.dll を BepInEx/plugins/ に入れる

4. Inferno を 1 回遊ぶ。結果画面の給与明細に課税欄が出る

MOD管理ソフトを使うなら Thunderstore から

     https://thunderstore.io/c/lazy-witchs-factory/p/KiyonakaNata/LwfTaxLedger/

────────────────────────────────
使いかた
────────────────────────────────

行をクリックすると内訳が開く。もう一度押すと閉じる

課税があるのは Inferno だけ。ほかの難易度では何も出ない

────────────────────────────────
消しかた
────────────────────────────────

BepInEx/plugins/LwfTaxLedger.dll を消す
設定も消すなら BepInEx/config/kiyonakanata.lwftaxledger.cfg

────────────────────────────────

Lazy Witch's Factory ver 0.27.0 / BepInEx 5.4.23.5 で確認
非公式のMOD、公式のサポート対象外

説明と画面は GitHub

     https://github.com/KiyonakaNata/lwf-tax-ledger


────────────────────────────────
English
────────────────────────────────

Install

1. Unpack BepInEx 5 (win x64) into the game folder

     https://github.com/BepInEx/BepInEx/releases

2. Start the game once and quit, so BepInEx/plugins is created

3. Put LwfTaxLedger.dll into BepInEx/plugins/

4. Play one Inferno run. The Payslip on the result screen lists the taxes

With a mod manager, install from Thunderstore

     https://thunderstore.io/c/lazy-witchs-factory/p/KiyonakaNata/LwfTaxLedger/

Use

Click a row to open its breakdown, click again to close.
Taxes exist on Inferno only; nothing is drawn on other difficulties.

Remove

Delete BepInEx/plugins/LwfTaxLedger.dll
Delete BepInEx/config/kiyonakanata.lwftaxledger.cfg to drop the settings

Tested with Lazy Witch's Factory ver 0.30.1 / BepInEx 5.4.23.5
Unofficial mod, not supported by the developer

     https://github.com/KiyonakaNata/lwf-tax-ledger
