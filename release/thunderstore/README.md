# LWF Tax Ledger

Adds a Taxation section to the Payslip in **Lazy Witch's Factory** (tested on ver 0.27.0).

- The result screen lists what each tax took from the run, phase by phase
- Shown on Inferno
- Panel text follows the game language
- The screenshots use sample data

![Taxation section](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/en/result-full.jpg)

## Open a breakdown

- Click a row for the items, resources and converted value behind it
- Click again to close
- `Expand breakdown = true` in the config starts every row open

![Breakdown](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/en/ledger-breakdown.png)

## What each tax reports

| Tax | Value |
|---|---|
| Poll Tax, Property Tax, Consumption Tax | Fixed-cost points, and the total paid across repayments |
| Investment Tax, Damages, Specialty Product Tax | Resources lost |
| Centralization | Sales cancelled, and the items behind them |
| Sample Provision Order, Tariffs | Crafts lost and their value |
| The same, on buff recipes | Buff points lost. Shown in the breakdown only, since buffs cannot repay |
| Mining Tax, Agricultural Tax | Extra hits required |
| Compulsory Triple Check, Arrest | Crafts lost and their value, from the speed loss |
| Water Conservation Order | The transport rate it takes away |

![Fixed cost](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/en/ledger-fixed-cost.png)

## Install

- Install with a mod manager, or put `LwfTaxLedger.dll` into `BepInEx/plugins/`
- Play one Inferno run

## Settings

`BepInEx/config/kiyonakanata.lwftaxledger.cfg`

| Setting | Default | Values |
|---|---|---|
| `1. General/Enabled` | `true` | `false` = off |
| `1. General/Expand breakdown` | `false` | `true` = rows start open |

`[2. Layout]` is text and spacing. No adjustment needed.

`[9. Dev]` is for development. Leave `Enabled` at `false`.

## Remove

- Delete `BepInEx/plugins/LwfTaxLedger.dll`
- Delete `BepInEx/config/kiyonakanata.lwftaxledger.cfg` to drop the settings

## Links

- [GitHub](https://github.com/KiyonakaNata/lwf-tax-ledger)
- [Official mod guidelines](https://store.steampowered.com/news/app/3971650/view/699897618302503133)

Unofficial mod, not supported by the developer.
