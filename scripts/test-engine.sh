#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
fixture_root="$(mktemp -d /private/tmp/adofai-engine-test.XXXXXX)"
game_app="$fixture_root/ADanceOfFireAndIce.app"
managed="$game_app/Contents/Resources/Data/Managed"
request_prefix="{\"protocolVersion\":1,\"gamePath\":\"$game_app\""

cleanup() { rm -rf "$fixture_root" }
trap cleanup EXIT

mkdir -p "$managed" "$managed/UnityModManager"
touch "$managed/UnityEngine.CoreModule.dll" "$managed/Assembly-CSharp.dll"
ditto -c -k --sequesterRsrc --keepParent \
    "$project_root/Tests/Fixtures/SampleMod" "$fixture_root/SampleMod.zip"

dotnet build "$project_root/Engine/UMMInstallerEngine.csproj" -c Debug >/dev/null

inspect="$(printf '%s' "$request_prefix,\"action\":\"inspectmod\",\"zipPath\":\"$fixture_root/SampleMod.zip\"}" \
    | dotnet run --project "$project_root/Engine/UMMInstallerEngine.csproj" -c Debug --no-build)"
[[ "$inspect" == *'"name":"Codex Sample Mod"'* ]]
[[ "$inspect" == *'"alreadyInstalled":false'* ]]

install="$(printf '%s' "$request_prefix,\"action\":\"installmod\",\"zipPath\":\"$fixture_root/SampleMod.zip\"}" \
    | dotnet run --project "$project_root/Engine/UMMInstallerEngine.csproj" -c Debug --no-build)"
[[ "$install" == *'"id":"CodexSampleMod"'* ]]
[[ -f "$fixture_root/Mods/CodexSampleMod/Info.json" ]]

disable="$(printf '%s' "$request_prefix,\"action\":\"setmodenabled\",\"modId\":\"CodexSampleMod\",\"enabled\":false}" \
    | dotnet run --project "$project_root/Engine/UMMInstallerEngine.csproj" -c Debug --no-build)"
[[ "$disable" == *'"enabled":false'* ]]
[[ -f "$managed/UnityModManager/Params.xml" ]]

remove="$(printf '%s' "$request_prefix,\"action\":\"removemod\",\"path\":\"$fixture_root/Mods/CodexSampleMod\"}" \
    | dotnet run --project "$project_root/Engine/UMMInstallerEngine.csproj" -c Debug --no-build)"
[[ "$remove" != *'"id":"CodexSampleMod"'* ]]
[[ ! -d "$fixture_root/Mods/CodexSampleMod" ]]

print "Engine import, toggle, and permanent removal integration test passed."
