#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app="$project_root/build/ADOFAI Mod Manager.app"
dist="$project_root/dist"
dmg="$dist/ADOFAI-Mod-Manager-0.1.0.dmg"
tool_root="$project_root/.build-tools/dmgbuild"
tool="$tool_root/bin/dmgbuild"
background="$project_root/build/generated-assets/dmg-background.png"

if [[ ! -d "$app" ]]; then
    "$project_root/scripts/build-app.sh"
fi

if [[ ! -x "$tool" ]]; then
    python3 -m venv "$tool_root"
    "$tool_root/bin/python" -m pip install \
        --disable-pip-version-check \
        --no-input \
        "dmgbuild==1.6.7"
fi

mkdir -p "$dist"
rm -f "$dmg"
"$tool" \
    -s "$project_root/scripts/dmg-settings.py" \
    -D "app=$app" \
    -D "background=$background" \
    "ADOFAI Mod Manager" \
    "$dmg"

codesign --force --sign - "$dmg"
print "$dmg"
