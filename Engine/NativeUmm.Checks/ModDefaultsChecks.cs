using System.Text.Json.Nodes;
using NativeUmm.Domain.Mods;
using NativeUmm.Infrastructure.Presets;

internal static class ModDefaultsChecks
{
    public static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        var directory = Path.Combine(root, "recommended-settings");
        ModInfo Mod(string id, string version)
        {
            var path = Path.Combine(directory, id);
            Directory.CreateDirectory(path);
            return new(id, id, version, null, null, "installed", path, true, true, []);
        }
        var setup = new OnboardingSetupService();
        var jrp = Mod("JipperResourcePack", "1.5.2.0");
        var kcb = Mod("KeyboardChatterBlocker", "0.1.2");
        var tuf = Mod("TUFHelper", "3.1.1");
        var countdown = Mod("EnhancedCountdown", "0.2.0");
        var jrpPath = Path.Combine(jrp.Path, "Settings.json");
        var kcbPath = Path.Combine(kcb.Path, "Setting.json");
        var tufPath = Path.Combine(tuf.Path, "Settings.json");
        JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(path))!;

        setup.Apply([jrp], "jrp-defaults", "ko-KR");
        var json = Read(jrpPath);
        var viewer = json["Feature"]!["KeyViewer"]!["Setting"]!;
        check(viewer["KeyViewerStyle"]!.GetValue<int>() == 3 && viewer["FootKeyViewerStyle"]!.GetValue<int>() == 0, "Fresh JRP defaults use Key10 and no foot keys");
        check(viewer["useGhostRain"]!.GetValue<bool>() && viewer["useRain"]!.GetValue<bool>(), "Both rain modes are enabled");
        check(json["Feature"]!["Status"]!["Setting"]!["TimeTextType"]!.GetValue<int>() == 0, "Use Korean music time for a Korean PC");
        check(!json["Feature"]!["Combo"]!["Setting"]!["EnableAutoCombo"]!.GetValue<bool>(), "Disable autoplay combo");
        check(!json["Feature"]!["ResourceChanger"]!["Setting"]!["ChangeBallColor"]!.GetValue<bool>(), "Disable planet resource changes");
        check(json["Feature"]!["BPM"]!["Enabled"]!.GetValue<bool>(), "Enable uppercase BPM feature");
        setup.Apply([jrp], "jrp-defaults", "ja-JP");
        check(Read(jrpPath)["Feature"]!["Status"]!["Setting"]!["TimeTextType"]!.GetValue<int>() == 1, "Use English for other PC languages");

        const string custom = """{"Custom":42,"Feature":{"KeyViewer":{"Setting":{"KeyViewerStyle":1,"FootKeyViewerStyle":3,"key10":[97,98,99,100,101,102,103,104,105,106],"key10Text":["custom"],"GhostKey10":[7,8]}}}}""";
        File.Delete(jrpPath + ".amm-backup");
        File.WriteAllText(jrpPath, custom);
        setup.Apply([jrp], "jrp-defaults", "en-US");
        json = Read(jrpPath);
        viewer = json["Feature"]!["KeyViewer"]!["Setting"]!;
        check(viewer["KeyViewerStyle"]!.GetValue<int>() == 1 && viewer["FootKeyViewerStyle"]!.GetValue<int>() == 3, "Preserve personalized styles");
        check(viewer["key10"]![0]!.GetValue<int>() == 97 && viewer["key10Text"]![0]!.GetValue<string>() == "custom", "Preserve personalized bindings and text");
        var edited = new[] { 113, 119, 101, 114, 117, 105, 111, 112, 32, 44 };
        check(setup.LoadKeys([jrp]).Style == 16 && setup.LoadKeys([jrp]).Layouts.Keys.Order().SequenceEqual(new[] { 4, 8, 10, 12, 16 }), "Read the active native style and all five independent layouts");
        foreach (var (count, nativeStyle) in new[] { (4, 4), (8, 5), (10, 3), (12, 0), (16, 1) })
        {
            var bindings = Enumerable.Range(97, count).ToArray();
            var expected = Read(jrpPath);
            var expectedViewer = expected["Feature"]!["KeyViewer"]!["Setting"]!;
            expectedViewer["KeyViewerStyle"] = nativeStyle;
            expectedViewer[$"key{count}"] = new JsonArray(bindings.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray());
            var saved = setup.SaveKeys([jrp], bindings, count);
            check(saved.Style == count && saved.Layouts[count].SequenceEqual(bindings), $"Key{count} bindings round trip with the native enum");
            check(JsonNode.DeepEquals(Read(jrpPath), expected), $"Key{count} saves preserve all other settings and layouts");
        }
        setup.SaveKeys([jrp], edited, 10);
        check(Read(jrpPath)["Feature"]!["KeyViewer"]!["Setting"]!["GhostKey10"]!.ToJsonString() == "[7,8]", "Key edits never touch ghost bindings");
        check(File.ReadAllText(jrpPath + ".amm-backup") == custom, "Retain first backup across multiple defaults and key saves");
        var original = File.ReadAllText(jrpPath);
        foreach (var count in new[] { 4, 8, 10, 12, 16 })
        {
            reject(() => setup.SaveKeys([jrp], [1, 2], count), $"Reject truncated Key{count} arrays");
            reject(() => setup.SaveKeys([jrp], Enumerable.Repeat(97, count).ToArray(), count), $"Reject duplicate guided Key{count} bindings");
        }
        reject(() => setup.SaveKeys([jrp], edited, 11), "Reject an unsupported key viewer style");
        reject(() => setup.SaveKeys([jrp], Enumerable.Range(97, 20).ToArray(), 20), "Key20 is no longer selectable");
        reject(() => setup.SaveKeys([jrp], [.. edited[..9], 0x11000], 10), "Reject out-of-range bindings");
        reject(() => setup.SaveKeys([jrp with { Version = "2.0.0" }], edited, 10), "Reject an unverified JRP schema");
        check(File.ReadAllText(jrpPath) == original, "Rejected key edits keep the file intact");
        var malformed = Read(jrpPath);
        malformed["Feature"]!["KeyViewer"]!["Setting"]!["key12"] = new JsonArray(1, 2);
        File.WriteAllText(jrpPath, malformed.ToJsonString());
        reject(() => setup.SaveKeys([jrp], edited, 10), "Reject malformed unrelated layouts before saving");
        check(JsonNode.DeepEquals(Read(jrpPath), malformed), "A malformed layout leaves the settings file intact");
        File.WriteAllText(jrpPath, original);

        var legacy = Read(jrpPath);
        var legacyViewer = legacy["Feature"]!["KeyViewer"]!["Setting"]!;
        legacyViewer["KeyViewerStyle"] = 2;
        legacyViewer["key20"] = new JsonArray(Enumerable.Range(97, 20).Select(k => (JsonNode)JsonValue.Create(k)!).ToArray());
        File.WriteAllText(jrpPath, legacy.ToJsonString());
        check(setup.LoadKeys([jrp]).Style == 16, "Open legacy Key20 settings with Key16 selected");
        check(JsonNode.DeepEquals(Read(jrpPath), legacy), "Loading legacy Key20 does not mutate settings");
        setup.SaveKeys([jrp], edited, 10);
        check(JsonNode.DeepEquals(Read(jrpPath)["Feature"]!["KeyViewer"]!["Setting"]!["key20"], legacyViewer["key20"]), "Explicit saves preserve legacy Key20 bindings");
        File.WriteAllText(jrpPath, original);

        File.WriteAllText(Path.Combine(kcb.Path, "Setting.xml"), """<Setting><inputInterval>100</inputInterval><lang>Korean</lang><ignoredKeys><KeyCode>A</KeyCode></ignoredKeys><ignoredAsyncKeys/><keyLimiterProfiles><KeyLimiterProfile><name>Personal</name><isSelected>true</isSelected><allowedKeys><KeyCode>Space</KeyCode></allowedKeys><allowedAsyncKeys><unsignedShort>32</unsignedShort></allowedAsyncKeys></KeyLimiterProfile></keyLimiterProfiles></Setting>""");
        setup.Apply([kcb], "chatter-defaults");
        json = Read(kcbPath);
        check(json["inputInterval"]!.GetValue<int>() == 35 && json["lang"]!.GetValue<string>() == "Korean", "Migrate XML and set threshold while keeping language");
        check(json["ignoredKeys"]![0]!.GetValue<string>() == "A" && json["ignoredAsyncKeys"]!.AsArray().Count == 0, "Preserve ignored keys and empty XML arrays");
        check(!File.Exists(Path.Combine(kcb.Path, "Settings.json")), "Use singular KCB filename");
        setup.Apply([jrp, kcb], "chatter-defaults");
        json = Read(kcbPath);
        check(json["enableKeyLimiter"]!.GetValue<bool>(), "Enable KCB key limiting when JRP is active");
        var profiles = json["keyLimiterProfiles"]!.AsArray();
        check(profiles.Count == 2 && !profiles[0]!["isSelected"]!.GetValue<bool>(), "Retain personal profiles and select JRP");
        check(profiles[1]!["allowedKeys"]!.AsArray().Any(k => k!.GetValue<int>() == 113), "JRP binding edits feed the limiter");
        foreach (var count in new[] { 4, 8, 10, 12, 16 })
        {
            var bindings = setup.LoadKeys([jrp]).Layouts[count];
            bindings[0] = 500 + count;
            var saved = setup.SaveKeys([jrp, kcb], bindings, count);
            check(saved.Style == count, $"Select Key{count} while chatter is installed");
            var allowed = Read(kcbPath)["keyLimiterProfiles"]![1]!["allowedKeys"]!.AsArray().Select(k => k!.GetValue<int>());
            var expectedAllowed = bindings.Concat(new[] { 289, 284, 288, 283, 287, 282 }).Distinct();
            check(allowed.SequenceEqual(expectedAllowed), $"Key{count} updates the limiter with hand and foot keys, removing duplicates");
        }
        var goodChatter = File.ReadAllText(kcbPath);
        original = File.ReadAllText(jrpPath);
        File.WriteAllText(kcbPath, """{"keyLimiterProfiles":[null]}""");
        reject(() => setup.SaveKeys([jrp, kcb], edited, 10), "Reject invalid limiter profiles before saving bindings");
        check(File.ReadAllText(jrpPath) == original, "A malformed limiter leaves JRP bindings unchanged");
        File.WriteAllText(kcbPath, goodChatter);

        setup.Apply([tuf], "tuf-overlay");
        json = Read(tufPath);
        var elements = json["IngameElementsSettings"]!;
        check(json["ShowTUFHelperOverlayer"]!.GetValue<bool>() && elements["Leaderboard"]!["IsShowed"]!.GetValue<bool>(), "Keep TUF master and leaderboard on");
        check(elements["PPDisplayer"]!["Anchor"]!.GetValue<int>() == 3 && elements["PPDisplayer"]!["Position"]!["X"]!.GetValue<double>() == 5, "Seed score overlay with its actual default placement");
        check(elements["LevelInfo"]!["Anchor"]!.GetValue<int>() == 2, "Seed level info at top right");
        setup.Apply([jrp, tuf], "jrp-defaults");
        check(!Read(tufPath)["IngameElementsSettings"]!["Combo"]!["IsShowed"]!.GetValue<bool>(), "Installing JRP after TUF suppresses duplicate overlay");
        check(Read(tufPath)["IngameElementsSettings"]!["Combo"]!["Position"]!["Y"]!.GetValue<double>() == -125, "Hidden elements retain their correct defaults");
        File.WriteAllText(tufPath, """{"FavoriteLevels":[42],"LevelSaveFolder":"/personal","IngameElementsSettings":{"Leaderboard":{"Position":{"X":90,"Y":12},"Scale":0.8},"PPDisplayer":{"Categories":{"PPDisplayer":{"$type":"keep","IsShowScore":false,"IsShowSpeed":false}}}}}""");
        setup.Apply([tuf, jrp], "tuf-overlay");
        json = Read(tufPath);
        check(json["FavoriteLevels"]![0]!.GetValue<int>() == 42 && json["LevelSaveFolder"]!.GetValue<string>() == "/personal", "Preserve TUF favorites and download path");
        check(json["IngameElementsSettings"]!["Leaderboard"]!["Position"]!["X"]!.GetValue<int>() == 90, "Preserve existing overlay placement");
        var category = json["IngameElementsSettings"]!["PPDisplayer"]!["Categories"]!["PPDisplayer"]!;
        check(category["$type"]!.GetValue<string>() == "keep" && category["IsShowScore"]!.GetValue<bool>() && category["IsShowSpeed"]!.GetValue<bool>(), "Enable both score subfields without removing type data");

        setup.Apply([countdown], "countdown-defaults");
        check(Read(Path.Combine(countdown.Path, "UpdateSettings.json"))["ReceiveBetaUpdates"]!.GetValue<bool>(), "Enable Countdown beta updates in the right file");
        check(!File.Exists(Path.Combine(countdown.Path, "Settings.json")), "Don't write JALib settings to Countdown");
        check(OnboardingSetupService.DefaultKind("TUFHelperLite") is null && OnboardingSetupService.DefaultKind("EnhancedEffectRemover") is null, "Don't invent unsupported Lite or EER settings");
    }
}
