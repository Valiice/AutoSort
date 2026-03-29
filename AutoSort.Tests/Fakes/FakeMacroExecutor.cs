using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeMacroExecutor : IMacroExecutor
{
    public List<IEnumerable<string>> Calls { get; } = new();
    public void Execute(IEnumerable<string> commands) => Calls.Add(commands);
}
