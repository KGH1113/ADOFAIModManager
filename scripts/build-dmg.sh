#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app="$project_root/build/ADOFAI Mod Manager.app"
app_icon="$app/Contents/Resources/AppIcon.icns"
icon_master="$project_root/Resources/AppIconMaster.png"
dist="$project_root/dist"
dmg="$dist/ADOFAI-Mod-Manager-0.1.0.dmg"
volume_name="ADOFAI Mod Manager"
tool_root="$project_root/.build-tools/dmgbuild"
tool="$tool_root/bin/dmgbuild"
background="$project_root/build/generated-assets/dmg-background.png"

detach_stale_images() {
    local device
    while IFS= read -r device; do
        [[ -z "$device" ]] && continue
        hdiutil detach "$device" >/dev/null 2>&1 \
            || hdiutil detach -force "$device" >/dev/null 2>&1 \
            || true
    done < <(hdiutil info -plist | python3 -c '
import plistlib, sys

volume = sys.argv[1]
info = plistlib.loads(sys.stdin.buffer.read())
seen = set()
for image in info.get("images", []):
    for entity in image.get("system-entities", []):
        mount = entity.get("mount-point", "")
        device = entity.get("dev-entry", "")
        if mount == f"/Volumes/{volume}" or mount.startswith(f"/Volumes/{volume} "):
            if device and device not in seen:
                seen.add(device)
                print(device)
' "$volume_name")
}

# dmgbuild/hdiutil can leave a writable image mounted after an interrupted run.
# A stale mount with the same volume name makes the next create fail with
# "Device not configured", so clean it before and after every build.
detach_stale_images
trap detach_stale_images EXIT

if [[ ! -d "$app" || ! -f "$app_icon" || "$icon_master" -nt "$app_icon" ]]; then
    "$project_root/scripts/build-app.sh"
fi

if [[ -x "$tool" ]]; then
    expected_shebang="#!$tool_root/bin/python"
    actual_shebang="$(head -n 1 "$tool")"
    if [[ "$actual_shebang" != "$expected_shebang" ]]; then
        print "dmgbuild environment moved; rebuilding it."
        rm -rf "$tool_root"
    fi
fi

if [[ ! -x "$tool" ]]; then
    python3 -m venv "$tool_root"
    "$tool_root/bin/python" -m pip install \
        --disable-pip-version-check \
        --no-input \
        "dmgbuild==1.6.7"
fi

mkdir -p "$dist"
rm -f "$dmg" "$project_root/build/ADOFAI-Mod-Manager-writable.dmg"
"$tool" \
    -s "$project_root/scripts/dmg-settings.py" \
    -D "app=$app" \
    -D "background=$background" \
    "$volume_name" \
    "$dmg"

codesign --force --sign - "$dmg"
print "$dmg"
