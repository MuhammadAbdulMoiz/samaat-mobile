# Try Samaat on your laptop and phone

You need two repos: the desktop app ([samaat.io](https://github.com/MuhammadAbdulMoiz/samaat.io)) and this one. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Git](https://git-scm.com/) first.

```bash
git clone --recursive https://github.com/MuhammadAbdulMoiz/samaat-mobile.git
git clone https://github.com/MuhammadAbdulMoiz/samaat.io.git
cd samaat.io && git checkout claude/phone-handoff && cd ..
```

## 1. The desktop app (Windows laptop)

```bash
cd samaat.io
# Real earbuds plus three simulated ones (needed for step 2)
dotnet run --project src/Samaat.App -f net10.0-windows10.0.19041.0 -- --simulate
```

Leave out `-- --simulate` to see only your real paired earbuds. On a Mac or Linux laptop, use `-f net10.0` (simulated earbuds only).

## 2. The phone app on your laptop (no phone needed)

The preview opens the phone app in a phone-sized window. Its earbuds are the same simulated ones as the desktop's `--simulate`, and it uses the real network link, so it pairs with the desktop app like a phone would.

1. Keep the desktop app running with `--simulate`.
2. In a second terminal:

   ```bash
   cd samaat-mobile
   dotnet run --project src/Samaat.Mobile.Preview
   ```

3. On the desktop, go to **Settings → Phone → Pair a phone**. When Windows asks, **allow Samaat on private networks**.
4. In the phone window, open the **Computer** tab and tap **Pair** next to your computer. Check that both windows show the same code, then tap **Pair** in both.
5. Open **Simulated Buds Pro** in either app and press **Move to …** or **Bring … here**.

To start over, **Forget** the phone in the desktop's Settings, and **Forget this computer** on the phone. The preview keeps its pairing in `%LOCALAPPDATA%\Samaat\phone-preview`.

## 3. An Android phone

### Get the APK

- **From CI (easiest):** on this repo's **Actions** tab, open the latest **CI** run and download **samaat-android-apk** from **Artifacts**. To build a fresh one, click **Run workflow**.
- **Build it yourself:**

  ```bash
  cd samaat-mobile
  dotnet workload install android
  dotnet build src/Samaat.Android -t:InstallAndroidDependencies -p:AndroidSdkDirectory="%LOCALAPPDATA%\Android\Sdk" -p:AcceptAndroidSDKLicenses=True
  dotnet publish src/Samaat.Android -c Release -p:AndroidSdkDirectory="%LOCALAPPDATA%\Android\Sdk" -o artifacts/apk
  ```

### Install it

- **Without a cable:** copy `io.samaat.app-Signed.apk` to the phone, open it, and allow "Install unknown apps" when asked.
- **With a cable:** turn on **Developer options → USB debugging**, plug the phone in, then run `adb install io.samaat.app-Signed.apk`. Or run `dotnet build src/Samaat.Android -t:Run`, which builds, installs and starts it.

### Test it

1. Pair your earbuds with the phone in Android's Bluetooth settings first (once).
2. Open Samaat on the phone and allow **Nearby devices** and **notifications**.
3. Put the phone on the **same Wi-Fi** as the laptop. Guest and office networks often block devices from reaching each other; a home network or the laptop's hotspot works.
4. Pair: on the desktop, **Settings → Phone → Pair a phone**; on the phone, **Computer → Pair**. Compare the codes.
5. Move your real earbuds from either side. If Android shows the audio output picker or a notification, tap your earbuds: most Android phones don't let apps connect headphones silently.

Please note what happened, using the [hardware checklist](https://github.com/MuhammadAbdulMoiz/samaat.io/blob/claude/phone-handoff/docs/plans/2026-11-04-phone-handoff.md#hardware-check-not-doable-in-ci).

## 4. iPhone

There's no iPhone app, and there can't be one that does this. iOS doesn't let apps connect or disconnect Bluetooth headphones, and it doesn't let apps reach most earbuds' control channels. Use the phone preview on your laptop (step 2) to try the phone side.
