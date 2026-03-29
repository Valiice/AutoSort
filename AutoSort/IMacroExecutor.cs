using System.Collections.Generic;

namespace AutoSort;

public interface IMacroExecutor
{
    void Execute(IEnumerable<string> commands);
}
