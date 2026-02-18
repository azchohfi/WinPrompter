# Store Deployment Setup Guide

This guide walks you through every step needed to publish WinPrompter to the Microsoft Store via GitHub Actions.

---

## Overview

The release workflow (`.github/workflows/release.yml`) triggers when you push a version tag (e.g. `v1.0.0`). It:

1. Builds MSIX packages for x64 and ARM64
2. Signs them with your certificate
3. Creates a GitHub Release with the `.msix` files attached
4. *(Optional)* Submits to the Microsoft Store automatically

You need to set up **two things**: a code-signing certificate and Microsoft Store API credentials.

---

## Prerequisites

- [GitHub CLI (`gh`)](https://cli.github.com/) installed and authenticated
- [Azure CLI (`az`)](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- A [Microsoft Partner Center](https://partner.microsoft.com/dashboard) account
- Your repo on GitHub (we'll call it `OWNER/winprompter`)

```powershell
# Verify you're authenticated
gh auth status
az account show
```

---

## Step 1 — Create a Code-Signing Certificate

You need a `.pfx` certificate to sign MSIX packages. For Store submission, use a **trusted certificate** from a CA. For testing, a self-signed cert works.

### Option A: Self-Signed (for testing / sideloading)

```powershell
# Generate a self-signed certificate (run as Admin)
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=YourPublisherName" `
  -KeyUsage DigitalSignature `
  -FriendlyName "WinPrompter Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export to .pfx with a password
$password = ConvertTo-SecureString -String "YOUR_CERT_PASSWORD" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "WinPrompter.pfx" -Password $password

# IMPORTANT: Update Package.appxmanifest Publisher to match the Subject
# e.g. Publisher="CN=YourPublisherName"
```

### Option B: Trusted Certificate (for Store submission)

Purchase a code-signing certificate from a CA (DigiCert, Sectigo, etc.) or use the one provided by Partner Center. Export it as `.pfx`.

---

## Step 2 — Store the Certificate as a GitHub Secret

```powershell
# Base64-encode the .pfx file
$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("WinPrompter.pfx"))
$base64 | Set-Content "cert-base64.txt"

# Set the GitHub secret
gh secret set STORE_CERTIFICATE_BASE64 --repo OWNER/winprompter < cert-base64.txt

# Set the certificate password
gh secret set STORE_CERTIFICATE_PASSWORD --repo OWNER/winprompter --body "YOUR_CERT_PASSWORD"

# Clean up — NEVER commit these files
Remove-Item "WinPrompter.pfx", "cert-base64.txt" -ErrorAction SilentlyContinue
```

---

## Step 3 — Set Package Identity Secrets

These values come from your Partner Center app registration.

### 3a. Register Your App in Partner Center

1. Go to https://partner.microsoft.com/dashboard
2. Navigate to **Apps and games** → **New product** → **MSIX or PWA app**
3. Reserve the name **"WinPrompter"**
4. Under **Product Identity**, note down:
   - **Package/Identity/Name** — e.g. `12345YourName.WinPrompter`
   - **Package/Identity/Publisher** — e.g. `CN=ABCDEF12-3456-7890-ABCD-EF1234567890`
   - **Product ID** — the numeric/GUID identifier

### 3b. Set the Secrets

```powershell
# Package identity (from Partner Center -> Product Identity)
gh secret set STORE_PACKAGE_IDENTITY --repo OWNER/winprompter --body "12345YourName.WinPrompter"

# Publisher ID (from Partner Center -> Product Identity)
gh secret set STORE_PUBLISHER_ID --repo OWNER/winprompter --body "CN=ABCDEF12-3456-7890-ABCD-EF1234567890"

# Store Product ID (from Partner Center URL or Product Identity page)
gh secret set STORE_PRODUCT_ID --repo OWNER/winprompter --body "9NXXXXXXXX"
```

---

## Step 4 — Set Up Azure AD App for Store API (Optional)

This is only needed if you want **automatic Store submission** on release. If you prefer to upload manually, skip this step and leave `ENABLE_STORE_SUBMIT` unset.

### 4a. Create an Azure AD App Registration

```powershell
# Create the app registration
az ad app create --display-name "WinPrompter Store Publisher" --sign-in-audience AzureADMyOrg

# Note the appId from the output, then create a secret
$appId = "YOUR_APP_ID_FROM_OUTPUT"
az ad app credential reset --id $appId --display-name "github-actions"

# Note the password and tenant from the output
```

### 4b. Link Azure AD App to Partner Center

1. Go to https://partner.microsoft.com/dashboard
2. Navigate to **Account settings** → **User management** → **Azure AD applications**
3. Click **Add Azure AD application**
4. Search for "WinPrompter Store Publisher" and add it
5. Grant it the **Developer** or **Manager** role

### 4c. Set the Secrets

```powershell
# Azure AD Tenant ID
gh secret set AZURE_TENANT_ID --repo OWNER/winprompter --body "YOUR_TENANT_ID"

# Azure AD App (Client) ID
gh secret set AZURE_CLIENT_ID --repo OWNER/winprompter --body "YOUR_APP_ID"

# Azure AD App Secret
gh secret set AZURE_CLIENT_SECRET --repo OWNER/winprompter --body "YOUR_APP_SECRET"
```

### 4d. Enable Store Submission

Store submission is gated behind a repository variable (not a secret). This lets you easily turn it on/off:

```powershell
# Enable automatic Store submission
gh variable set ENABLE_STORE_SUBMIT --repo OWNER/winprompter --body "true"

# To disable (uploads to GitHub Release only):
gh variable set ENABLE_STORE_SUBMIT --repo OWNER/winprompter --body "false"
```

---

## Step 5 — Verify Your Secrets

```powershell
# List all configured secrets (values are hidden)
gh secret list --repo OWNER/winprompter

# Expected output:
# AZURE_CLIENT_ID          Updated 2026-...
# AZURE_CLIENT_SECRET      Updated 2026-...
# AZURE_TENANT_ID          Updated 2026-...
# STORE_CERTIFICATE_BASE64 Updated 2026-...
# STORE_CERTIFICATE_PASSWORD Updated 2026-...
# STORE_PACKAGE_IDENTITY   Updated 2026-...
# STORE_PRODUCT_ID         Updated 2026-...
# STORE_PUBLISHER_ID       Updated 2026-...

# Check the variable
gh variable list --repo OWNER/winprompter

# Expected:
# ENABLE_STORE_SUBMIT  true  Updated 2026-...
```

---

## Step 6 — Create a Release

```powershell
# Tag and push to trigger the release workflow
git tag v1.0.0
git push origin v1.0.0
```

Or create a release via the GitHub CLI:

```powershell
gh release create v1.0.0 --generate-notes --title "WinPrompter v1.0.0"
```

Then monitor the workflow:

```powershell
# Watch the release workflow
gh run watch --repo OWNER/winprompter

# Or list recent runs
gh run list --repo OWNER/winprompter --workflow release.yml
```

---

## Secrets Reference

| Secret | Required | Description |
|--------|----------|-------------|
| `STORE_CERTIFICATE_BASE64` | ✅ | Base64-encoded `.pfx` signing certificate |
| `STORE_CERTIFICATE_PASSWORD` | ✅ | Password for the `.pfx` file |
| `STORE_PUBLISHER_ID` | ✅ | Publisher identity from Partner Center (e.g. `CN=...`) |
| `STORE_PACKAGE_IDENTITY` | ✅ | Package name from Partner Center |
| `STORE_PRODUCT_ID` | For Store submit | Product ID from Partner Center |
| `AZURE_TENANT_ID` | For Store submit | Azure AD tenant ID |
| `AZURE_CLIENT_ID` | For Store submit | Azure AD app registration client ID |
| `AZURE_CLIENT_SECRET` | For Store submit | Azure AD app registration secret |

| Variable | Required | Description |
|----------|----------|-------------|
| `ENABLE_STORE_SUBMIT` | No | Set to `true` to enable automatic Store submission |

---

## Troubleshooting

### "The publisher CN does not match the signing certificate"
Your `Package.appxmanifest` Publisher must exactly match the certificate Subject. The release workflow patches this automatically from `STORE_PUBLISHER_ID`.

### "No speech language pack installed" (voice feature)
Install a speech language pack: **Settings → Time & Language → Speech → Add languages**. The app uses on-device recognition and requires a downloaded speech model.

### Build fails with "Platform not supported"
Ensure you specify `-p:Platform=x64` or `-p:Platform=arm64`. The project does not support AnyCPU.

### Store submission fails with 401/403
Verify the Azure AD app has the correct Partner Center role and the client secret hasn't expired.
