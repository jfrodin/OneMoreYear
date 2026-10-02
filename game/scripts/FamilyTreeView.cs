using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// The graphical family tree: photo cards in generations around one person – grandparents above
/// their child, parents, siblings in birth order with the partner beside, children and grandchildren
/// below their parent – joined by lines. Choosing a card moves the tree to that person; choosing the
/// person in the middle opens their page. Every card is a button, so it works with a controller.
/// </summary>
public partial class FamilyTreeView : Control
{
    private const float CardW = 132, CardH = 156, Gap = 14, RowGap = 44, Margin = 12;

    private readonly GameSession _s;
    private readonly int _focusId;
    private readonly Action<int> _refocus, _open;
    private readonly Action<Control, string> _hint;
    private readonly List<(Vector2 A, Vector2 B)> _lines = new();
    private readonly List<(Vector2 A, Vector2 B)> _couples = new();
    private Button? _focusCard;

    public FamilyTreeView(GameSession s, int focusId, Action<int> refocus, Action<int> open, Action<Control, string> hint)
    {
        _s = s;
        _focusId = focusId;
        _refocus = refocus;
        _open = open;
        _hint = hint;
    }

    public Button? FocusCard => _focusCard;

    public override void _Ready()
    {
        var f = _s.FamilyFocus(_focusId);
        MouseFilter = MouseFilterEnum.Pass;

        // Rows, top to bottom. Each row is a list of groups: cards that belong together (a couple,
        // a set of siblings) and the x they would like to be centred on (filled in below).
        var rowGrand = f.Parents.Select(parent => f.Grandparents.TryGetValue(parent.Id, out var g) ? g : Array.Empty<TreePerson>()).ToList();
        var siblingsRow = new List<TreePerson>();
        foreach (var sib in f.Siblings)
        {
            siblingsRow.Add(sib);
            if (sib.Id == f.Focus.Id && f.Partner != null) siblingsRow.Add(f.Partner);
        }
        var grandchildren = f.Children.Select(c => f.Grandchildren.TryGetValue(c.Id, out var g) ? g : Array.Empty<TreePerson>()).ToList();

        bool hasGrand = rowGrand.Any(g => g.Count > 0), hasParents = f.Parents.Count > 0;
        bool hasChildren = f.Children.Count > 0, hasGrandchildren = grandchildren.Any(g => g.Count > 0);
        int row = 0;
        float Y(int r) => Margin + r * (CardH + RowGap);
        int rGrand = hasGrand ? row++ : -1;
        int rParents = hasParents ? row++ : -1;
        int rSiblings = row++;
        int rChildren = hasChildren ? row++ : -1;
        int rGrandchildren = hasGrandchildren ? row++ : -1;

        // Middle row first: it decides where everything else goes.
        var sibX = Place(new List<(int Count, float Want)> { (siblingsRow.Count, 0) })[0];
        var cardsX = new Dictionary<int, float>(); // card id -> left x
        for (int i = 0; i < siblingsRow.Count; i++) cardsX[siblingsRow[i].Id] = sibX + i * (CardW + Gap);
        float focusCenter = cardsX[f.Focus.Id] + CardW / 2;
        float coupleCenter = f.Partner != null ? focusCenter + (CardW + Gap) / 2 : focusCenter;
        var fullSiblings = f.Siblings.Where(x => !x.HalfSibling).Select(x => cardsX[x.Id] + CardW / 2).ToList();
        float siblingsCenter = fullSiblings.Count > 0 ? fullSiblings.Average() : focusCenter;

        var cards = new List<(TreePerson P, Vector2 Pos)>();
        foreach (var p in siblingsRow) cards.Add((p, new Vector2(cardsX[p.Id], Y(rSiblings))));

        // Parents above the siblings.
        var parentX = new Dictionary<int, float>();
        if (hasParents)
        {
            float left = Place(new List<(int, float)> { (f.Parents.Count, siblingsCenter) })[0];
            for (int i = 0; i < f.Parents.Count; i++)
            {
                parentX[f.Parents[i].Id] = left + i * (CardW + Gap);
                cards.Add((f.Parents[i], new Vector2(parentX[f.Parents[i].Id], Y(rParents))));
            }
            Couple(parentX.Values.ToList(), Y(rParents));
            var parentsBottom = new Vector2(parentX.Values.Average() + CardW / 2, Y(rParents) + CardH);
            foreach (var sib in f.Siblings)
                _lines.Add((parentsBottom, new Vector2(cardsX[sib.Id] + CardW / 2, Y(rSiblings))));
        }

        // Each parent's parents above that parent.
        if (hasGrand)
        {
            var groups = rowGrand.Select((g, i) => (g.Count, Want: parentX[f.Parents[i].Id] + CardW / 2)).ToList();
            var lefts = Place(groups);
            for (int gi = 0; gi < rowGrand.Count; gi++)
            {
                var xs = new List<float>();
                for (int i = 0; i < rowGrand[gi].Count; i++)
                {
                    float x = lefts[gi] + i * (CardW + Gap);
                    xs.Add(x);
                    cards.Add((rowGrand[gi][i], new Vector2(x, Y(rGrand))));
                }
                if (xs.Count == 0) continue;
                Couple(xs, Y(rGrand));
                _lines.Add((new Vector2(xs.Average() + CardW / 2, Y(rGrand) + CardH), new Vector2(parentX[f.Parents[gi].Id] + CardW / 2, Y(rParents))));
            }
        }

        // Children below the person (and partner), grandchildren below their parent.
        var childX = new Dictionary<int, float>();
        if (hasChildren)
        {
            float left = Place(new List<(int, float)> { (f.Children.Count, coupleCenter) })[0];
            for (int i = 0; i < f.Children.Count; i++)
            {
                childX[f.Children[i].Id] = left + i * (CardW + Gap);
                cards.Add((f.Children[i], new Vector2(childX[f.Children[i].Id], Y(rChildren))));
                _lines.Add((new Vector2(coupleCenter, Y(rSiblings) + CardH), new Vector2(childX[f.Children[i].Id] + CardW / 2, Y(rChildren))));
            }
        }
        if (hasGrandchildren)
        {
            var groups = grandchildren.Select((g, i) => (g.Count, Want: childX[f.Children[i].Id] + CardW / 2)).ToList();
            var lefts = Place(groups);
            for (int gi = 0; gi < grandchildren.Count; gi++)
                for (int i = 0; i < grandchildren[gi].Count; i++)
                {
                    float x = lefts[gi] + i * (CardW + Gap);
                    cards.Add((grandchildren[gi][i], new Vector2(x, Y(rGrandchildren))));
                    _lines.Add((new Vector2(childX[f.Children[gi].Id] + CardW / 2, Y(rChildren) + CardH), new Vector2(x + CardW / 2, Y(rGrandchildren))));
                }
        }
        if (f.Partner != null) _couples.Add((new Vector2(cardsX[f.Focus.Id] + CardW, Y(rSiblings) + CardH * 0.35f), new Vector2(cardsX[f.Partner.Id], Y(rSiblings) + CardH * 0.35f)));

        // Everything can end up left of zero; shift so the leftmost card sits at the margin.
        float minX = cards.Min(c => c.Pos.X);
        float shift = Margin - minX;
        float maxX = cards.Max(c => c.Pos.X) + shift + CardW + Margin;
        for (int i = 0; i < _lines.Count; i++) _lines[i] = (_lines[i].A + new Vector2(shift, 0), _lines[i].B + new Vector2(shift, 0));
        for (int i = 0; i < _couples.Count; i++) _couples[i] = (_couples[i].A + new Vector2(shift, 0), _couples[i].B + new Vector2(shift, 0));
        CustomMinimumSize = new Vector2(maxX, Y(row) - RowGap + Margin);

        foreach (var (p, pos) in cards)
        {
            var card = Card(p, p.Id == f.Focus.Id);
            card.Position = pos + new Vector2(shift, 0);
            AddChild(card);
            if (p.Id == f.Focus.Id) _focusCard = card;
        }
    }

    /// <summary>Lays out groups of cards left to right near the x they want, without overlapping. Returns each group's left x.</summary>
    private static List<float> Place(List<(int Count, float Want)> groups)
    {
        var lefts = new List<float>();
        float end = float.MinValue;
        foreach (var (count, want) in groups)
        {
            float width = Math.Max(0, count * (CardW + Gap) - Gap);
            float left = Math.Max(want - width / 2, end == float.MinValue ? float.MinValue : end + Gap * 3);
            lefts.Add(left);
            if (count > 0) end = left + width;
        }
        // Keep the whole row balanced around where it wanted to be.
        var real = groups.Select((g, i) => (g, i)).Where(x => x.g.Count > 0).ToList();
        if (real.Count > 0)
        {
            float drift = real.Average(x => lefts[x.i] + (x.g.Count * (CardW + Gap) - Gap) / 2 - x.g.Want);
            for (int i = 0; i < lefts.Count; i++) lefts[i] -= drift;
        }
        return lefts;
    }

    private void Couple(List<float> xs, float y)
    {
        if (xs.Count == 2) _couples.Add((new Vector2(xs[0] + CardW, y + CardH * 0.35f), new Vector2(xs[1], y + CardH * 0.35f)));
    }

    private Button Card(TreePerson p, bool isFocus)
    {
        var b = new Button { CustomMinimumSize = new Vector2(CardW, CardH), Size = new Vector2(CardW, CardH), FocusMode = FocusModeEnum.All };
        var style = UiTheme.PaperCard(8);
        if (p.IsPlayer) { style.BorderColor = UiTheme.Accent; style.SetBorderWidthAll(2); }
        if (isFocus) { style.BgColor = UiTheme.Panel.Lightened(0.35f); style.ShadowSize = 10; }
        b.AddThemeStyleboxOverride("normal", style);
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BorderColor = UiTheme.Accent;
        hover.SetBorderWidthAll(2);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", hover);

        var col = Ui.VBox(2);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.OffsetLeft = col.OffsetTop = 8;
        col.OffsetRight = col.OffsetBottom = -8;
        var photoRow = new CenterContainer();
        photoRow.AddChild(Portrait.Create(_s.Portrait(p.Id), p.IsPlayer, 64));
        col.AddChild(photoRow);
        var name = Ui.Label(p.Name, 14, p.Alive ? UiTheme.Text : UiTheme.Muted, wrap: true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(name);
        var years = Ui.Label(p.Years + (p.Alive ? "" : "  †"), 12, UiTheme.Muted);
        years.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(years);
        var rel = UiTheme.HandLabel(p.IsPlayer ? "you" : p.Relation + (p.HalfSibling ? " (half)" : ""), 16, UiTheme.AccentDark);
        rel.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(rel);
        b.AddChild(col);
        Ui.PassMouse(b);

        int id = p.Id;
        b.Pressed += () => { if (isFocus) _open(id); else _refocus(id); };
        _hint(b, isFocus ? $"{p.Name}: press to open the page about them." : $"{p.Name}: press to see the family from their place in it.");
        return b;
    }

    public override void _Draw()
    {
        var ink = UiTheme.Border.Darkened(0.1f);
        foreach (var (a, b) in _lines)
        {
            // Down, across, down: the classic family-tree elbow.
            float mid = (a.Y + b.Y) / 2;
            DrawPolyline(new[] { a, new Vector2(a.X, mid), new Vector2(b.X, mid), b }, ink, 2, true);
        }
        foreach (var (a, b) in _couples)
        {
            DrawLine(a, b, UiTheme.Accent, 2, true);
            DrawCircle((a + b) / 2, 4, UiTheme.Accent);
        }
    }
}
