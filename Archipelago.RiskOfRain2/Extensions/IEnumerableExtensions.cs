using System;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.RiskOfRain2.Extensions;

public static class IEnumerableExtensions
{
    private static readonly Random rand = new();
    public static T PickRandom<T>(this IEnumerable<T> self)
    {
        var list = self.ToList();
        return list[rand.Next(list.Count)];
    }
}