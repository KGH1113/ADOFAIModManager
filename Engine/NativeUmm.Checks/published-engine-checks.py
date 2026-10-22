"""Exercise the published macOS AOT protocol with an isolated game fixture."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


engine = Path(sys.argv[1]).resolve()
repository = Path(__file__).resolve().parents[2]
checks = 0


def check(condition, description):
    global checks
    if not condition:
        raise AssertionError(description)
    checks += 1


with tempfile.TemporaryDirectory(prefix="amm-aot-settings-", dir="/private/tmp") as temporary:
    root = Path(temporary)
    game = root / "ADanceOfFireAndIce.app"
    managed = game / "Contents/Resources/Data/Managed"
    managed.mkdir(parents=True)
    # Real managed metadata lets status inspection run without launching a game.
    assembly = repository / "Resources/UMMPayload/UnityModManager.dll"
    for name in ["UnityEngine.CoreModule.dll", "Assembly-CSharp.dll"]:
        shutil.copyfile(assembly, managed / name)
    mods = root / "Mods"
    for identifier, version in [
        ("JipperResourcePack", "1.5.2.0"),
        ("KeyboardChatterBlocker", "0.1.2"),
        ("TUFHelper", "3.1.1"),
        ("EnhancedCountdown", "0.2.3"),
    ]:
        directory = mods / identifier
        directory.mkdir(parents=True)
        (directory / "Info.json").write_text(json.dumps({
            "Id": identifier, "DisplayName": identifier, "Version": version,
        }))
    environment = {**os.environ, "ADOFAI_MOD_MANAGER_DATA": str(root / "app-data")}

    def request(action, **arguments):
        result = subprocess.run(
            [str(engine)],
            input=json.dumps({"action": action, "gamePath": str(game), **arguments}),
            text=True, capture_output=True, env=environment, timeout=20,
        )
        response = json.loads(result.stdout)
        if response.get("error") == "game_running":
            raise RuntimeError("Close the game before running the published-engine fixture checks.")
        return response

    def settings(identifier, filename="Settings.json"):
        return json.loads((mods / identifier / filename).read_text())

    check(request("onboardingsetup", presetKind="jrp-defaults")["ok"], "AOT JRP defaults")
    check(request("onboardingsetup", presetKind="chatter-defaults")["ok"], "AOT chatter defaults")
    loaded = request("jrpkeys")["keyBindings"]
    check(loaded["style"] == 10 and {int(key): len(value) for key, value in loaded["layouts"].items()}
          == {10: 10, 12: 12, 16: 16, 20: 20}, "AOT active style and all layouts read")
    for count, native_style in [(10, 3), (12, 0), (16, 1), (20, 2)]:
        before = request("jrpkeys")["keyBindings"]["layouts"]
        edited = list(range(97, 97 + count))
        if count == 20:
            edited[16] = edited[12]
        saved = request("savejrpkeys", keyBindings=edited, keyViewerStyle=count)
        expected = {**before, str(count): edited}
        check(saved.get("keyBindings") == {"style": count, "layouts": expected},
              f"AOT Key{count} save and unrelated layout preservation")
        viewer = settings("JipperResourcePack")["Feature"]["KeyViewer"]["Setting"]
        check(viewer["KeyViewerStyle"] == native_style and viewer[f"key{count}"] == edited,
              f"AOT Key{count} native style mapping")
        chatter = settings("KeyboardChatterBlocker", "Setting.json")
        profile = next(profile for profile in chatter["keyLimiterProfiles"] if profile["isSelected"])
        check(chatter["inputInterval"] == 35 and chatter["enableKeyLimiter"]
              and profile["allowedKeys"] == list(dict.fromkeys(edited)),
              f"AOT Key{count} limiter synchronization and free-key deduplication")
        for invalid in [[97] * count, edited[:-1]]:
            rejected = request("savejrpkeys", keyBindings=invalid, keyViewerStyle=count)
            check(not rejected["ok"], f"AOT invalid Key{count} binding rejection")
            check(request("jrpkeys")["keyBindings"] == {"style": count, "layouts": expected},
                  f"AOT Key{count} rejection preserves bindings")
    check(not request("savejrpkeys", keyBindings=edited, keyViewerStyle=11)["ok"],
          "AOT unsupported style rejection")
    check(request("onboardingsetup", presetKind="tuf-overlay")["ok"], "AOT TUF defaults")
    tuf = settings("TUFHelper")
    check(tuf["ShowTUFHelperOverlayer"] and tuf["IngameElementsSettings"]["Leaderboard"]["IsShowed"]
          and not tuf["IngameElementsSettings"]["Combo"]["IsShowed"], "AOT TUF overlay reconciliation")
    check(request("onboardingsetup", presetKind="countdown-defaults")["ok"], "AOT Countdown defaults")
    check(settings("EnhancedCountdown", "UpdateSettings.json")["ReceiveBetaUpdates"], "AOT beta update preference")
    check((mods / "JipperResourcePack/Settings.json.amm-backup").is_file(), "AOT settings backup")

print(f"{checks} published macOS AOT settings protocol checks passed.")
