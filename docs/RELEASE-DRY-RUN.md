# Building and verifying a release locally

Gungun is a local program: the database, settings and key live under `%LOCALAPPDATA%\Gungun`, and
nothing in the app talks to a server. A release is therefore a local build you run yourself — there
is no cloud account, no CI signing service and no upload step in this procedure.

## 1. Build the debug app

```bash
dotnet build Gungun/Gungun.csproj -c Debug
Gungun/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Gungun.exe
```

Check, in this order:

1. The app starts hidden (no window, no taskbar entry).
2. `Alt + N` opens the workspace and `Esc` collapses it.
3. Customize → General shows **Database: Encrypted — SQLCipher 256-bit** and the data folder opens
   at `%LOCALAPPDATA%\Gungun`.
4. Adding a task, closing the app and reopening it keeps the task.
5. With no calendar linked, the Events card shows its empty state and the app makes no network
   request. Linking a calendar is the only action that leaves the machine.

## 2. Build and sign the MSIX

```powershell
.\scripts\package-msix.ps1
```

This builds `dist\Gungun-<version>-x64.msix` — one self-contained package containing the .NET
runtime and the Windows App SDK — signs it, verifies the signature and prints its SHA-256. The
certificate is a local one (see [`SIGNING.md`](SIGNING.md)); no signing service account is involved.
For a self-signed certificate, trust it machine-wide first, or `Add-AppxPackage` fails with
`0x800B0109`:

```powershell
Import-Certificate -FilePath .\Gungun.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

## 3. Install it like a user would

```powershell
Add-AppxPackage -Path .\dist\Gungun-1.0.3-x64.msix
Start-Process explorer.exe "shell:AppsFolder\$((Get-AppxPackage Gungun.Desktop).PackageFamilyName)!Gungun"
```

Check, in this order:

1. The app starts hidden — no window, no taskbar entry.
2. `Gungun.lnk` appears on the desktop on that first launch, with the package icon, and starting it
   opens the workspace.
3. Gungun is listed in the Start menu.
4. `Alt + N` opens the workspace and `Esc` collapses it.
5. Data still lives in `%LOCALAPPDATA%\Gungun`; a first launch has no database until the first
   task or note is added.

```powershell
Get-AppxPackage Gungun.Desktop | Remove-AppxPackage     # clean up the test install
```

The database is created on first write, not at startup. An empty `%LOCALAPPDATA%\Gungun` after a
first launch is expected; `gungun.db` and `db.key` appear together after the first task or note.

## 4. Confirm the offline claim

With the app running, the only connections it may open are the calendar provider's. To check:

```powershell
Get-NetTCPConnection -OwningProcess (Get-Process Gungun).Id -ErrorAction SilentlyContinue |
  Select-Object RemoteAddress, RemotePort, State
```

Expect no rows until you link a calendar. After linking, expect the Google or Microsoft endpoints
only; the app does not call the marketing site, GitHub, or any update feed.

## 5. What to keep out of the repository

- `*.pfx`, `*.cer`, `*.pvk` — already covered by `.gitignore`.
- `%LOCALAPPDATA%\Gungun\db.key` — the user's key, never part of a build.
- The encrypted database itself — personal data, not an artifact.
