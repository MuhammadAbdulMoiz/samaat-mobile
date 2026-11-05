<p align="center"><img src="docs/images/logo.png" width="72" alt="Samaat logo" /></p>

<h1 align="center">Samaat for Android</h1>

<p align="center">
  <b>Your earbuds on your phone, and one tap to move them to your computer and back.</b><br />
  The phone app for <a href="https://github.com/MuhammadAbdulMoiz/samaat.io">Samaat</a>.
</p>

<p align="center">
  <a href="docs/TRY-IT.md">Try it</a> &nbsp;·&nbsp;
  <a href="#build">Build</a> &nbsp;·&nbsp;
  <a href="#how-its-built">How it's built</a>
</p>

<p align="center">
  <img src="docs/images/mobile-devices.png" width="24%" alt="Earbuds list with battery levels and a low-battery warning" />
  <img src="docs/images/mobile-device-artwork.png" width="24%" alt="Device page with the earbuds' photos, each with its battery level" />
  <img src="docs/images/mobile-device.png" width="24%" alt="Device page with Bring to this phone" />
  <img src="docs/images/mobile-computer-code.png" width="24%" alt="Pairing with a computer by comparing a 6-digit code" />
</p>

## What it does

- **Move your earbuds between the phone and your computer**, from either one, even earbuds that connect to one device at a time. The computer checks the phone, disconnects them there, and the phone connects them. If that doesn't happen, they go back where they were.
- **Pair once by comparing a code.** The two apps talk over your Wi-Fi with an encrypted link: no account, no server.
- **Battery, noise control and EQ**, with the desktop's drivers (Jieli RCSP, Google Fast Pair, the BLE Battery Service) and the desktop's device photos.
- **Looks like the desktop app**: same design system, same logo, same artwork.

Android doesn't let apps connect a headset silently on most phones, so a move may ask you to tap your earbuds once in Android's audio output picker. There's no iPhone app: iOS doesn't let apps connect or disconnect Bluetooth headphones at all.

## Try it

**[docs/TRY-IT.md](docs/TRY-IT.md)**: run the phone app on your laptop (no phone needed) and pair it with the desktop app, or install the APK on an Android phone. CI builds the APK: open the latest **CI** run on the **Actions** tab and download **samaat-android-apk**.

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). The shared code comes in as a git submodule, so clone with `--recursive`:

```bash
git clone --recursive https://github.com/MuhammadAbdulMoiz/samaat-mobile.git
cd samaat-mobile
# Already cloned without it?  git submodule update --init

dotnet run --project src/Samaat.Mobile.Preview      # the phone app in a window on your computer
dotnet test Samaat.Mobile.slnx                      # tests (set SAMAAT_SCREENSHOTS=<dir> to save screenshots)
```

The Android app needs the Android workload and SDK:

```bash
dotnet workload install android
dotnet build src/Samaat.Android -t:InstallAndroidDependencies -p:AndroidSdkDirectory=<sdk folder> -p:AcceptAndroidSDKLicenses=True
dotnet publish src/Samaat.Android -c Release -p:AndroidSdkDirectory=<sdk folder> -o artifacts/apk
```

## How it's built

```
src/
  Samaat.Mobile/             the phone UI (Avalonia), shared by the app, the preview and the tests
  Samaat.Mobile.Preview/     the phone app in a window on a computer, with simulated earbuds
  Samaat.Platform.Android/   Android Bluetooth audio, paired devices, RFCOMM and GATT
  Samaat.Android/            the Android app: activity, foreground service, permissions
tests/
  Samaat.Mobile.Tests/       the phone UI headless, against a real computer link; accessibility checks; screenshots
external/samaat.io/          the desktop repo (submodule): Core, phone link, design system, drivers, protocols, devices/
```

| Solution | What |
|---|---|
| `Samaat.Mobile.slnx` | Everything that builds without the Android SDK. CI builds and tests it on Linux and Windows. |
| `Samaat.Mobile.Android.slnx` | Everything, including the Android app. |

The two repos share one copy of the code they have in common. `Directory.Build.props` and `Directory.Packages.props` import the desktop repo's, so compiler settings and package versions match too. The design, protocol and security notes live in the desktop repo: [phone handoff](https://github.com/MuhammadAbdulMoiz/samaat.io/blob/claude/phone-handoff/docs/modules/phone-handoff.md), [plan](https://github.com/MuhammadAbdulMoiz/samaat.io/blob/claude/phone-handoff/docs/plans/2026-11-04-phone-handoff.md), [design system](https://github.com/MuhammadAbdulMoiz/samaat.io/blob/claude/phone-handoff/docs/design/ui-audit.md).

### Updating the shared code

The submodule follows the desktop repo's `claude/phone-handoff` branch until that is merged:

```bash
git submodule update --remote external/samaat.io    # move to the latest shared code
git add external/samaat.io && git commit -m "Update shared code"
```

After the desktop branch is merged, switch the submodule to `master`: `git submodule set-branch --branch master external/samaat.io`, then update as above.

## Status

Built and tested in CI with simulated earbuds; the Android app hasn't been run on a phone yet. Please try it and report what happens, using the [hardware checklist](https://github.com/MuhammadAbdulMoiz/samaat.io/blob/claude/phone-handoff/docs/plans/2026-11-04-phone-handoff.md#hardware-check-not-doable-in-ci).
