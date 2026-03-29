using System;

namespace AutoSort.Tests.Fakes;

public class SynchronousScheduler : IActionScheduler
{
    public void Schedule(Action action, int delayMs) => action();
}
