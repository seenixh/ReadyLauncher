using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The colors of parts of the launcher that can be chosen in Settings → Appearance: the buttons at the top (and the
    /// quick launch strip's round button), the volume control, the names under icons, and the notes window's title and
    /// tabs. Each can be its own color, all can share one, or each stays automatic (readable on what's behind it).
    /// </summary>
    internal partial class LauncherWindow
    {
        /// <summary>A part's chosen color (or the shared one when "same color for all" is on), else <paramref name="auto"/>.</summary>
        Color PartColor(string own, Color auto)
        {
            string chosen = S.SameColorForAll ? S.AllPartsColor : own;
            Color c;
            return NoteDocs.TryColor(chosen, out c) ? Color.FromRgb(c.R, c.G, c.B) : auto;
        }

        Color ButtonsColor { get { return PartColor(S.ButtonsColor, fgColor); } }
        Color IconTextColor { get { return PartColor(S.IconTextColor, fgColor); } }
        /// <summary>The volume control inside the launcher sits on the launcher's background (picture and all); outside, on its color.</summary>
        Color VolumeColor(bool outside) { return PartColor(S.VolumeColor, outside ? TextColorFor(Luminance(bg)) : fgColor); }
        Color NotesChromeColor { get { return PartColor(S.NotesChromeColor, TextColorFor(Luminance(bg))); } }
    }
}
