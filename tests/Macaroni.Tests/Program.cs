using Macaroni;

int passed = 0;
void Check(string name, bool success)
{
    if (!success) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
void Reject(string name, string json)
{
    try { Configuration.Parse(json); }
    catch { Check(name, true); return; }
    Check(name, false);
}
Check("Exact monitor", Configuration.ResolveMonitor(3, [1, 3, 5]) == 3);
Check("Closest lower monitor", Configuration.ResolveMonitor(4, [1, 3, 5]) == 3);
Check("Lowest monitor when none lower", Configuration.ResolveMonitor(1, [3, 5]) == 3);
Check("Only remaining monitor", Configuration.ResolveMonitor(8, [2]) == 2);
Reject("Reserved zero", """{"mappings":{"0":{"action":"folder","path":"C:\\"}}}""");
Reject("Exact title required", """{"mappings":{"9":{"executable":"wt.exe","process":"WindowsTerminal","selection":"title"}}}""");
Reject("Unknown action", """{"mappings":{"9":{"action":"oops"}}}""");
Reject("Null arguments", """{"mappings":{"9":{"action":"command","executable":"cmd.exe","arguments":[null]}}}""");
Reject("Invalid monitor", """{"mappings":{"9":{"action":"folder","path":"C:\\","monitor":0}}}""");
Reject("Incomplete save", "{\"mappings\":");
Reject("Misspelled setting", """{"monitro":2}""");
Check("Comments and trailing commas", Configuration.Parse("{ /* comment */ \"version\":1, }").Version == 1);
var p = new KeyPolicy();
Check("Mapped press fires", p.Handle(0x49, true, false, true) == (true, true));
Check("Held key does not repeat", p.Handle(0x49, true, false, true) == (true, false));
Check("Release stays suppressed after state changes", p.Handle(0x49, false, true, false) == (true, false));
Check("Num Lock on passes through", p.Handle(0x49, true, true, true) == (false, false));
Check("Pass-through press stays pass-through after toggle", p.Handle(0x49, true, false, true) == (false, false));
Check("Pass-through release", p.Handle(0x49, false, false, true) == (false, false));
Check("Disabled or unmapped key passes through", p.Handle(0x48, true, false, false) == (false, false));
Check("Toggle can fire while other keys disabled", p.Handle(0x52, true, false, true) == (true, true));
Check("Toggle does not repeat", p.Handle(0x52, true, false, true) == (true, false));
if (args.Length > 0) Check("Example configuration", Configuration.Parse(File.ReadAllText(args[0])).Mappings.Count == 4);
Console.WriteLine($"{passed} checks passed.");
