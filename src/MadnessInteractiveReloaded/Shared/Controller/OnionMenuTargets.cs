using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Walgelijk;
using Walgelijk.Onion;
using Walgelijk.Onion.Controls;

namespace MIR;

/// <summary>
/// <see cref="IMenuTargets"/> over the Onion UI tree, as it stood after Onion's last update.
/// </summary>
public sealed class OnionMenuTargets : IMenuTargets
{
    // Onion's Slider keeps its value and range private. Read them to nudge from the current value; null means the nudge
    // falls back to starting at the cursor.
    private static readonly FieldInfo? sliderStates = typeof(Slider).GetField("states", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo? sliderRange = typeof(Slider).GetField("range", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Onion's own rule for what keyboard navigation can focus (alive last frame, captures hover, a hit test rect with an
    /// area), minus pure layout containers and images, which capture hover only to block what is behind them. A control
    /// scrolled out of its scroll view has lost its hit test rect, but still counts, so navigation can scroll to it.
    /// <para>
    /// Open things are modal: while a dropdown is open, only its options; while a confirmation dialog is open, only its
    /// window. The controls behind keep their hit test rects, so without this the focus would land on them under the
    /// list. A modal with nothing to focus, like a key capture (which also uses Onion's triggered control), limits nothing.
    /// </para>
    /// </summary>
    public IEnumerable<(int Id, Rect Rect)> Live()
    {
        if (ModalRoots() is List<int> roots)
        {
            var scoped = Collect(roots);
            if (scoped.Count > 0)
                return scoped;
        }
        return Collect(null);
    }

    public int? LiveAncestorOf(int id)
    {
        if (!Onion.Tree.Nodes.TryGetValue(id, out var node))
            return null;

        var live = new HashSet<int>();
        foreach (var (liveId, _) in Live())
            live.Add(liveId);

        for (var n = node.Parent; n != null; n = n.Parent)
            if (live.Contains(n.Identity))
                return n.Identity;
        return null;
    }

    /// <remarks>
    /// <see cref="Navigator.FindInDirection"/> would also land on the groups the menus nest every control in, so this
    /// applies the same rule to <see cref="Live"/> instead.
    /// </remarks>
    public int? FindInDirection(int from, Vector2 dir)
    {
        if (!Onion.Tree.Instances.TryGetValue(from, out var origin))
            return null;
        return MenuNavigator.NearestInDirection(Live(), from, origin.Rects.ComputedGlobal.GetCenter(), dir);
    }

    public int? Focused
    {
        get => Onion.Navigator.FocusedControl;
        set => Onion.Navigator.FocusedControl = value;
    }

    public void Highlight(int id) => Onion.Navigator.HighlightControl(id, 0.25f);

    public Rect? ViewportOf(int id)
    {
        if (!Onion.Tree.Nodes.TryGetValue(id, out var node))
            return null;
        if (EnclosingScrollView(node) is Node view && Onion.Tree.Instances.TryGetValue(view.Identity, out var inst))
            return inst.Rects.ComputedGlobal;
        var size = Game.Main.Window.Size;
        return new Rect(0, 0, size.X, size.Y);
    }

    /// <remarks>
    /// The menus only use horizontal sliders, through <c>Ui.FloatSlider</c> and <c>Ui.IntSlider</c>.
    /// </remarks>
    public bool IsSlider(int id) => Onion.Tree.Nodes.TryGetValue(id, out var node) && node.Behaviour is Slider;

    public float? SliderFraction(int id)
    {
        if (!Onion.Tree.Nodes.TryGetValue(id, out var node) || node.Behaviour is not Slider)
            return null;
        if (sliderStates?.GetValue(null) is not OptionalControlState<float> states || !states.TryGetState(id, out var value))
            return null;
        if (sliderRange?.GetValue(node.Behaviour) is not MinMax<float> range || range.Max <= range.Min)
            return null;
        return float.Clamp((value - range.Min) / (range.Max - range.Min), 0, 1);
    }

    /// <remarks>
    /// A popup is Onion's triggered control with targets of its own inside it: an open dropdown. A key capture is
    /// triggered too, but has nothing inside it.
    /// </remarks>
    public (int Id, Rect Rect)? ClosePopup()
    {
        if (Onion.Navigator.TriggeredControl is not int triggered || !Onion.Tree.IsAlive(triggered)
            || Collect([triggered]).Count == 0 || !Onion.Tree.Instances.TryGetValue(triggered, out var inst))
            return null;

        Onion.Navigator.TriggeredControl = null;
        return (triggered, inst.Rects.ComputedGlobal);
    }

    /// <remarks>
    /// The last open dialog's "No" (<see cref="ConfirmationDialogComponent.CancelNode"/>), else the first target in its window.
    /// A known "No" that isn't live yet (its layout lands a frame after the window's close button) is waited for.
    /// </remarks>
    public int? DialogFocus()
    {
        if (Game.Main?.Scene is not Scene scene)
            return null;

        ConfirmationDialogComponent? open = null;
        foreach (var dialog in scene.GetAllComponentsOfType<ConfirmationDialogComponent>())
            if (dialog.WindowNode is int window && Onion.Tree.IsAlive(window))
                open = dialog;
        if (open?.WindowNode is not int root)
            return null;

        var inside = Collect([root]);
        if (open.CancelNode is int cancel)
            return inside.Exists(t => t.Id == cancel) ? cancel : null;
        return inside.Count > 0 ? inside[0].Id : null;
    }

    /// <remarks>
    /// Onion's own rule for what the wheel scrolls: the topmost control under the mouse that captures scroll.
    /// </remarks>
    public bool ScrollsAt(Vector2 point) => Onion.Navigator.Raycast(point, CaptureFlags.Scroll) != null;

    public Rect? LargestScrollView()
    {
        var roots = ModalRoots();
        var size = Game.Main.Window.Size;
        var window = new Rect(0, 0, size.X, size.Y);

        Rect? best = null;
        float bestArea = 0;
        foreach (var (id, node) in Onion.Tree.Nodes)
        {
            if (node.Behaviour is not ScrollView || !node.AliveLastFrame || (roots != null && !IsInside(node, roots))
                || !Onion.Tree.Instances.TryGetValue(id, out var inst) || inst.Rects.Raycast is not Rect hit)
                continue;

            var visible = hit.Intersect(window);
            float area = visible.Width > 0 && visible.Height > 0 ? visible.Area : 0;
            if (area > bestArea)
            {
                best = visible;
                bestArea = area;
            }
        }
        return best;
    }

    /// <summary>
    /// The nodes whose descendants are the only targets right now: an open dropdown, else the open confirmation dialogs'
    /// windows. Null when nothing is open.
    /// </summary>
    private static List<int>? ModalRoots()
    {
        if (Onion.Navigator.TriggeredControl is int triggered && Onion.Tree.IsAlive(triggered))
            return [triggered];

        List<int>? roots = null;
        if (Game.Main?.Scene is Scene scene)
            foreach (var dialog in scene.GetAllComponentsOfType<ConfirmationDialogComponent>())
                if (dialog.WindowNode is int window && Onion.Tree.IsAlive(window))
                    (roots ??= []).Add(window);
        return roots;
    }

    /// <summary>
    /// Every target, or with <paramref name="roots"/> only those inside one of them.
    /// </summary>
    private static List<(int Id, Rect Rect)> Collect(List<int>? roots)
    {
        var found = new List<(int, Rect)>();
        foreach (var (id, node) in Onion.Tree.Nodes)
            if ((roots == null || IsInside(node, roots)) && TryGetTarget(id, node, out var rect))
                found.Add((id, rect));
        return found;
    }

    private static bool IsInside(Node node, List<int> roots)
    {
        for (var n = node.Parent; n != null; n = n.Parent)
            if (roots.Contains(n.Identity))
                return true;
        return false;
    }

    private static bool TryGetTarget(int id, Node node, out Rect rect)
    {
        rect = default;
        if (!node.AliveLastFrame || IsContainer(node.Behaviour)
            || !Onion.Tree.Instances.TryGetValue(id, out var inst)
            || !inst.CaptureFlags.HasFlag(CaptureFlags.Hover))
            return false;

        rect = inst.Rects.ComputedGlobal;
        if (inst.Rects.Raycast is Rect hit && hit.Area > float.Epsilon)
            return true;
        return rect.Area > float.Epsilon && EnclosingScrollView(node) != null;
    }

    private static bool IsContainer(IControl behaviour)
        => behaviour is Dummy or Group or ScrollView or DragWindow or Image;

    private static Node? EnclosingScrollView(Node node)
    {
        for (var n = node.Parent; n != null; n = n.Parent)
            if (n.Behaviour is ScrollView)
                return n;
        return null;
    }
}
