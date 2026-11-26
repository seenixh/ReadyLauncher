using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReadyLauncher
{
    /// <summary>
    /// How random numbers for the lamp strip look: groups separated by commas, each written like xxx/0-999, one x for each
    /// digit (a smaller number is padded with zeros to that many digits), then a slash and the lowest and highest numbers it
    /// can be. Without the slash and range, a group can be any number with that many digits. "xx/0-99,xxx/0-999,xxxx/0-9999"
    /// picks numbers like 24 432 8476.
    /// </summary>
    internal class NumberPattern
    {
        public const string Usual = "xx/0-99,xxx/0-999,xxxx/0-9999";
        public const int MaxGroups = 20, MaxDigits = 9;

        class Group { public int Digits, Min, Max; }

        readonly List<Group> groups;
        static readonly Random rnd = new Random();
        static readonly Regex GroupText = new Regex(@"^(x+)(?:/(\d+)-(\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        NumberPattern(List<Group> groups) { this.groups = groups; }

        /// <summary>The pattern written out, or null with <paramref name="problem"/> saying what's wrong with it.</summary>
        public static NumberPattern Parse(string text, out string problem)
        {
            problem = null;
            var parts = (text ?? "").Split(',').Select(p => Regex.Replace(p, @"\s+", "")).ToList();
            if (parts.All(p => p.Length == 0)) { problem = "Type at least one group of numbers, like xxx/0-999."; return null; }
            if (parts.Count > MaxGroups) { problem = "That's " + parts.Count + " groups; there can be " + MaxGroups + " at most."; return null; }
            var list = new List<Group>();
            for (int k = 0; k < parts.Count; k++)
            {
                string which = parts.Count == 1 ? "The group" : "Group " + (k + 1) + " (\"" + parts[k] + "\")";
                if (parts[k].Length == 0) { problem = "Group " + (k + 1) + " is empty: there's a comma too many."; return null; }
                var m = GroupText.Match(parts[k]);
                if (!m.Success)
                {
                    problem = which + " isn't like xxx/0-999: an x for each digit, then a slash and the lowest and highest number, with a dash between them.";
                    return null;
                }
                int digits = m.Groups[1].Length;
                if (digits > MaxDigits) { problem = which + " has " + digits + " x's; a group can have " + MaxDigits + " at most."; return null; }
                int top = (int)Math.Pow(10, digits) - 1, min = 0, max = top;
                if (m.Groups[2].Success)
                {
                    long lo, hi;
                    if (!long.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out lo) || !long.TryParse(m.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out hi) || hi > top)
                    {
                        problem = which + " goes up to a number with more digits than it has x's (" + digits + ").";
                        return null;
                    }
                    if (lo > hi) { problem = which + " has its lowest number, " + lo + ", above its highest, " + hi + "."; return null; }
                    min = (int)lo;
                    max = (int)hi;
                }
                list.Add(new Group { Digits = digits, Min = min, Max = max });
            }
            return new NumberPattern(list);
        }

        /// <summary>The pattern written the usual way ("xxx/0-999,x/1-6"), or the text as it is when it isn't a pattern.</summary>
        public static string Tidy(string text)
        {
            string problem;
            var p = Parse(text, out problem);
            return p == null ? (text ?? "").Trim() : p.ToString();
        }

        public static bool IsPattern(string text)
        {
            string problem;
            return Parse(text, out problem) != null;
        }

        /// <summary>New random numbers: one for each group, padded with zeros to its digits, with a space between groups.</summary>
        public string Pick()
        {
            lock (rnd) return string.Join(" ", groups.Select(g => rnd.Next(g.Min, g.Max + 1).ToString("D" + g.Digits, CultureInfo.InvariantCulture)));
        }

        public override string ToString()
        {
            return string.Join(",", groups.Select(g => new string('x', g.Digits) + "/" + g.Min.ToString(CultureInfo.InvariantCulture) + "-" + g.Max.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Test copies: picks this many times and says, for each group, the lowest and highest numbers it picked and how
        /// many different ones.
        /// </summary>
        public string Stats(int times)
        {
            var seen = groups.Select(g => new List<string>()).ToList();
            for (int t = 0; t < times; t++)
            {
                var nums = Pick().Split(' ');
                for (int k = 0; k < nums.Length; k++) seen[k].Add(nums[k]);
            }
            return string.Join("; ", seen.Select((s, k) => "group " + (k + 1) + ": " + s.Min() + " to " + s.Max() + ", " + s.Distinct().Count() + " different of "
                + (groups[k].Max - groups[k].Min + 1) + ", all " + groups[k].Digits + " digits: " + s.All(x => x.Length == groups[k].Digits)));
        }
    }
}
