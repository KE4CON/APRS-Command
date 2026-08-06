# Code Signing Setup — Windows (Azure) & macOS (Apple)

A slow, hand-held, click-by-click guide to signing APRS Command so users stop seeing "unknown
publisher / are you sure you want to run this?" warnings, and instead see **James Rospopo (KE4CON)** as
the verified publisher. No prior experience assumed. Every term is explained the first time it appears.

**How to use this guide:** do one Part at a time. You can't break anything by clicking around and
reading. The only slow bits are Microsoft's and Apple's identity checks, which happen in the background —
you start them, then wait.

> This is the **private** maintainer setup guide (accounts, credentials, commands). The **public**
> user-facing policy is `docs/release/CODE_SIGNING_POLICY.md`. **Never** put a real certificate, key, password,
> `.p12`, or `.p8` file into the repository — only their *names* ever appear in these files.

**Contents**
- [What "code signing" actually is](#what-code-signing-actually-is)
- [Part 1 — Windows (Azure Artifact Signing)](#part-1--windows-azure-artifact-signing)
- [Part 2 — macOS (Apple Developer ID + Notarization)](#part-2--macos-apple-developer-id--notarization)

---

## What "code signing" actually is

When you download a program, Windows and macOS ask: *"Do we know who made this, and has it been tampered
with?"* **Code signing** answers both. You get a **certificate** — a cryptographic ID card issued after a
trusted authority checks who you are — and your build process uses it to stamp your installer. The
operating system checks that stamp and, if it trusts the issuer, shows **your name** instead of a scary
warning.

- **Windows** trusts the stamp directly once the issuer is trusted. We'll use **Azure Artifact Signing**.
- **macOS** needs one extra step called **notarization** — you send the signed app to Apple, their
  servers scan it for malware, and send back a "ticket" you attach to the app. Only then does macOS trust
  it. We'll use an **Apple Developer ID** certificate plus notarization.

They're completely independent. Doing one doesn't affect the other.

---

# Part 1 — Windows (Azure Artifact Signing)

**What it costs:** about **$9.99/month** (the "Basic" plan). **What users will see:** your verified legal
name as the publisher. **Good news:** the GitHub automation is **already built** — it stays asleep until
you flip the switch in Phase 3, so nothing signs (and nothing can break) until you're ready.

**Vocabulary for this Part:**
- **Azure** — Microsoft's cloud platform. You'll create a free account and one paid signing service in it.
- **Subscription** — your billing container in Azure. Everything you create lives under a subscription.
- **Resource group** — a labeled folder that holds related Azure things. You'll make one to hold the
  signing service, so it's tidy and easy to delete later if you ever want to.
- **Resource provider** — a switch that turns on a *category* of Azure service. Signing is off until you
  turn on the "Microsoft.CodeSigning" provider once.
- **Service principal** — a "robot login." Instead of putting your personal password in GitHub, you
  create a limited robot account that's only allowed to sign, and give *that* to GitHub.

## Phase 1 — Set up the signing service in the Azure portal

### 1.1 — Sign in and check your billing type
1. Open a web browser and go to **https://portal.azure.com**.
2. Sign in with a Microsoft account (the same one is fine if you already have Outlook/Xbox/etc.).
3. If you've **never** used Azure, it will prompt you to start a subscription. Choose **Pay-As-You-Go**
   (there's no monthly minimum; you only pay for the ~$10 signing plan). Enter a payment method.
4. **Important for individuals:** in the top search bar type **Cost Management + Billing** and open it →
   **Billing** → **Billing accounts**. Confirm your **account type is "Individual"** and that your
   **legal name and address are exactly right**. The certificate will copy this name *character for
   character* — if your name is misspelled here, it's misspelled on your certificate. Fix it here first
   if needed.

### 1.2 — Turn on the signing service (register the provider)
1. In the top search bar, type **Subscriptions** and click it.
2. Click the name of your subscription (e.g. "Pay-As-You-Go").
3. In the **left-hand menu**, scroll to **Settings** → click **Resource providers**.
4. In the filter box, type **CodeSigning**.
5. Click the row **Microsoft.CodeSigning** (its Status will say *NotRegistered*).
6. Click **Register** at the top. The status changes to *Registering…*, then *Registered* after a minute
   (click **Refresh** if it seems stuck). ✅ You only ever do this once.

### 1.3 — Create the Artifact Signing account
"Account" here just means the signing service instance.
1. In the top search bar, type **Artifact Signing Accounts** and click it.
2. Click **+ Create** (top-left).
3. Fill the form:
   - **Subscription:** your subscription.
   - **Resource group:** click **Create new**, type `rg-signing`, click OK.
   - **Account name:** something 3–24 letters/numbers starting with a letter, e.g. `aprscommandsign`.
     (Must be globally unique; if it complains, add a couple digits.)
   - **Region:** pick one near you and **write it down** — it decides your "endpoint" web address. Common
     choices and their endpoints:
     - West US 2 → `https://wus2.codesigning.azure.net/`
     - East US → `https://eus.codesigning.azure.net/`
     - West Europe → `https://weu.codesigning.azure.net/`
   - **Pricing tier / SKU:** choose **Basic**.
4. Click **Review + Create**, then **Create**. Wait ~30 seconds, then click **Go to resource**. You're
   now looking at your signing account's **Overview** page — keep this tab.

### 1.4 — Give yourself permission to do the identity check
Azure separates "owning the account" from "being allowed to verify an identity." Grant yourself the
verifier role:
1. On your signing account page, in the left menu click **Access control (IAM)**.
2. Click **+ Add** → **Add role assignment**.
3. In the **Role** search box, type **Artifact Signing Identity Verifier**, select it, click **Next**.
4. **Assign access to:** leave **User, group, or service principal**. Click **+ Select members**, search
   your own name/email, select it, click **Select**.
5. Click **Review + assign** (twice). ✅ Now the "New identity" button in the next step will be clickable
   (it's greyed out without this role).

### 1.5 — Prove who you are (identity validation)
This is the part Microsoft reviews. For an individual it's largely automated via your phone.
1. On your signing account page, left menu → under **Objects** click **Identity validations**.
2. There's a dropdown that says **Organization** — change it to **Individual**.
3. Click **+ New Identity** → choose **Public**.
4. Select your **billing account** from the dropdown. The name/address fields fill in automatically and
   are read-only (that's why 1.1 mattered). Review the **Certificate subject preview** — this is exactly
   what will appear as your publisher name.
5. Click **Create**. Status shows **In Progress**, then changes to **Action Required**.
6. Click your name in the list → a panel opens on the right → click the link **"Please complete your
   verification here."**
7. A Microsoft **Verified ID** page opens. Sign in with the **same email** you've been using.
8. Click **Get verified** — you're handed to a verification partner (AU10TIX). Steps there:
   - Enter your email → they send a **PIN** → type it in.
   - Enter your phone number.
   - It shows a **QR code** — scan it with your phone's camera.
   - On your phone, follow the prompts to **photograph a government photo ID** (passport, driver's
     license, or state ID). Tips: flat surface, good light, no flash, no fingers over the card.
   - When done, it adds a "Verified ID" to the **Microsoft Authenticator** app on your phone (install it
     from your app store first if you don't have it).
9. Back in the browser, **share the credential** when asked. The page says **Verification Successful**.
10. Return to the Azure tab. Within a few minutes the status becomes **Completed**. (Microsoft *allows*
    up to **1–20 business days** if they need a manual look, but individual checks are usually quick.)

    *If it says Failed:* you must start a **new** identity validation (you can't edit a failed one).
    Usually it's a name/address mismatch with billing, or a blurry ID photo — fix and redo.

### 1.6 — Create the certificate profile
The "profile" is the template your actual signing certificates are minted from.
1. Signing account page → under **Objects** click **Certificate profiles**.
2. Click **+ Create** → choose **Public Trust**.
3. **Certificate profile name:** e.g. `aprscommand-public` (5–100 letters/numbers).
4. **Verified CN and O:** select the identity validation you just completed.
5. Leave other options default. Click **Create**. ✅ You now have everything Azure needs.

### 1.7 — Copy the three names GitHub will need
Keep these handy for Phase 3:
- **Endpoint** — your region URI from 1.3 (e.g. `https://wus2.codesigning.azure.net/`).
- **Account name** — from 1.3 (e.g. `aprscommandsign`).
- **Certificate profile name** — from 1.6 (e.g. `aprscommand-public`).

## Phase 2 — Create the "robot login" (service principal)

The easiest way is Azure's built-in **Cloud Shell** (no installing anything).

### 2.1 — Get your account's Resource ID
1. Go back to your **Artifact Signing account** page (search "Artifact Signing Accounts" → click it).
2. Left menu → **Settings** → **Properties** (or **JSON View**, a link near the top-right).
3. Copy the long **Resource ID** — it looks like:
   `/subscriptions/1111.../resourceGroups/rg-signing/providers/Microsoft.CodeSigning/codeSigningAccounts/aprscommandsign`
   That whole string is what goes after `--scopes` below.

### 2.2 — Open Cloud Shell and run one command
1. At the very top of the Azure portal, click the **`>_`** icon (Cloud Shell). If it's your first time,
   choose **Bash**, and let it create a small storage account when prompted (accept defaults).
2. When you see a `$` prompt, paste this — replacing the `--scopes` value with the Resource ID you copied:
   ```bash
   az ad sp create-for-rbac \
     --name "aprs-command-signing-ci" \
     --role "Trusted Signing Certificate Profile Signer" \
     --scopes "<PASTE-THE-RESOURCE-ID-HERE>"
   ```
3. Press Enter. After a few seconds it prints a small block of text like:
   ```json
   {
     "appId": "aaaaaaaa-....",
     "displayName": "aprs-command-signing-ci",
     "password": "abc123~....",
     "tenant": "bbbbbbbb-...."
   }
   ```
4. **Copy `appId`, `password`, and `tenant` somewhere safe right now** — the `password` is shown **only
   this once**. (If you miss it, just run the command again; it makes a fresh one.)

## Phase 3 — Flip the switch in GitHub

1. In a browser go to **https://github.com/KE4CON/APRS-Command**.
2. Click **Settings** (top row of the repo). If you don't see it, make sure you're signed in as KE4CON.
3. Left menu → **Secrets and variables** → **Actions**.
4. On the **Secrets** tab, click **New repository secret** and add these three, one at a time (Name,
   then Value, then **Add secret**):

   | Name | Value |
   |---|---|
   | `AZURE_TENANT_ID` | the `tenant` from 2.2 |
   | `AZURE_CLIENT_ID` | the `appId` from 2.2 |
   | `AZURE_CLIENT_SECRET` | the `password` from 2.2 |

5. Click the **Variables** tab (next to Secrets), click **New repository variable**, and add these three:

   | Name | Value |
   |---|---|
   | `AZURE_SIGNING_ENDPOINT` | your endpoint, e.g. `https://wus2.codesigning.azure.net/` |
   | `AZURE_SIGNING_ACCOUNT` | your account name, e.g. `aprscommandsign` (**this is the on-switch**) |
   | `AZURE_SIGNING_CERT_PROFILE` | your profile name, e.g. `aprscommand-public` |

✅ **That's it.** The next time you create a version tag (a release like `v1.0.0`), the workflow will run
its "Sign installer (Azure Artifact Signing)" step automatically and the downloaded `Setup.exe` will show
your name instead of a warning.

**To test without a real release:** create a throwaway tag from GitHub Desktop or the site (e.g.
`v0.5.1-test`), watch the **Actions** tab — the sign step should run (not say "skipped"). You can delete
the draft release and tag afterward.

**Reality note:** Azure signing usually removes the SmartScreen warning immediately because it chains to
a Microsoft-trusted authority. There were some 2026 reports of the warning briefly returning when Azure
rotates to a new back-end authority that hasn't built reputation yet — uncommon, and not something you
control, but worth knowing so it doesn't surprise you.

---

# Part 2 — macOS (Apple Developer ID + Notarization)

You already paid the **$99/year** Apple Developer membership — this is what to do next. Everything here
is done **on a Mac**. Take it slowly; there are more moving parts than Windows, but each is small.

**Vocabulary for this Part:**
- **Developer ID Application certificate** — the ID card that lets you sign apps for distribution
  *outside* the Mac App Store (which is what we're doing).
- **Keychain Access** — the built-in Mac app that stores certificates and keys. (Find it in
  Applications → Utilities, or press ⌘-Space and type "Keychain Access.")
- **Notarization** — you upload your signed app to Apple; their servers scan it for malware and return a
  **ticket**.
- **Stapling** — attaching that ticket onto your app so it's trusted even offline.
- **Hardened runtime** — a stricter security mode Apple requires for notarized apps. It blocks a few
  things .NET needs unless you explicitly allow them (that's the "entitlements" file).
- **`.p12`** — a file that bundles your certificate **and** its private key, protected by a password.
- **`.p8`** — the App Store Connect API key file, used to log in to Apple's notarization service.
- **base64** — a way to turn a binary file into plain text so it can be pasted into a GitHub secret.

### Step 1 — Find your Team ID
1. Go to **https://developer.apple.com/account** and sign in.
2. Click **Membership details** (left menu).
3. Copy your **Team ID** — 10 characters like `AB12CD34EF`. Your full signing name will be:
   `Developer ID Application: James Rospopo (AB12CD34EF)`.

### Step 2 — Create the Developer ID Application certificate
**Easiest way — with Xcode:**
1. Install **Xcode** from the Mac App Store (it's large; let it finish).
2. Open Xcode → menu **Xcode** → **Settings…** → **Accounts** tab.
3. Click **+** (bottom-left) → **Apple ID** → sign in with your developer Apple ID.
4. Select your team in the list → click **Manage Certificates…** (bottom-right).
5. Click the **+** (bottom-left of that dialog) → choose **Developer ID Application**.
6. After a moment it appears in the list. ✅ The certificate and its **private key** are now in your
   login keychain. Close the dialogs.

**No-Xcode way (if you'd rather not install Xcode):**
1. Open **Keychain Access** → menu **Keychain Access** → **Certificate Assistant** → **Request a
   Certificate From a Certificate Authority…**
2. Enter your email; leave "CA Email" blank; choose **Saved to disk**; **Continue**; save the
   `.certSigningRequest` file to your Desktop.
3. Go to **https://developer.apple.com/account/resources/certificates** → click the **+**.
4. Under "Software," choose **Developer ID Application** → **Continue**.
5. Upload the `.certSigningRequest` file → **Continue** → **Download** the resulting `.cer` file.
6. Double-click the downloaded `.cer` — it installs into Keychain Access.

### Step 3 — Export the certificate as a `.p12` (for GitHub later)
1. Open **Keychain Access** → left side, select **login** keychain, and the **My Certificates** category.
2. Find **Developer ID Application: James Rospopo (…)**. Click the small triangle to the left to expand
   it — you should see a **private key** listed underneath. (If there's no key underneath, the export
   won't work — that means the cert was created on a different Mac; redo Step 2 on this Mac.)
3. Hold **⌘** and click **both** the certificate row **and** the private key row so both are selected.
4. Right-click → **Export 2 items…** → File Format **Personal Information Exchange (.p12)** → **Save**
   (e.g. to Desktop as `DeveloperID.p12`).
5. It asks for a password to protect the file — set one and remember it (this becomes the
   `APPLE_CERT_PASSWORD` secret). It may then ask for your Mac login password to allow the export — enter
   it and click **Allow**.
6. Turn the file into text for GitHub. Open **Terminal** (Applications → Utilities → Terminal) and run:
   ```bash
   base64 -i ~/Desktop/DeveloperID.p12 | pbcopy
   ```
   This copies the encoded certificate to your clipboard (nothing prints — that's normal). You'll paste
   it into a GitHub secret in Step 7. (Do this again just before pasting, so it's fresh on the clipboard.)

### Step 4 — Create an App Store Connect API key (for notarization login)
1. Go to **https://appstoreconnect.apple.com** → sign in.
2. Click **Users and Access** (top menu).
3. Click the **Integrations** tab → in the left list choose **App Store Connect API** → **Team Keys**.
4. Click **+** (Generate API Key). Give it a name like `notarization`, set **Access** to **Developer**,
   click **Generate**.
5. **Download the key file now** — a file named `AuthKey_XXXXXXXXXX.p8`. **Apple lets you download it
   only once.** Save it somewhere safe.
6. On that page note two IDs:
   - **Key ID** — the 10-character code in the key's row (also in the filename).
   - **Issuer ID** — a long UUID shown near the top of the Keys section.
7. Encode the key for GitHub (Terminal):
   ```bash
   base64 -i ~/Downloads/AuthKey_XXXXXXXXXX.p8 | pbcopy
   ```

### Step 5 — Add the .NET entitlements file to the repo
.NET runs a "just-in-time" compiler that the hardened runtime blocks by default, which makes
notarization fail. You grant three exceptions. Create a new file in the repo at
**`build/entitlements.mac.plist`** with exactly this content:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>com.apple.security.cs.allow-jit</key>                         <true/>
  <key>com.apple.security.cs.allow-unsigned-executable-memory</key>  <true/>
  <key>com.apple.security.cs.disable-library-validation</key>        <true/>
</dict>
</plist>
```
(Commit this file — it's not a secret, it's part of the build.)

### Step 6 — Do it once by hand to prove it works
On your Mac, in the repo folder, first build the app bundle:
```bash
bash scripts/make-macos-app.sh osx-arm64
```
Now sign, notarize, and staple. Set two shortcuts first (use *your* Team ID):
```bash
ID="Developer ID Application: James Rospopo (AB12CD34EF)"
APP="artifacts/installers/APRS Command.app"
```
**6a — Sign the inside pieces first, then the whole bundle** (never use `--deep`; it mis-signs files):
```bash
# sign every bundled library
find "$APP/Contents/MacOS" -name "*.dylib" -exec \
  codesign --force --options runtime --timestamp \
    --entitlements build/entitlements.mac.plist --sign "$ID" {} \;
# sign the main program
codesign --force --options runtime --timestamp \
  --entitlements build/entitlements.mac.plist --sign "$ID" "$APP/Contents/MacOS/Aprs.Desktop"
# sign the bundle itself, last
codesign --force --options runtime --timestamp \
  --entitlements build/entitlements.mac.plist --sign "$ID" "$APP"
```
What the flags mean: `--force` = re-sign if already signed; `--options runtime` = turn on hardened
runtime (required); `--timestamp` = get a trusted time stamp so the signature doesn't "expire"
(required); `--entitlements` = the .NET exceptions from Step 5.

**6b — Check the signature is valid:**
```bash
codesign --verify --strict --verbose=2 "$APP"
```
You want to see `satisfies its Designated Requirement` / no errors.

**6c — Send it to Apple to be notarized** (uses your Step 4 key):
```bash
ditto -c -k --keepParent "$APP" "APRSCommand.zip"
xcrun notarytool submit "APRSCommand.zip" \
  --key ~/Downloads/AuthKey_XXXXXXXXXX.p8 \
  --key-id <KEY_ID> --issuer <ISSUER_ID> --wait
```
`--wait` makes it sit until Apple finishes (usually a couple of minutes) and print **Accepted** or
**Invalid**.

**6d — If Accepted, staple the ticket onto the app:**
```bash
xcrun stapler staple "$APP"
```

**6e — Rebuild the `.dmg` from the now-stapled app, and sign the `.dmg` too:**
```bash
bash scripts/make-macos-app.sh osx-arm64      # repackages the stapled .app into the .dmg
codesign --force --timestamp --sign "$ID" "artifacts/installers/APRSCommand-osx-arm64.dmg"
```

**If notarization says Invalid:** get the exact reason with
```bash
xcrun notarytool log <the-submission-id-it-printed> \
  --key ~/Downloads/AuthKey_XXXXXXXXXX.p8 --key-id <KEY_ID> --issuer <ISSUER_ID>
```
It names the offending file/entitlement. Almost always it's a missing `--options runtime`, a file that
didn't get signed, or a missing entitlement — fix and re-run 6a onward.

### Step 7 — Add the Mac secrets to GitHub, and I'll automate it
Once Step 6 works by hand, add these repo **secrets** (GitHub → Settings → Secrets and variables →
Actions → Secrets). Then tell me, and **I'll write the macOS signing job into `release.yml`** (import
the certificate into a temporary keychain, sign, notarize, staple) — dormant until configured, exactly
like the Windows one.

| Secret | What to paste |
|---|---|
| `APPLE_CERT_P12_BASE64` | the base64 text from Step 3.6 |
| `APPLE_CERT_PASSWORD` | the `.p12` password you set in Step 3.5 |
| `APPLE_SIGNING_IDENTITY` | `Developer ID Application: James Rospopo (TEAMID)` |
| `APPLE_API_KEY_P8_BASE64` | the base64 text from Step 4.7 |
| `APPLE_API_KEY_ID` | the Key ID from Step 4.6 |
| `APPLE_API_ISSUER_ID` | the Issuer ID from Step 4.6 |

### Apple gotchas (each of these has cost people an evening)
- **Sign inner files first, the bundle last. Never `--deep`.**
- **`--options runtime` and `--timestamp` are both mandatory** — leave either out and notarization fails.
- **The `.p8` key downloads only once** — if you lose it, revoke it and make a new one.
- **Your `.app` must be stapled, then packaged into the `.dmg`** (staple the app before making the dmg),
  or offline users still see a warning.
- **Renewals:** the Developer ID certificate lasts ~5 years, but the **$99 membership is annual** — if it
  lapses, signing/notarization stop working until you renew.

---

*Maintenance note: keep every real secret out of the repository. They belong only in GitHub Actions
secrets and your personal password manager. This document contains names and steps, never values.*
