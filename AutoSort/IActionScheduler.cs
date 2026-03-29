using System;

namespace AutoSort;

public interface IActionScheduler
{
    void Schedule(Action action, int delayMs);
}
