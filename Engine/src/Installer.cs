using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using DnMethodAttributes = dnlib.DotNet.MethodAttributes;
using DnMethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using DnTypeAttributes = dnlib.DotNet.TypeAttributes;

namespace NativeUmm;

internal sealed class Installer(GameLayout layout)
{
    public InstallStatus ReadStatus()
    {
        var entryAssemblyPath = layout.EntryAssemblyPath;
        var managerDll = Path.Combine(layout.ManagerPath, "UnityModManager.dll");
        var status = new InstallStatus
        {
            ManagerInstalled = File.Exists(managerDll),
            HasOriginalBackup = File.Exists(layout.OriginalBackupPath),
            OriginalBackupPath = layout.OriginalBackupPath
        };

        if (status.ManagerInstalled)
        {
            try
            {
                using var manager = ModuleDefMD.Load(File.ReadAllBytes(managerDll));
                status.ManagerVersion = manager.Assembly?.Version?.ToString();
            }
            catch
            {
                status.ManagerVersion = "unreadable";
            }
        }

        try
        {
            using var module = ModuleDefMD.Load(File.ReadAllBytes(entryAssemblyPath));
            var warnings = new List<string>();
            status.HookInstalled = HasStarterHook(module);
            if (ValidateEntryPoint(module, createMissing: false) is null)
                warnings.Add("Target entry method missing; installer can create static constructor during install.");

            if (status.HasOriginalBackup && !OriginalBackupMatches(module, layout.OriginalBackupPath))
                warnings.Add("Original backup does not match current game assembly; install/repair will refresh it.");

            var configWarning = ReadConfigWarning();
            if (configWarning is not null)
                warnings.Add(configWarning);

            status.Warning = warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings);
        }
        catch (Exception ex)
        {
            status.Warning = $"Could not inspect hook: {ex.Message}";
        }

        return status;
    }

    public async Task InstallAsync(string? payloadDir, bool forcePayload)
    {
        Directory.CreateDirectory(layout.ManagerPath);
        Directory.CreateDirectory(layout.ModsPath);

        var payload = await PayloadResolver.ResolveAsync(layout, payloadDir, forceDownload: forcePayload);
        CopyPayload(payload, forcePayload);
        ApplyHarmonyCompatibilityOverride();
        WriteGameConfig();
        PatchEntryAssembly();
        Log.Info("Install / repair complete.");
    }

    public void RemoveHook()
    {
        BackupTimestamped(layout.EntryAssemblyPath);
        using var module = ModuleDefMD.Load(File.ReadAllBytes(layout.EntryAssemblyPath));
        var removed = RemoveStarterHook(module);
        if (!removed)
        {
            Log.Warn("No hook found.");
            return;
        }
        WriteModuleAtomically(module, layout.EntryAssemblyPath);
        Log.Info("Hook removed.");
    }

    public void RestoreOriginal()
    {
        if (!File.Exists(layout.OriginalBackupPath))
        {
            Log.Warn("No .original_ backup exists.");
            return;
        }

        BackupTimestamped(layout.EntryAssemblyPath);
        File.Copy(layout.OriginalBackupPath, layout.EntryAssemblyPath, overwrite: true);
        Log.Info("Original assembly restored.");
    }

    private void PatchEntryAssembly()
    {
        BackupTimestamped(layout.EntryAssemblyPath);

        using (var cleanModule = ModuleDefMD.Load(File.ReadAllBytes(layout.EntryAssemblyPath)))
        {
            var cleanModuleWasMutated = RemoveStarterHook(cleanModule);
            cleanModuleWasMutated |= RemoveMarker(cleanModule);
            RefreshOriginalBackup(cleanModule, cleanModuleWasMutated);
        }

        using var module = ModuleDefMD.Load(File.ReadAllBytes(layout.EntryAssemblyPath));
        RemoveStarterHook(module);

        var method = ValidateEntryPoint(module, createMissing: true)
                     ?? throw new InvalidOperationException("Could not create or find target entry method.");

        EnsureMarker(module);
        var starter = EnsureStarter(module);
        var start = starter.Methods.First(m => m.Name == "Start");
        var call = OpCodes.Call.ToInstruction(start);

        if (Spec.EntryPoint.Place == InsertPlace.Before)
            method.Body.Instructions.Insert(0, call);
        else
            method.Body.Instructions.Insert(Math.Max(0, method.Body.Instructions.Count - 1), call);

        WriteModuleAtomically(module, layout.EntryAssemblyPath);
    }

    private void RefreshOriginalBackup(ModuleDefMD cleanModule, bool cleanModuleWasMutated)
    {
        if (File.Exists(layout.OriginalBackupPath) && OriginalBackupMatches(cleanModule, layout.OriginalBackupPath))
            return;

        if (File.Exists(layout.OriginalBackupPath))
            BackupTimestamped(layout.OriginalBackupPath);

        if (cleanModuleWasMutated)
            WriteModuleAtomically(cleanModule, layout.OriginalBackupPath);
        else
            File.Copy(layout.EntryAssemblyPath, layout.OriginalBackupPath, overwrite: true);

        Log.Info("Refreshed UnityEngine.CoreModule.dll.original_");
    }

    private void CopyPayload(Payload payload, bool force)
    {
        foreach (var name in Spec.PayloadFiles)
        {
            if (name == "System.Xml.dll" && File.Exists(Path.Combine(layout.ManagedPath, name)))
                continue;

            if (!payload.Files.TryGetValue(name, out var source))
                throw new FileNotFoundException($"Payload file missing: {name}");

            var dest = Path.Combine(layout.ManagerPath, name);
            var shouldCopy = force || payload.IsBundled || !File.Exists(dest) || !FilesEqual(source, dest);
            if (!shouldCopy)
                continue;

            if (File.Exists(dest))
                BackupTimestamped(dest);

            File.Copy(source, dest, overwrite: true);
            Log.Info($"Copied {name}");
        }
    }

    private void ApplyHarmonyCompatibilityOverride()
    {
        var overridePath = Environment.GetEnvironmentVariable("ADOFAI_HARMONY_OVERRIDE");
        if (string.IsNullOrWhiteSpace(overridePath) || !File.Exists(overridePath))
            return;

        var destination = Path.Combine(layout.ManagerPath, "0Harmony.dll");
        var installedVersion = ReadAssemblyVersion(destination);
        var overrideVersion = ReadAssemblyVersion(overridePath);
        if (overrideVersion is null || overrideVersion < new Version(2, 4))
            throw new InvalidDataException("Bundled Harmony compatibility override is not version 2.4 or newer.");

        if (installedVersion is not null && installedVersion >= new Version(2, 4))
        {
            Log.Info($"Keeping Harmony {installedVersion}; compatibility override is not needed.");
            return;
        }

        if (File.Exists(destination))
            BackupTimestamped(destination);
        File.Copy(overridePath, destination, overwrite: true);
        Log.Info($"Applied Harmony {overrideVersion} compatibility override.");
    }

    private static Version? ReadAssemblyVersion(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            using var module = ModuleDefMD.Load(File.ReadAllBytes(path));
            return module.Assembly?.Version;
        }
        catch
        {
            return null;
        }
    }

    private static bool FilesEqual(string a, string b)
    {
        try
        {
            var infoA = new FileInfo(a);
            var infoB = new FileInfo(b);
            if (infoA.Length != infoB.Length)
                return false;
            using var streamA = File.OpenRead(a);
            using var streamB = File.OpenRead(b);
            using var sha = SHA256.Create();
            return sha.ComputeHash(streamA).SequenceEqual(sha.ComputeHash(streamB));
        }
        catch
        {
            return false;
        }
    }

    private void WriteGameConfig()
    {
        var configPath = Path.Combine(layout.ManagerPath, "Config.xml");
        var xml = BuildGameConfig();

        if (File.Exists(configPath))
        {
            var existing = XDocument.Load(configPath);
            if (ConfigMatches(expected: xml, actual: existing))
                return;

            BackupTimestamped(configPath);
        }

        xml.Save(configPath);
        Log.Info("Wrote Config.xml");
    }

    private string? ReadConfigWarning()
    {
        var configPath = Path.Combine(layout.ManagerPath, "Config.xml");
        if (!File.Exists(configPath))
            return "UnityModManager/Config.xml missing; install/repair will create it.";

        try
        {
            var existing = XDocument.Load(configPath);
            return ConfigMatches(BuildGameConfig(), existing)
                ? null
                : "UnityModManager/Config.xml does not match the required ADOFAI settings; install/repair will rewrite it.";
        }
        catch (Exception ex)
        {
            return $"UnityModManager/Config.xml unreadable; install/repair will rewrite it. {ex.Message}";
        }
    }

    internal static XDocument BuildGameConfig()
    {
        return new XDocument(
            new XElement("Config",
                new XAttribute("Name", "A Dance of Fire and Ice"),
                new XElement("Folder", "ADOFAI"),
                new XElement("ModsDirectory", "Mods"),
                new XElement("ModInfo", "Info.json"),
                new XElement("GameExe", "A Dance of Fire and Ice.exe"),
                new XElement("EntryPoint", Spec.EntryPoint.ToConfigString()),
                new XElement("StartingPoint", Spec.StartingPoint.ToConfigString()),
                new XElement("UIStartingPoint", Spec.UIStartingPoint.ToConfigString()),
                new XElement("MinimalManagerVersion", "0.22.14"),
                new XElement("Comment", "Required minimum game version 2.7.0")));
    }

    private static bool ConfigMatches(XDocument expected, XDocument actual)
    {
        var expectedRoot = expected.Root;
        var actualRoot = actual.Root;
        if (expectedRoot is null || actualRoot is null)
            return false;

        if (actualRoot.Attribute("Name")?.Value != expectedRoot.Attribute("Name")?.Value)
            return false;

        foreach (var element in expectedRoot.Elements())
        {
            if (actualRoot.Element(element.Name)?.Value != element.Value)
                return false;
        }

        return true;
    }

    private MethodDef? ValidateEntryPoint(ModuleDefMD module, bool createMissing)
    {
        var type = module.Types.FirstOrDefault(t => t.FullName == Spec.EntryPoint.TypeName);
        if (type is null)
            return null;

        var method = type.Methods.FirstOrDefault(m => m.Name == Spec.EntryPoint.MethodName);
        if (method is not null)
            return method;

        if (!createMissing || Spec.EntryPoint.MethodName != ".cctor")
            return null;

        method = new MethodDefUser(
            ".cctor",
            MethodSig.CreateStatic(module.CorLibTypes.Void),
            DnMethodImplAttributes.IL | DnMethodImplAttributes.Managed,
            DnMethodAttributes.Private | DnMethodAttributes.Static | DnMethodAttributes.HideBySig |
            DnMethodAttributes.SpecialName | DnMethodAttributes.RTSpecialName);
        method.Body = new CilBody();
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(method);
        return method;
    }

    private static bool HasStarterHook(ModuleDefMD module)
    {
        return module.Types.Any(t => t.FullName == "UnityModManagerNet.Injection.UnityModManagerStarter") &&
               module.GetTypes().SelectMany(t => t.Methods)
                   .Where(m => m.HasBody)
                   .SelectMany(m => m.Body.Instructions)
                   .Any(IsStarterCall);
    }

    private static bool RemoveStarterHook(ModuleDefMD module)
    {
        var removed = false;
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody))
        {
            for (var i = method.Body.Instructions.Count - 1; i >= 0; i--)
            {
                if (!IsStarterCall(method.Body.Instructions[i]))
                    continue;

                method.Body.Instructions.RemoveAt(i);
                removed = true;
            }
        }

        foreach (var type in module.Types
                     .Where(t => t.FullName == "UnityModManagerNet.Injection.UnityModManagerStarter")
                     .ToList())
        {
            module.Types.Remove(type);
            removed = true;
        }

        return removed;
    }

    private static bool RemoveMarker(ModuleDefMD module)
    {
        var removed = false;
        foreach (var type in module.Types
                     .Where(t => t.FullName == "UnityModManagerNet.Marks.IsDirty")
                     .ToList())
        {
            module.Types.Remove(type);
            removed = true;
        }

        return removed;
    }

    private static bool IsStarterCall(Instruction instruction)
    {
        if (instruction.OpCode != OpCodes.Call || instruction.Operand is not IMethod method)
            return false;

        var typeName = method.DeclaringType.FullName;
        return method.Name == "Start" &&
               (typeName == "UnityModManagerNet.Injection.UnityModManagerStarter" ||
                typeName == "UnityModManagerNet.UnityModManager");
    }

    private static void EnsureMarker(ModuleDefMD module)
    {
        if (module.Types.Any(t => t.FullName == "UnityModManagerNet.Marks.IsDirty"))
            return;

        var marker = new TypeDefUser(
            "UnityModManagerNet.Marks",
            "IsDirty",
            module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = DnTypeAttributes.Public | DnTypeAttributes.Abstract |
                         DnTypeAttributes.Sealed | DnTypeAttributes.BeforeFieldInit
        };
        module.Types.Add(marker);
    }

    private static TypeDef EnsureStarter(ModuleDefMD module)
    {
        var existing = module.Types.FirstOrDefault(t => t.FullName == "UnityModManagerNet.Injection.UnityModManagerStarter");
        if (existing is not null)
            return existing;

        var starter = new TypeDefUser(
            "UnityModManagerNet.Injection",
            "UnityModManagerStarter",
            module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = DnTypeAttributes.Public | DnTypeAttributes.AutoLayout |
                         DnTypeAttributes.Class | DnTypeAttributes.AnsiClass |
                         DnTypeAttributes.BeforeFieldInit
        };

        starter.Methods.Add(BuildStarterMethod(module));
        module.Types.Add(starter);
        return starter;
    }

    private static MethodDef BuildStarterMethod(ModuleDefMD module)
    {
        var corlib = module.CorLibTypes.AssemblyRef;
        var consoleType = new TypeRefUser(module, "System", "Console", corlib);
        var pathType = new TypeRefUser(module, "System.IO", "Path", corlib);
        var assemblyType = new TypeRefUser(module, "System.Reflection", "Assembly", corlib);
        var bindingFlagsType = new TypeRefUser(module, "System.Reflection", "BindingFlags", corlib);
        var typeType = new TypeRefUser(module, "System", "Type", corlib);
        var methodInfoType = new TypeRefUser(module, "System.Reflection", "MethodInfo", corlib);
        var exceptionType = new TypeRefUser(module, "System", "Exception", corlib);
        var objectType = module.CorLibTypes.Object.ToTypeDefOrRef();

        MemberRefUser Method(IMemberRefParent type, string name, MethodSig sig) => new(module, name, sig, type);

        var getExecutingAssembly = Method(assemblyType, "GetExecutingAssembly", MethodSig.CreateStatic(new ClassSig(assemblyType)));
        var getLocation = Method(assemblyType, "get_Location", MethodSig.CreateInstance(module.CorLibTypes.String));
        var loadFrom = Method(assemblyType, "LoadFrom", MethodSig.CreateStatic(new ClassSig(assemblyType), module.CorLibTypes.String));
        var getAssemblyType = Method(assemblyType, "GetType", MethodSig.CreateInstance(new ClassSig(typeType), module.CorLibTypes.String));
        var getMethod = Method(typeType, "GetMethod", MethodSig.CreateInstance(new ClassSig(methodInfoType), module.CorLibTypes.String, new ValueTypeSig(bindingFlagsType)));
        var invoke = Method(methodInfoType, "Invoke", MethodSig.CreateInstance(module.CorLibTypes.Object, module.CorLibTypes.Object, new SZArraySig(module.CorLibTypes.Object)));
        var getDirectoryName = Method(pathType, "GetDirectoryName", MethodSig.CreateStatic(module.CorLibTypes.String, module.CorLibTypes.String));
        var combine = Method(pathType, "Combine", MethodSig.CreateStatic(module.CorLibTypes.String, module.CorLibTypes.String, module.CorLibTypes.String));
        var writeLine = Method(consoleType, "WriteLine", MethodSig.CreateStatic(module.CorLibTypes.Void, module.CorLibTypes.String));
        var concat = Method(new TypeRefUser(module, "System", "String", corlib), "Concat", MethodSig.CreateStatic(module.CorLibTypes.String, module.CorLibTypes.String, module.CorLibTypes.String));
        var exceptionToString = Method(exceptionType, "ToString", MethodSig.CreateInstance(module.CorLibTypes.String));

        var start = new MethodDefUser(
            "Start",
            MethodSig.CreateStatic(module.CorLibTypes.Void),
            DnMethodImplAttributes.IL | DnMethodImplAttributes.Managed,
            DnMethodAttributes.Public | DnMethodAttributes.Static | DnMethodAttributes.HideBySig);

        var body = new CilBody { InitLocals = true, MaxStack = 5 };
        start.Body = body;
        var fileLocal = new Local(module.CorLibTypes.String);
        var assemblyLocal = new Local(new ClassSig(assemblyType));
        var typeLocal = new Local(new ClassSig(typeType));
        var methodLocal = new Local(new ClassSig(methodInfoType));
        var exceptionLocal = new Local(new ClassSig(exceptionType));
        body.Variables.Add(fileLocal);
        body.Variables.Add(assemblyLocal);
        body.Variables.Add(typeLocal);
        body.Variables.Add(methodLocal);
        body.Variables.Add(exceptionLocal);

        var done = Instruction.Create(OpCodes.Ret);
        var tryStart = Instruction.Create(OpCodes.Call, getExecutingAssembly);
        var tryEnd = Instruction.Create(OpCodes.Leave_S, done);
        var catchStart = Instruction.Create(OpCodes.Stloc, exceptionLocal);

        body.Instructions.Add(tryStart);
        body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getLocation));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, getDirectoryName));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "UnityModManager"));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, combine));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "UnityModManager.dll"));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, combine));
        body.Instructions.Add(Instruction.Create(OpCodes.Stloc, fileLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "[Assembly] Loading UnityModManager by "));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, fileLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, concat));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, writeLine));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, fileLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, loadFrom));
        body.Instructions.Add(Instruction.Create(OpCodes.Stloc, assemblyLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, assemblyLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "UnityModManagerNet.Injector"));
        body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getAssemblyType));
        body.Instructions.Add(Instruction.Create(OpCodes.Stloc, typeLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, typeLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "Run"));
        body.Instructions.Add(Instruction.CreateLdcI4((int)(BindingFlags.Static | BindingFlags.Public)));
        body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getMethod));
        body.Instructions.Add(Instruction.Create(OpCodes.Stloc, methodLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, methodLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        body.Instructions.Add(Instruction.CreateLdcI4(1));
        body.Instructions.Add(Instruction.Create(OpCodes.Newarr, objectType));
        body.Instructions.Add(Instruction.Create(OpCodes.Dup));
        body.Instructions.Add(Instruction.CreateLdcI4(0));
        body.Instructions.Add(Instruction.CreateLdcI4(0));
        body.Instructions.Add(Instruction.Create(OpCodes.Box, module.CorLibTypes.Boolean.ToTypeDefOrRef()));
        body.Instructions.Add(Instruction.Create(OpCodes.Stelem_Ref));
        body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, invoke));
        body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        body.Instructions.Add(tryEnd);
        body.Instructions.Add(catchStart);
        body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, exceptionLocal));
        body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, exceptionToString));
        body.Instructions.Add(Instruction.Create(OpCodes.Call, writeLine));
        body.Instructions.Add(Instruction.Create(OpCodes.Leave_S, done));
        body.Instructions.Add(done);

        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = catchStart,
            HandlerStart = catchStart,
            HandlerEnd = done,
            CatchType = exceptionType
        });

        return start;
    }

    private static void BackupTimestamped(string path)
    {
        if (!File.Exists(path))
            return;

        var stem = $"{path}.nativeumm_backup_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
        var backup = stem;
        var suffix = 1;
        while (File.Exists(backup))
            backup = $"{stem}-{suffix++}";
        File.Copy(path, backup, overwrite: false);
    }

    private static bool OriginalBackupMatches(ModuleDefMD module, string backupPath)
    {
        if (!File.Exists(backupPath))
            return false;

        try
        {
            using var backup = ModuleDefMD.Load(File.ReadAllBytes(backupPath));
            return backup.Mvid == module.Mvid;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteModuleAtomically(ModuleDefMD module, string path)
    {
        var temp = $"{path}.nativeumm_tmp_{Environment.ProcessId}";
        module.Write(temp);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }
}

internal sealed class PayloadResolver
{
    public static async Task<Payload> ResolveAsync(GameLayout layout, string? bundledPayloadDir, bool forceDownload)
    {
        var bundledDirs = BundledPayloadDirectories(bundledPayloadDir).Distinct(StringComparer.Ordinal).ToList();
        var bundled = TryBuild(bundledDirs, isBundled: true);
        if (bundled is not null)
            return bundled;

        var candidates = CandidatePayloadDirectories(layout).Distinct(StringComparer.Ordinal).ToList();
        if (!forceDownload)
        {
            var payload = TryBuild(candidates);
            if (payload is not null)
                return payload;
        }

        var cache = await DownloadOfficialPayloadAsync();
        candidates.Insert(0, cache);
        return TryBuild(candidates) ?? throw new InvalidOperationException("Could not resolve UnityModManager payload files.");
    }

    private static IEnumerable<string> BundledPayloadDirectories(string? bundledPayloadDir)
    {
        if (!string.IsNullOrWhiteSpace(bundledPayloadDir))
            yield return bundledPayloadDir;

        yield return Path.Combine(AppContext.BaseDirectory, "payload");
        yield return Path.Combine(AppContext.BaseDirectory, "UnityModManagerInstaller");
    }

    private static IEnumerable<string> CandidatePayloadDirectories(GameLayout layout)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "payload");
        yield return Path.Combine(AppContext.BaseDirectory, "UnityModManagerInstaller");
        yield return layout.ManagerPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "lib");
            yield return Path.Combine(dir.FullName, "UnityModManagerInstaller");
            dir = dir.Parent;
        }

        yield return CachePayloadPath;
    }

    private static Payload? TryBuild(IEnumerable<string> directories, bool isBundled = false)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in directories.Where(Directory.Exists))
        {
            foreach (var name in Spec.PayloadFiles)
            {
                if (files.ContainsKey(name))
                    continue;

                var direct = Path.Combine(dir, name);
                if (File.Exists(direct))
                {
                    files[name] = direct;
                    continue;
                }

                var nested = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
                if (nested is not null)
                    files[name] = nested;
            }
        }

        return files.ContainsKey("UnityModManager.dll") &&
               files.ContainsKey("0Harmony.dll") &&
               files.ContainsKey("dnlib.dll")
            ? new Payload(files, isBundled)
            : null;
    }

    private static string CachePayloadPath => Path.Combine(AppData.Cache, "payload");

    private static async Task<string> DownloadOfficialPayloadAsync()
    {
        Directory.CreateDirectory(CachePayloadPath);
        var zipPath = Path.Combine(CachePayloadPath, "UnityModManager.zip");

        Log.Info("Downloading UnityModManager payload...");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("adofai-mod-manager/1.0");
        var bytes = await client.GetByteArrayAsync(Spec.OfficialPayloadUrl);
        await File.WriteAllBytesAsync(zipPath, bytes);

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (!Spec.PayloadFiles.Contains(name, StringComparer.OrdinalIgnoreCase) || string.IsNullOrEmpty(name))
                continue;

            entry.ExtractToFile(Path.Combine(CachePayloadPath, name), overwrite: true);
        }

        var stamp = Convert.ToHexString(SHA256.HashData(bytes))[..16];
        await File.WriteAllTextAsync(Path.Combine(CachePayloadPath, "payload.sha256.txt"), stamp);
        return CachePayloadPath;
    }
}
