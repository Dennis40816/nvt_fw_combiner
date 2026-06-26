using System.Reflection;

string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
if (args is ["--version"] or ["version"])
{
    Console.WriteLine(version);
    return 0;
}

if (args is ["doctor"])
{
    Console.WriteLine("NVT FW Combiner repository bootstrap is healthy.");
    Console.WriteLine($"CLI assembly version: {version}");
    Console.WriteLine("Firmware composition commands are introduced by the Composition Core milestone.");
    return 0;
}

Console.Error.WriteLine("Usage: nvt_fw_combiner [--version|doctor]");
return 64;
