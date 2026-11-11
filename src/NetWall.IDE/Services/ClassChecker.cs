using System.Collections.Generic;
using System.Linq;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public static class ClassChecker
{
    public static List<bool> Check(IReadOnlyList<ChallengeTest> tests, ChallengeResult result)
    {
        var packets = result.Packets ?? [];
        var next = 0;
        return [.. tests.Select(test => Passes(test, result, packets, ref next))];
    }

    private static bool Passes(ChallengeTest test, ChallengeResult result, List<SimulatedPacket> packets, ref int next)
    {
        switch (test.Kind)
        {
            case "packet":
                return next < packets.Count && packets[next++].Action == test.Expect;
            case "policy":
                return result.PolicyDefined && result.DefaultPolicy == test.Expect;
            case "clean":
                return result.Diagnostics.Count == 0;
            default:
                return result.Declarations?.FirstOrDefault(declaration => declaration.Name == test.Name) is { } found
                       && (test.Type is null || found.Type == test.Type)
                       && (test.Value is null || SameValue(found, test.Value));
        }
    }

    private static bool SameValue(ChallengeDeclaration declaration, string expected) =>
        declaration.Type == "Red"
            ? Ipv4.Parse(declaration.Text) is { } actual && Ipv4.Parse(expected) == actual
            : declaration.Text == expected;
}
