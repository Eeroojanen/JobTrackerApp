# JobTracker

## English

### Build and run (developer)
```bash
dotnet run
```

### Portable zip distribution (no installer)
1. Publish the app (single-file, Windows x64):

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

2. Zip the publish folder and share it:

```
bin\Release\net8.0\win-x64\publish\
```

3. Unzip and run `JobTracker.exe` directly.

### Create a Desktop shortcut
After unzipping, run this script once:

```powershell
./scripts/create-desktop-shortcut.ps1
```

It will create a **JobTracker** shortcut on the Desktop.

### How to use the application
1. Enter a company name and choose a status.
2. Click **Add company** to add it to the list.
3. Click **Remove chosen** to delete the selected row.
4. Click **Clear all** to remove everything (confirmation required).
5. Use **Search company** to filter the list.
6. Use **Import** to load from Excel (set column headers and status labels first).

---

## Suomi

### Käännä ja aja (kehittäjälle)
```bash
dotnet run
```

### Kannettava zip-jakelu (ei asenninta)
1. Julkaise sovellus (yksi tiedosto, Windows x64):

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

2. Pakkaa julkaistu kansio zip-tiedostoksi ja jaa se:

```
bin\Release\net8.0\win-x64\publish\
```

3. Pura zip ja käynnistä `JobTracker.exe`.

### Työpöytäkuvake
Pura zip ja aja tämä skripti kerran:

```powershell
./scripts/create-desktop-shortcut.ps1
```

Skripti luo **JobTracker**-pikakuvakkeen työpöydälle.

### Sovelluksen käyttö
1. Syötä yrityksen nimi ja valitse status.
2. Klikkaa **Add company** lisätäksesi rivin listaan.
3. Klikkaa **Remove chosen** poistaaksesi valitun rivin.
4. Klikkaa **Clear all** tyhjentääksesi listan (vahvistus vaaditaan).
5. **Search company** suodattaa listaa hakusanalla.
6. **Import** tuo Excelistä (aseta sarakeotsikot ja status‑nimet ensin).