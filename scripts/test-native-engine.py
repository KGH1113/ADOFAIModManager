"""Exercise the bundled Native AOT JSON protocol without touching a real game."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

engine = Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix="amm-native-check-") as temporary:
    root = Path(temporary).resolve()
    if sys.platform == "darwin":
        game = root / "ADanceOfFireAndIce.app"
        managed = game / "Contents/Resources/Data/Managed"
    else:
        game = root / "game"
        game.mkdir()
        executable = "A Dance of Fire and Ice.exe" if sys.platform == "win32" else "ADanceOfFireAndIce.x86_64"
        (game / executable).touch()
        data = "A Dance of Fire and Ice_Data" if sys.platform == "win32" else "ADanceOfFireAndIce_Data"
        managed = game / data / "Managed"
    managed.mkdir(parents=True)
    (managed / "UnityEngine.CoreModule.dll").touch()
    (managed / "Assembly-CSharp.dll").touch()
    mods = root / "Mods" if sys.platform == "darwin" else game / "Mods"
    mod = mods / "AdofaiTweaks"
    mod.mkdir(parents=True)
    (mod / "Info.json").write_text(json.dumps({"Id": "AdofaiTweaks", "DisplayName": "ADOFAI Tweaks", "Version": "2.9.3"}))

    def request(action, **extra):
        env = {**os.environ, "ADOFAI_MOD_MANAGER_DATA": str(root / "app-data")}
        result = subprocess.run([str(engine)], input=json.dumps({"protocolVersion": 1, "gamePath": str(game), "action": action, **extra}), text=True, capture_output=True, check=True, env=env)
        response = json.loads(result.stdout)
        assert response.get("ok"), response
        return response

    status = request("status")
    assert status["detected"]
    original = "<DisableEffectsSettings><Preserved>custom</Preserved></DisableEffectsSettings>"
    settings = mod / "DisableEffectsSettings.xml"
    settings.write_text(original)
    saved = request("savepreset", modId="AdofaiTweaks", presetKind="tweaks", presetValues=[True, False, True])
    assert saved["preset"] == [True, False, True], saved
    assert request("preset", modId="AdofaiTweaks", presetKind="tweaks")["preset"] == [True, False, True]
    assert "<Preserved>custom</Preserved>" in settings.read_text()
    assert Path(str(settings) + ".amm-backup").read_text() == original
    for mod_id, version in [("JipperResourcePack", "1.5.2.0"), ("KeyboardChatterBlocker", "0.1.2"), ("TUFHelper", "3.1.1")]:
        directory = mods / mod_id
        directory.mkdir(parents=True)
        (directory / "Info.json").write_text(json.dumps({"Id": mod_id, "DisplayName": mod_id, "Version": version}))
    request("onboardingsetup", presetKind="jrp-key-sync")
    jrp_settings = mods / "JipperResourcePack/Settings.json"
    assert json.loads(jrp_settings.read_text())["Feature"]["KeyViewer"]["Setting"]["AutoSetupKeyLimit"] is True
    request("onboardingsetup", presetKind="tuf-overlay")
    tuf_settings = mods / "TUFHelper/Settings.json"
    assert json.loads(tuf_settings.read_text())["ShowTUFHelperOverlayer"] is False
    missing = subprocess.run([str(engine)], input=json.dumps({"protocolVersion": 1, "gamePath": str(root / "Missing.app"), "action": "status"}), text=True, capture_output=True, check=True)
    assert not json.loads(missing.stdout)["detected"], "An explicit missing game must not fall back to Steam."
    archive = root / "sample.zip"
    with zipfile.ZipFile(archive, "w") as output:
        output.writestr("Sample/Info.json", json.dumps({"Id": "Sample", "DisplayName": "Sample", "Version": "1.0.0"}))
    request("inspectmod", zipPath=str(archive))
    request("installmod", zipPath=str(archive))
    assert (mods / "Sample/Info.json").is_file()
    request("setmodenabled", modId="Sample", enabled=False)
    assert (managed / "UnityModManager/Params.xml").is_file()
    request("removemod", path=str(mods / "Sample"))
    assert not (mods / "Sample").exists()
print("Native AOT status, presets, onboarding, ZIP import, toggle, removal, backup and explicit-path checks passed.")
