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
var assignments = new Dictionary<string, string> { ["1"] = "laptop", ["2"] = "external-right", ["3"] = "external-left" };
var dedicated = new DedicatedWindows();
var firstWindow = new WindowCandidate(11, 101, 1001, 10);
var secondWindow = new WindowCandidate(22, 102, 1002, 20);
Check("Dedicated adopts sole existing window", dedicated.Select("7", [firstWindow]) == 11);
Check("Dedicated stays on adopted window despite newer focus", dedicated.Select("7", [firstWindow, secondWindow]) == 11);
Check("Closed dedicated window adopts remaining window", dedicated.Select("7", [secondWindow]) == 22);
var thirdWindow = new WindowCandidate(33, 103, 1003, 30);
Check("Closed dedicated chooses most recently used replacement", dedicated.Select("7", [firstWindow, thirdWindow]) == 33);
Check("Adopted replacement remains dedicated", dedicated.Select("7", [firstWindow with { LastFocused = 99 }, thirdWindow]) == 33);
Check("No dedicated candidate requests launch", dedicated.Select("7", []) == 0);
Check("Newly launched window becomes dedicated", dedicated.Select("7", [secondWindow]) == 22);
Check("Recycled handle with another process is not treated as tracked", dedicated.Select("7", [secondWindow with { Pid = 104, Started = 1004 }, thirdWindow]) == 33);
Check("Fresh session adopts most recently used existing window", new DedicatedWindows().Select("7", [firstWindow, secondWindow]) == 22);
var numbered = MonitorAssignments.NumberPaths(["DISPLAY1", "DISPLAY21", "DISPLAY22"]);
Check("Windows path 2 is display 21, not fallback to display 1", numbered[2] == "DISPLAY21");
numbered = MonitorAssignments.NumberPaths(["DISPLAY1", "DISPLAY22", "DISPLAY21"]);
Check("Numbering follows updated Windows path order", numbered[2] == "DISPLAY22");
numbered = MonitorAssignments.NumberPaths(["DISPLAY1", "DISPLAY21"]);
Check("Disconnected Windows path falls back to remaining number", Configuration.ResolveMonitor(3, numbered.Keys) == 2);
var displays = MonitorAssignments.Available(assignments, ["external-left", "LAPTOP", "external-right"]);
Check("Logical monitor 2 uses its identity, independent of enumeration order", displays[2] == "external-right");
displays = MonitorAssignments.Available(assignments, ["external-left", "laptop"]);
Check("Disconnected assigned monitor falls back to closest lower", Configuration.ResolveMonitor(2, displays.Keys) == 1);
displays = MonitorAssignments.Available(assignments, ["external-left"]);
Check("Assigned monitor fallback when none lower", Configuration.ResolveMonitor(2, displays.Keys) == 3);
Check("Unknown physical monitor cannot silently impersonate an assigned number", MonitorAssignments.Available(assignments, ["unknown"]).Count == 0);
Reject("Duplicate physical monitor identities", """{"monitors":{"1":"screen-a","2":"SCREEN-A"}}""");
Reject("Invalid assigned number", """{"monitors":{"0":"screen-a"}}""");
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
