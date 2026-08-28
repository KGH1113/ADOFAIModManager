#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
harmony_tag="v2.3.6.0"
monomod_version="1.3.3"
work_root="$(mktemp -d /private/tmp/adofai-harmony-build.XXXXXX)"
harmony_source="$work_root/Harmony"
output="$project_root/Resources/UMMPayload/0Harmony.dll"

cleanup() { rm -rf "$work_root" }
trap cleanup EXIT

cd "$project_root"

git clone --branch "$harmony_tag" --depth 1 \
    https://github.com/pardeike/Harmony.git "$harmony_source"

# Harmony 2.3.6 includes its local packages directory as an additional restore
# source. It must exist even though all dependencies are restored from NuGet.
mkdir -p "$harmony_source/packages"

dotnet build "$harmony_source/Harmony/Harmony.csproj" \
    -c ReleaseFat \
    -f net48 \
    --nologo \
    -p:MonoModCoreVersion="$monomod_version"

artifact="$harmony_source/Harmony/bin/ReleaseFat/net48/0Harmony.dll"
if [[ ! -f "$artifact" ]]; then
    print -u2 "Harmony compatibility build did not produce $artifact"
    exit 1
fi

for marker in \
    '2.3.6.0' \
    'MethodPatcher' \
    'MonoMod.Core, Version=1.3.3.0' \
    'Arm64Arch' \
    'exhelper_macos_arm64.dylib'; do
    if ! strings "$artifact" | grep "$marker" >/dev/null; then
        print -u2 "Harmony compatibility build is missing marker: $marker"
        exit 1
    fi
done

cp "$artifact" "$output"
print "Updated $output from Harmony $harmony_tag with MonoMod.Core $monomod_version."
