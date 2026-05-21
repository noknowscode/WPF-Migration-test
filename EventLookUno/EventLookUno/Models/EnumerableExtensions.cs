using System;
using System.Collections.Generic;

namespace EventLookUno.Models;

public static class EnumerableExtensions
{
    /// <summary>
    /// Disposes every item in the sequence. Used by <c>WindowsEventSource</c> to release
    /// <c>EventRecord</c> handles after adapting them into <see cref="EventEntry"/>.
    /// </summary>
    public static void DisposeAll<T>(this IEnumerable<T> seq) where T : IDisposable
    {
        foreach (var item in seq)
            item?.Dispose();
    }
}
