using System;
using ECommons.Schedulers;

namespace AutoSort;

public class TickSchedulerAdapter : IActionScheduler
{
    public void Schedule(Action action, int delayMs) =>
        new TickScheduler(action, delayMs);
}
