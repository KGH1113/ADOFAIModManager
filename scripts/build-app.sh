#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
build_root="$project_root/build"
app="$build_root/ADOFAI Mod Manager.app"
contents="$app/Contents"
assets="$build_root/generated-assets"

rm -rf "$build_root"
mkdir -p "$contents/MacOS" "$contents/Helpers" "$contents/Resources/Harmony" "$contents/Resources/ThirdPartyNotices"

swift "$project_root/scripts/generate-assets.swift" "$assets"
cp "$assets/AppIcon.icns" "$contents/Resources/AppIcon.icns"

dotnet restore "$project_root/Engine/UMMInstallerEngine.csproj"
dotnet publish "$project_root/Engine/UMMInstallerEngine.csproj" -c Release -r osx-arm64 -o "$build_root/engine-arm64"
dotnet publish "$project_root/Engine/UMMInstallerEngine.csproj" -c Release -r osx-x64 -o "$build_root/engine-x64"
lipo -create "$build_root/engine-arm64/UMMInstallerEngine" "$build_root/engine-x64/UMMInstallerEngine" -output "$contents/Helpers/UMMInstallerEngine"
chmod 755 "$contents/Helpers/UMMInstallerEngine"

swift build --disable-sandbox --package-path "$project_root" -c release --arch arm64 --arch x86_64
swift_bin="$(swift build --disable-sandbox --package-path "$project_root" -c release --arch arm64 --arch x86_64 --show-bin-path)"
cp "$swift_bin/ADOFAIModManager" "$contents/MacOS/ADOFAIModManager"
cp -R "$swift_bin/ADOFAIModManager_ADOFAIModManager.bundle" "$contents/Resources/"
cp "$project_root/Resources/Info.plist" "$contents/Info.plist"
cp -R "$project_root/Resources/ThirdPartyNotices/." "$contents/Resources/ThirdPartyNotices/"

nuget_root="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
harmony="$(find "$nuget_root/lib.harmony/2.4.2" -type f -name 0Harmony.dll | head -1)"
if [[ -z "$harmony" ]]; then
    print -u2 "Harmony 2.4.2 was not restored."
    exit 1
fi
cp "$harmony" "$contents/Resources/Harmony/0Harmony.dll"

codesign --force --sign - --options runtime "$contents/Helpers/UMMInstallerEngine"
codesign --force --sign - --options runtime "$app"

print "$app"
file "$contents/MacOS/ADOFAIModManager"
file "$contents/Helpers/UMMInstallerEngine"
