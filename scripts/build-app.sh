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

# Harmony 2.4 adds ARM support, but its NuGet package contains builds for many
# runtimes. Unity Mono needs the mscorlib/net48 build, never the net5.0 build.
nuget_root="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
harmony="$nuget_root/lib.harmony/2.4.2/lib/net48/0Harmony.dll"
if [[ ! -f "$harmony" ]]; then
    print -u2 "Harmony 2.4.2 net48 payload was not restored."
    exit 1
fi
cp "$harmony" "$contents/Resources/UMMPayload/0Harmony.dll"

codesign --force --sign - --options runtime "$contents/Helpers/UMMInstallerEngine"
codesign --force --sign - --options runtime "$app"

print "$app"
file "$contents/MacOS/ADOFAIModManager"
file "$contents/Helpers/UMMInstallerEngine"
