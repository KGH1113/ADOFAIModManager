#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app="$project_root/build/ADOFAI Mod Manager.app"
helper="$app/Contents/Helpers/UMMInstallerEngine"
payload="$app/Contents/Resources/UMMPayload"
source_managed="${1:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}"

if [[ ! -x "$helper" ]]; then
    print -u2 "Build the app before running this test."
    exit 1
fi
if [[ ! -f "$source_managed/UnityEngine.CoreModule.dll" || ! -f "$source_managed/Assembly-CSharp.dll" ]]; then
    print "ADOFAI assemblies not found; full UMM install test skipped."
    exit 0
fi

fixture_root="$(mktemp -d /private/tmp/adofai-umm-install-test.XXXXXX)"
game_app="$fixture_root/ADanceOfFireAndIce.app"
managed="$game_app/Contents/Resources/Data/Managed"
cleanup() { rm -rf "$fixture_root" }
trap cleanup EXIT

mkdir -p "$managed"
cp "$source_managed/UnityEngine.CoreModule.dll" "$managed/"
cp "$source_managed/Assembly-CSharp.dll" "$managed/"

request="{\"protocolVersion\":1,\"action\":\"install\",\"gamePath\":\"$game_app\",\"payloadDir\":\"$payload\",\"forcePayload\":true}"
response="$(printf '%s' "$request" | "$helper")"

[[ "$response" == *'"ok":true'* ]]
[[ "$response" == *'"hookInstalled":true'* ]]
cmp "$payload/0Harmony.dll" "$managed/UnityModManager/0Harmony.dll"
if strings "$managed/UnityModManager/0Harmony.dll" | grep -q 'System.Runtime, Version=5'; then
    print -u2 "The installed Harmony payload targets .NET 5 instead of Unity Mono."
    exit 1
fi

print "Full UMM install test passed with the bundled Unity Mono Harmony payload."
