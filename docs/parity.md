# Desktop parity

What the phone app has of the desktop app (`external/samaat.io/src/Samaat.App`), page by page. Checked on 2026-11-06 against the desktop's `feature/ui-redesign` at v1.1.0 (`4932ec7`).

**Done**: on the phone, with the desktop's wording. **N/A**: doesn't apply to a phone, or Android doesn't allow it. Nothing is left: every desktop page that makes sense on a phone is there. Ported desktop code (diagnostics, capability wording, multipoint, accent palette) names its source file in a header comment; moving it into shared code is a desktop follow-up.

## Overview

| Desktop | Phone | Notes |
|---|---|---|
| Device artwork with the battery ring, split photos per bud and case, "Change look" | Done | Same `devices/` photos and looks. |
| Battery tiles: left, right, case, with low and critical in words | Done | |
| "Overall" tile, renamed "Lowest bud" (owner decision) | Done | Label above the hero figure: "Lowest bud" when a bud reports a level, "Battery" when only a combined level exists. |
| Battery caption naming every piece without a level | Done | Same rule (the phone's `BatterySummary`). |
| Refresh battery ("Ask the device for fresh battery levels") | Done | Battery panel; off while disconnected. |
| The OS battery level when no protocol reports one | Done | Android's own HFP level (and its battery broadcast) fills in whenever the driver reports nothing, like the desktop's Windows level. |
| Noise control row and Links row leading to their tabs | Done | The phone shows both panels on the device page instead. |

## Sound

| Desktop | Phone | Notes |
|---|---|---|
| Noise control modes (selector) | Done | |
| Noise cancelling on/off switch for single-mode models | Done | |
| "Noise control unavailable" with the reason | Done | Was hidden on the phone before. |
| Noise cancelling strength and ambient sound level sliders | Done | Newest value wins while dragging, as on the desktop. |
| Extras: wind noise reduction, conversation awareness | Done | |
| Controls off while disconnected or read-only | Done | |
| Equalizer presets stored on the earbuds | Done | |
| "Equalizer unavailable" with the reason | Done | Was hidden on the phone before. |
| Share audio (several outputs, delay, "Switch here") | N/A | Windows audio routing (WASAPI); Android has no equivalent for apps. |

## Links

| Desktop | Phone | Notes |
|---|---|---|
| This computer: connect, disconnect, Bluetooth settings | Done | "This phone": Connect / Disconnect, and Bluetooth settings from the empty state and banner. Android may ask for one tap in its output picker. |
| Your phone: pair, move the earbuds, bring them back | Done | The phone's mirror: Computer tab (pair by code) and "Move to computer" / "Bring to this phone" on the device page. |
| Devices: multipoint switch and the hosts the earbuds report | Done | Device page, "Devices" panel: switch, hosts with this phone marked, Switch / Disconnect; off while disconnected. |
| "How links work" explainer | Done | Closed by default, as on the desktop. |

## Diagnostics

| Desktop | Phone | Notes |
|---|---|---|
| Recognition, features available, capability list | Done | "Diagnostics" at the bottom of the device page. |
| Copy report | Done | Includes the recent log lines; the phone now logs through the shared `SamaatLog`. |
| Technical details (Bluetooth services, IDs) | Done | Closed by default. |
| Event log | Done | Closed by default. |

## Settings

| Desktop | Phone | Notes |
|---|---|---|
| Appearance: mode (System, Light, Dark) | Done | Settings tab. Dark by default, as on the desktop. |
| Appearance: accent | Done | Lime, Emerald, Ice, Mono. |
| Reduce motion | Done | Follows Android's "Remove animations"; no in-app switch. |
| Text size | Done | Follows Android's font size (Avalonia 11.3 doesn't, so the phone scales its type ramp). |
| About: logo, version, GPL notice, Licence and Source code links | Done | Settings tab; links go to the GPL text and this repo. |
| Phone pairing, paired phones, Forget | Done | Computer tab: pair, "Forget this computer". |
| Battery alerts: low and critical thresholds, include the case | Done | The device page's low and critical states follow them at once. |
| System notifications | Done | Android notification channel "Battery alerts"; Samaat asks for notification permission only while this is on. |
| Layouts (Studio, Focus, Glance), device list filter | N/A | Window layouts; the phone has one screen size and lists the paired earbuds. |
| Keep running in the tray, start with Windows | N/A | Windows only. The phone keeps the computer link alive with a foreground service. |
