using System.Text.RegularExpressions;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Fills in placeholders in content text.
/// <list type="bullet">
/// <item><c>{name}</c>, <c>{fullname}</c>, <c>{year}</c> – the player</item>
/// <item><c>{t.name}</c>, <c>{t.full}</c>, <c>{t.role}</c> ("your brother"), <c>{t.Role}</c>, <c>{t.age}</c>,
/// <c>{t.he}</c> (he/she), <c>{t.him}</c> (him/her), <c>{t.his}</c> (his/her) – capitalise for sentence start.
/// Use <c>o.</c> for the other participant and <c>p.</c> for the player's partner.</item>
/// <item><c>{amount}</c> – any event variable, formatted as money</item>
/// </list>
/// </summary>
public static partial class TextFormatter
{
    [GeneratedRegex(@"\{(\w+)(?:\.(\w+))?\}")]
    private static partial Regex Token();

    [GeneratedRegex(@"\[\[(\d{4}):(.*?)\|\|(.*?)\]\]", RegexOptions.Singleline)]
    private static partial Regex Era();

    /// <summary>
    /// Text that follows the times: <c>[[2005: a letter || a text message]]</c> gives the first part
    /// before 2005 and the second from then on.
    /// </summary>
    public static string ByEra(int year, string text) =>
        Era().Replace(text, m => (year >= int.Parse(m.Groups[1].Value) ? m.Groups[3].Value : m.Groups[2].Value).Trim());

    [GeneratedRegex(@"\{money:(\d+)\}")]
    private static partial Regex MoneyToken();

    public static string Format(SimContext ctx, string text, PendingEvent? pending, Person? viewer = null)
    {
        var w = ctx.World;
        var player = w.Player;
        var perspective = viewer ?? player;
        text = ByEra(ctx.Year, text);
        // {money:20}: an amount in 2020 money, shown in the country's currency at this year's prices.
        text = MoneyToken().Replace(text, m => EconomySystem.Format(ctx, ctx.Nominal(double.Parse(m.Groups[1].Value))));
        return Token().Replace(text, m =>
        {
            string head = m.Groups[1].Value;
            string field = m.Groups[2].Success ? m.Groups[2].Value : "";

            if (field == "")
            {
                return head switch
                {
                    "name" => player.FirstName,
                    "fullname" => player.FullName,
                    "year" => ctx.Year.ToString(),
                    "grades" => player.Grades.ToString("0"),
                    "age" => player.Age(ctx.Year).ToString(),
                    "city" => HousingSystem.City(ctx, player).Name,
                    _ when pending != null && pending.Vars.TryGetValue(head, out var v) => EconomySystem.Format(ctx, ctx.Nominal(v)),
                    _ => m.Value
                };
            }

            Person? who = head.ToLowerInvariant() switch
            {
                "t" => w.TryGet(pending?.Roles.GetValueOrDefault("target")),
                "o" => w.TryGet(pending?.Roles.GetValueOrDefault("other")),
                "p" => w.TryGet(player.PartnerId),
                _ => null
            };
            if (who == null)
            {
                // The person is gone (e.g. a partner who left earlier this year). Fall back to neutral words.
                string fallback = field.ToLowerInvariant() switch
                {
                    "he" or "she" => "they",
                    "him" or "her" => "them",
                    "his" or "hers" => "their",
                    "role" => "someone",
                    _ => "someone"
                };
                return char.IsUpper(field[0]) || char.IsUpper(head[0]) ? Capitalize(fallback) : fallback;
            }
            bool male = who.Sex == Sex.Male;
            string value = field.ToLowerInvariant() switch
            {
                "name" => who.FirstName,
                "full" => who.FullName,
                "age" => who.Age(ctx.Year).ToString(),
                "role" => Kinship.Possessive(w, perspective, who),
                "he" or "she" => male ? "he" : "she",
                "him" or "her" => male ? "him" : "her",
                "his" or "hers" => male ? "his" : "her",
                _ => m.Value
            };
            return char.IsUpper(field[0]) || char.IsUpper(head[0]) ? Capitalize(value) : value;
        });
    }

    public static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
