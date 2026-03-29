using System;
using System.Collections.Generic;
using ECommons.Automation;
using ECommons.Logging;

namespace AutoSort;

public class MacroExecutor : IMacroExecutor
{
    public void Execute(IEnumerable<string> commands)
    {
        try
        {
            MacroManager.Execute(commands);
        }
        catch (Exception e)
        {
            PluginLog.Error($"AutoSort: Macro execution failed: {e.Message}");
        }
    }
}
