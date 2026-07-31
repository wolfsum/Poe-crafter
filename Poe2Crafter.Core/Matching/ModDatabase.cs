using System.Globalization;
using System.Text.RegularExpressions;
using Poe2Crafter.Core.Games;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.Core.Matching;

public class ModDatabase
{
    // Consecutive-line patterns (combined form, PoB multi-stat, advanced expansions).
    private readonly List<(Regex Regex, ModDefinition Mod, int LineCount)> _patterns;
    private readonly Dictionary<string, List<(Regex Regex, ModDefinition Mod, int LineCount)>> _byGroup;

    // Order-independent: each entry is one mod's individual line regexes. Used when
    // Attack/Cast (or Str/Dex/…) appear as separate clipboard lines in any order.
    private readonly Dictionary<string, List<(ModDefinition Mod, Regex[] Lines)>> _setByGroup;

    public IReadOnlyList<ModDefinition> AllMods { get; }

    public ModDatabase(IEnumerable<ModDefinition> mods)
    {
        AllMods = mods.ToList();
        _patterns = [];
        _setByGroup = new(StringComparer.Ordinal);

        foreach (var mod in AllMods)
        {
            foreach (var (pattern, lines) in BuildConsecutivePatterns(mod))
                _patterns.Add((new Regex("^" + pattern + "$", RegexOptions.IgnoreCase), mod, lines));

            foreach (var lineRxs in BuildSetPatterns(mod))
            {
                if (!_setByGroup.TryGetValue(mod.Group, out var list))
                    _setByGroup[mod.Group] = list = [];
                list.Add((mod, lineRxs));
            }
        }

        _byGroup = _patterns
            .GroupBy(p => p.Mod.Group)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public IReadOnlyList<ModGroup> GetGroups(GameProfile profile, SlotSelection selection)
    {
        var tags = profile.BuildTags(selection);

        return AllMods
            .Where(m => profile.SourceAllowed(m.Source, selection.Slot)
                        && profile.ModAllowed(m)
                        && CanSpawn(m, tags))
            .GroupBy(m => m.Group)
            .Select(g => new ModGroup(g.Key, g))
            .OrderBy(g => g.DisplayName)
            .ToList();
    }

    private static bool CanSpawn(ModDefinition m, IReadOnlySet<string> tags)
    {
        if (m.WeightKeys.Length == 0)
            return m.ItemTags.Any(tags.Contains);

        for (int i = 0; i < m.WeightKeys.Length && i < m.WeightVals.Length; i++)
        {
            var key = m.WeightKeys[i];
            if (key == "default" || tags.Contains(key))
                return m.WeightVals[i] > 0;
        }
        return false;
    }

    public IReadOnlyList<ModMatch> Match(string line)
    {
        var results = new List<ModMatch>();
        foreach (var (regex, mod, lineCount) in _patterns)
        {
            if (lineCount != 1) continue;
            var m = regex.Match(line);
            if (!m.Success) continue;
            var values = ExtractValues(m);
            if (!ValuesInRange(mod, values)) continue;
            results.Add(new ModMatch(mod, values));
        }
        return results;
    }

    public IReadOnlyList<LineMatch> MatchLines(IReadOnlyList<ParsedModLine> mods, string? itemClass = null)
    {
        var results = new List<LineMatch>();
        for (int i = 0; i < mods.Count; )
        {
            var best = BestConsecutiveAt(mods, i, itemClass, groupFilter: null);
            if (best is null) { i++; continue; }
            results.Add(best);
            i += best.LineCount;
        }
        return results;
    }

    public LineMatch? MatchGroup(IReadOnlyList<ParsedModLine> mods, string groupId, string? itemClass = null)
    {
        LineMatch? best = null;

        // 1) Consecutive multi-line / single-line patterns
        for (int i = 0; i < mods.Count; i++)
        {
            var hit = BestConsecutiveAt(mods, i, itemClass, groupFilter: groupId);
            if (hit is null) continue;
            best = Prefer(best, hit, itemClass);
        }

        // 2) Order-independent set match — Attack Speed + Cast Speed in any order,
        //    not necessarily adjacent, still counts as the combined affix.
        if (_setByGroup.TryGetValue(groupId, out var sets))
        {
            foreach (var (mod, lines) in sets)
            {
                var hit = MatchAsSet(mods, mod, lines);
                if (hit is null) continue;
                best = Prefer(best, hit, itemClass);
            }
        }

        return best;
    }

    private static LineMatch Prefer(LineMatch? current, LineMatch candidate, string? itemClass)
    {
        if (current is null) return candidate;
        if (candidate.LineCount > current.LineCount) return candidate;
        if (candidate.LineCount < current.LineCount) return current;
        int cd = DomainScore(candidate.Match.Mod, itemClass);
        int od = DomainScore(current.Match.Mod, itemClass);
        if (cd != od) return cd > od ? candidate : current;
        return candidate.Match.Mod.Tier > current.Match.Mod.Tier ? candidate : current;
    }

    private LineMatch? BestConsecutiveAt(IReadOnlyList<ParsedModLine> mods, int index, string? itemClass, string? groupFilter)
    {
        IEnumerable<(Regex Regex, ModDefinition Mod, int LineCount)> pool = groupFilter is null
            ? _patterns
            : _byGroup.GetValueOrDefault(groupFilter) ?? [];

        LineMatch? best = null;
        int bestDomain = int.MinValue;

        foreach (var (regex, mod, need) in pool)
        {
            if (index + need > mods.Count) continue;

            var text = need == 1
                ? mods[index].Text
                : string.Join("\n", mods.Skip(index).Take(need).Select(x => x.Text));

            var m = regex.Match(text);
            if (!m.Success) continue;

            var values = ExtractValues(m);
            if (!ValuesInRange(mod, values)) continue;

            var candidate = new LineMatch(
                new ModMatch(mod, values), index, need, mods[index],
                need == 1 ? mods[index].Text
                          : string.Join(" / ", mods.Skip(index).Take(need).Select(x => x.Text)));
            int domain = DomainScore(mod, itemClass);

            if (best is null
                || candidate.LineCount > best.LineCount
                || (candidate.LineCount == best.LineCount && domain > bestDomain)
                || (candidate.LineCount == best.LineCount && domain == bestDomain
                    && candidate.Match.Mod.Tier > best.Match.Mod.Tier))
            {
                best = candidate;
                bestDomain = domain;
            }
        }

        return best;
    }

    // Each line regex must hit a distinct clipboard line (any order).
    private static LineMatch? MatchAsSet(IReadOnlyList<ParsedModLine> mods, ModDefinition mod, Regex[] lineRxs)
    {
        if (lineRxs.Length < 2) return null;

        var used = new bool[mods.Count];
        var values = new List<double>();
        var texts = new List<string>();
        int firstIndex = int.MaxValue;

        foreach (var rx in lineRxs)
        {
            bool found = false;
            for (int i = 0; i < mods.Count; i++)
            {
                if (used[i]) continue;
                var m = rx.Match(mods[i].Text);
                if (!m.Success) continue;

                var vals = ExtractValues(m);
                if (vals.Length > 0 && mod.ValuesMin.Length > 0)
                {
                    // Every roll on this line must sit in the mod's overall range envelope
                    double lo = mod.ValuesMin.Min();
                    double hi = mod.ValuesMax.Max();
                    if (vals.Any(v => v < lo || v > hi)) continue;
                }

                used[i] = true;
                values.AddRange(vals);
                texts.Add(mods[i].Text);
                if (i < firstIndex) firstIndex = i;
                found = true;
                break;
            }
            if (!found) return null;
        }

        var arr = values.ToArray();
        if (!ValuesInRange(mod, arr)) return null;

        return new LineMatch(
            new ModMatch(mod, arr),
            firstIndex,
            lineRxs.Length,
            mods[firstIndex],
            string.Join(" / ", texts));
    }

    private static int DomainScore(ModDefinition mod, string? itemClass)
    {
        if (string.IsNullOrEmpty(itemClass)) return 0;
        var src = mod.Source;
        bool jewelish = itemClass.Contains("Jewel", StringComparison.OrdinalIgnoreCase)
                     || itemClass.Contains("Abyss", StringComparison.OrdinalIgnoreCase);
        bool cluster  = itemClass.Contains("Cluster", StringComparison.OrdinalIgnoreCase);
        bool abyss    = itemClass.Contains("Abyss", StringComparison.OrdinalIgnoreCase);

        if (jewelish)
        {
            if (abyss && src.Contains("Abyss", StringComparison.OrdinalIgnoreCase)) return 4;
            if (cluster && src.Contains("Cluster", StringComparison.OrdinalIgnoreCase)) return 4;
            if (src.Contains("Jewel", StringComparison.OrdinalIgnoreCase)) return 3;
            if (src.Contains("Explicit", StringComparison.OrdinalIgnoreCase)) return 0;
            return 1;
        }

        if (src.Contains("Explicit", StringComparison.OrdinalIgnoreCase)) return 3;
        if (src.Contains("Jewel", StringComparison.OrdinalIgnoreCase)) return 0;
        return 1;
    }

    public IReadOnlyList<ModMatch> MatchItem(IEnumerable<string> modLines)
    {
        var parsed = modLines.Select(t => new ParsedModLine(t, 0)).ToList();
        return MatchLines(parsed)
            .GroupBy(x => x.Match.Mod.Group)
            .Select(g => g.OrderByDescending(x => x.Match.Mod.Tier).First().Match)
            .ToList();
    }

    // ── Pattern builders ──────────────────────────────────────────────
    private static readonly (string CombinedSuffix, string[] LineSuffixes)[] Expansions =
    [
        (" to all Attributes",
            [" to Strength", " to Dexterity", " to Intelligence"]),
        (" to Strength and Dexterity",
            [" to Strength", " to Dexterity"]),
        (" to Strength and Intelligence",
            [" to Strength", " to Intelligence"]),
        (" to Dexterity and Intelligence",
            [" to Dexterity", " to Intelligence"]),
        ("% increased Attack and Cast Speed",
            ["% increased Attack Speed", "% increased Cast Speed"]),
    ];

    private static IEnumerable<(string Pattern, int LineCount)> BuildConsecutivePatterns(ModDefinition mod)
    {
        yield return (mod.MatchRegex, Math.Max(1, mod.Templates.Length));

        // Also register reversed order for 2-line PoB templates (Cast then Attack, etc.)
        if (mod.Templates.Length == 2)
        {
            var rev = PobModParser.BuildMatchRegex([mod.Templates[1], mod.Templates[0]]);
            yield return (rev, 2);
        }

        foreach (var template in mod.Templates)
        {
            foreach (var (combined, parts) in Expansions)
            {
                int idx = template.IndexOf(combined, StringComparison.Ordinal);
                if (idx < 0) continue;

                var prefix = template[..idx];
                var trailing = template[(idx + combined.Length)..];
                var lineRegexes = parts
                    .Select(p => PobModParser.BuildMatchRegex(prefix + p + trailing))
                    .ToArray();

                yield return (string.Join("\n", lineRegexes), parts.Length);
                if (parts.Length == 2)
                    yield return (string.Join("\n", lineRegexes.Reverse()), 2);
            }
        }
    }

    private static IEnumerable<Regex[]> BuildSetPatterns(ModDefinition mod)
    {
        // Multi-stat mods from PoB (minion Attack Speed + Cast Speed, Life+Mana, …)
        if (mod.Templates.Length >= 2)
        {
            yield return mod.Templates
                .Select(t => new Regex("^" + PobModParser.BuildMatchRegex(t) + "$", RegexOptions.IgnoreCase))
                .ToArray();
        }

        // Expanded combined phrases (all Attributes, Attack and Cast Speed, …)
        foreach (var template in mod.Templates)
        {
            foreach (var (combined, parts) in Expansions)
            {
                int idx = template.IndexOf(combined, StringComparison.Ordinal);
                if (idx < 0) continue;

                var prefix = template[..idx];
                var trailing = template[(idx + combined.Length)..];
                yield return parts
                    .Select(p => new Regex(
                        "^" + PobModParser.BuildMatchRegex(prefix + p + trailing) + "$",
                        RegexOptions.IgnoreCase))
                    .ToArray();
            }
        }
    }

    private static bool ValuesInRange(ModDefinition mod, double[] values)
    {
        if (mod.ValuesMin.Length == 0 || values.Length == 0) return true;
        for (int i = 0; i < values.Length; i++)
        {
            int ri = Math.Min(i, mod.ValuesMin.Length - 1);
            if (values[i] < mod.ValuesMin[ri] || values[i] > mod.ValuesMax[ri])
                return false;
        }
        return true;
    }

    private static double[] ExtractValues(Match m)
    {
        var values = new List<double>();
        for (int i = 1; i < m.Groups.Count; i++)
        {
            if (!m.Groups[i].Success) continue;
            if (double.TryParse(m.Groups[i].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                values.Add(v);
        }
        return values.ToArray();
    }
}

public record LineMatch(
    ModMatch Match,
    int Index,
    int LineCount,
    ParsedModLine FirstLine,
    string? DisplayText = null);
