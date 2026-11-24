using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.Networking.Bootstrapping
{
public static class NetworkStartupGate
{
    private static Func<UniTask> _wait;
    private static Func<bool> _canStart;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _wait = null;
        _canStart = null;
    }
    public static void Configure(Func<UniTask> wait, Func<bool> canStart)
    {
        _wait = wait;
        _canStart = canStart;
    }
    public static async UniTask WaitAsync()
    {
        if (_wait == null) throw new InvalidOperationException("Application bootstrap has not configured networking.");
        await _wait();
        if (_canStart == null || !_canStart()) throw new OperationCanceledException("Application is not accepting connections.");
    }
}

}
