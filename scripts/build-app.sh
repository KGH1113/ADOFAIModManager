#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
engine_project="$project_root/Engine/NativeUmm.Cli/NativeUmm.Cli.csproj"
build_root="$project_root/build"
app="$build_root/ADOFAI Mod Manager.app"
contents="$app/Contents"
assets="$build_root/generated-assets"

rm -rf "$app" "$assets" "$build_root/engine-arm64" "$build_root/engine-x64"
mkdir -p "$contents/MacOS" "$contents/Helpers" "$contents/Resources/UMMPayload" "$contents/Resources/ThirdPartyNotices"

swift "$project_root/scripts/generate-assets.swift" "$assets"
cp "$assets/AppIcon.icns" "$contents/Resources/AppIcon.icns"

dotnet restore "$engine_project"
dotnet publish "$engine_project" -c Release -r osx-arm64 -o "$build_root/engine-arm64"
dotnet publish "$engine_project" -c Release -r osx-x64 -o "$build_root/engine-x64"
lipo -create "$build_root/engine-arm64/UMMInstallerEngine" "$build_root/engine-x64/UMMInstallerEngine" -output "$contents/Helpers/UMMInstallerEngine"
chmod 755 "$contents/Helpers/UMMInstallerEngine"

swift build --disable-sandbox --package-path "$project_root" -c release --arch arm64 --arch x86_64
swift_bin="$(swift build --disable-sandbox --package-path "$project_root" -c release --arch arm64 --arch x86_64 --show-bin-path)"
cp "$swift_bin/ADOFAIModManager" "$contents/MacOS/ADOFAIModManager"
cp -R "$swift_bin/ADOFAIModManager_ADOFAIModManager.bundle" "$contents/Resources/"
cp "$project_root/Resources/Info.plist" "$contents/Info.plist"
cp -R "$project_root/Resources/ThirdPartyNotices/." "$contents/Resources/ThirdPartyNotices/"
cp "$project_root/Resources/UMMPayload/"* "$contents/Resources/UMMPayload/"

# This is Harmony 2.3.6 with MonoMod.Core 1.3.3 merged into the net48 build.
# It preserves the internal 2.3.6 ABI used by JALib while providing the ARM64
# detour backend required by native Apple Silicon Unity Mono.
harmony="$contents/Resources/UMMPayload/0Harmony.dll"
has_harmony_marker() { strings "$harmony" | grep "$1" >/dev/null }
if ! has_harmony_marker 'MonoMod.Core, Version=1.3.3.0' \
    || ! has_harmony_marker 'MethodPatcher' \
    || ! has_harmony_marker 'Arm64Arch' \
    || ! has_harmony_marker 'exhelper_macos_arm64.dylib'; then
    print -u2 "The bundled Harmony payload is not the 2.3.6 ABI-compatible ARM64 build."
    exit 1
fi

codesign --force --sign - --options runtime "$contents/Helpers/UMMInstallerEngine"
codesign --force --sign - --options runtime "$app"

print "$app"
file "$contents/MacOS/ADOFAIModManager"
file "$contents/Helpers/UMMInstallerEngine"
