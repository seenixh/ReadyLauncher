using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// Turns a saved notes tab into a document the notes editor shows, and back. Only the formatting the notes use is
    /// kept (bold, italic, underline, strikethrough, size, color, highlight, bullets, numbering, checkboxes), and it's
    /// read field by field, so nothing in a notes file or an imported backup is ever run as code.
    /// </summary>
    internal static class NoteDocs
    {
        /// <summary>Marks the checkboxes that belong to the notes (Tag of the CheckBox).</summary>
        public static readonly object CheckTag = new object();

        public static FlowDocument NewDocument(double fontSize, Color fg)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0), FontSize = fontSize, Foreground = new SolidColorBrush(fg),
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), TextAlignment = TextAlignment.Left
            };
            var para = new Style(typeof(Paragraph));
            para.Setters.Add(new Setter(Block.MarginProperty, new Thickness(0)));
            doc.Resources.Add(typeof(Paragraph), para);
            var list = new Style(typeof(List));
            list.Setters.Add(new Setter(Block.MarginProperty, new Thickness(0)));
            list.Setters.Add(new Setter(Block.PaddingProperty, new Thickness(Math.Round(fontSize * 1.5), 0, 0, 0)));
            doc.Resources.Add(typeof(List), list);
            doc.Resources.Add(typeof(Hyperlink), LinkStyle(fg));
            return doc;
        }

        /// <summary>How links look: blue and underlined, light blue on dark notes. (A style, so it's never saved as the text's own color.)</summary>
        public static Style LinkStyle(Color text)
        {
            bool darkArea = (0.299 * text.R + 0.587 * text.G + 0.114 * text.B) / 255 > 0.5;   // light text = dark background
            var style = new Style(typeof(Hyperlink));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, new SolidColorBrush(darkArea ? Color.FromRgb(0x8A, 0xC4, 0xFF) : Color.FromRgb(0x0B, 0x5C, 0xC9))));
            style.Setters.Add(new Setter(Inline.TextDecorationsProperty, TextDecorations.Underline));
            style.Setters.Add(new Setter(FrameworkContentElement.ToolTipProperty, "Click to open"));
            return style;
        }

        public static FlowDocument Build(NoteTab tab, double fontSize, Color fg, Action edited)
        {
            var doc = NewDocument(fontSize, fg);
            // The lists open at each indent level (0 = not indented), so indented lines go back under the line above.
            var open = new List<List>();
            foreach (var np in tab.Paras)
            {
                var p = new Paragraph();
                if (np.Align == "center") p.TextAlignment = TextAlignment.Center;
                else if (np.Align == "right") p.TextAlignment = TextAlignment.Right;
                else if (np.Align == "justify") p.TextAlignment = TextAlignment.Justify;
                if (np.Check) p.Inlines.Add(MakeCheck(np.Done, fontSize, edited));
                foreach (var r in np.Runs) AddRun(p, r);
                if (np.List != "bullet" && np.List != "number")
                {
                    open.Clear();
                    doc.Blocks.Add(p);
                    continue;
                }
                int level = Math.Max(0, Math.Min(np.Level, open.Count));   // at most one deeper than the line above
                while (open.Count > level + 1) open.RemoveAt(open.Count - 1);
                if (open.Count == level + 1 && IsNumbered(open[level].MarkerStyle) != (np.List == "number")) open.RemoveAt(level);
                if (open.Count == level)
                {
                    var list = new List { MarkerStyle = np.List == "number" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
                    if (level == 0) doc.Blocks.Add(list);
                    else open[level - 1].ListItems.LastListItem.Blocks.Add(list);   // inside the line above
                    open.Add(list);
                }
                open[level].ListItems.Add(new ListItem(p));
            }
            if (doc.Blocks.Count == 0) doc.Blocks.Add(new Paragraph());
            foreach (var p in Paragraphs(doc)) Linkify(p, tab.NoLinks, null);   // web addresses become links again
            return doc;
        }

        static void AddRun(Paragraph p, NoteRun r)
        {
            string[] parts = (r.T ?? "").Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) p.Inlines.Add(new LineBreak());   // Shift+Enter inside a paragraph
                if (parts[i].Length == 0) continue;
                var run = new Run(parts[i]);
                if (r.B) run.FontWeight = FontWeights.Bold;
                if (r.I) run.FontStyle = FontStyles.Italic;
                if (r.U || r.S) run.TextDecorations = Decorations(r.U, r.S);
                if (r.Size > 0) run.FontSize = Math.Max(4, Math.Min(200, r.Size));
                Color c;
                if (TryColor(r.Color, out c)) run.Foreground = new SolidColorBrush(c);
                if (TryColor(r.Back, out c)) run.Background = new SolidColorBrush(c);
                p.Inlines.Add(run);
            }
        }

        // ---------- document -> saved notes ----------

        public static List<NotePara> Read(FlowDocument doc, double fontSize, Color fg)
        {
            var paras = new List<NotePara>();
            foreach (var b in doc.Blocks) ReadBlock(b, "", -1, paras, fontSize, fg);
            return paras;
        }

        /// <param name="level">Indent level of the list the block is in (-1 = not in a list).</param>
        static void ReadBlock(Block b, string list, int level, List<NotePara> paras, double size, Color fg)
        {
            var p = b as Paragraph;
            if (p != null) { paras.Add(ReadParagraph(p, list, Math.Max(0, level), size, fg)); return; }
            var l = b as List;
            if (l != null)
            {
                string kind = IsNumbered(l.MarkerStyle) ? "number" : "bullet";
                foreach (var item in l.ListItems)
                    foreach (var child in item.Blocks) ReadBlock(child, kind, level + 1, paras, size, fg);   // a list inside a list is one deeper
                return;
            }
            var section = b as Section;
            if (section != null) { foreach (var child in section.Blocks) ReadBlock(child, list, level, paras, size, fg); return; }
            var table = b as Table;
            if (table != null)
                foreach (var group in table.RowGroups)
                    foreach (var row in group.Rows)
                        foreach (var cell in row.Cells)
                            foreach (var child in cell.Blocks) ReadBlock(child, list, level, paras, size, fg);
        }

        public static bool IsNumbered(TextMarkerStyle m)
        {
            return m == TextMarkerStyle.Decimal || m == TextMarkerStyle.LowerLatin || m == TextMarkerStyle.UpperLatin
                || m == TextMarkerStyle.LowerRoman || m == TextMarkerStyle.UpperRoman;
        }

        static NotePara ReadParagraph(Paragraph p, string list, int level, double size, Color fg)
        {
            var np = new NotePara { List = list, Level = list.Length > 0 ? level : 0 };
            if (p.TextAlignment == TextAlignment.Center) np.Align = "center";
            else if (p.TextAlignment == TextAlignment.Right) np.Align = "right";
            else if (p.TextAlignment == TextAlignment.Justify) np.Align = "justify";
            ReadInlines(p.Inlines, np, size, fg);
            return np;
        }

        static void ReadInlines(InlineCollection inlines, NotePara np, double size, Color fg)
        {
            foreach (var inline in inlines)
            {
                var run = inline as Run;
                if (run != null)
                {
                    if (!string.IsNullOrEmpty(run.Text)) Append(np, ReadRun(run, size, fg));
                    continue;
                }
                if (inline is LineBreak) { Append(np, new NoteRun { T = "\n" }); continue; }
                var span = inline as Span;
                if (span != null) { ReadInlines(span.Inlines, np, size, fg); continue; }
                var ui = inline as InlineUIContainer;
                var cb = ui == null ? null : ui.Child as CheckBox;
                if (cb != null && cb.Tag == CheckTag && !np.Check) { np.Check = true; np.Done = cb.IsChecked == true; }
            }
        }

        /// <summary>Adds a run, joined to the one before when they look the same (the editor splits text into many pieces).</summary>
        static void Append(NotePara np, NoteRun r)
        {
            var last = np.Runs.Count > 0 ? np.Runs[np.Runs.Count - 1] : null;
            if (last != null && r.T != "\n" && last.T != "\n" && last.B == r.B && last.I == r.I && last.U == r.U && last.S == r.S
                && Math.Abs(last.Size - r.Size) < 0.01 && last.Color == r.Color && last.Back == r.Back)
            {
                last.T += r.T;
                return;
            }
            np.Runs.Add(r);
        }

        static NoteRun ReadRun(Run run, double size, Color fg)
        {
            var r = new NoteRun { T = run.Text };
            r.B = run.FontWeight.ToOpenTypeWeight() >= 600;
            r.I = run.FontStyle != FontStyles.Normal;
            bool u, s;
            DecorationsOf(run, out u, out s);
            r.U = u;
            r.S = s;
            if (Math.Abs(run.FontSize - size) > 0.05) r.Size = Math.Round(run.FontSize, 1);
            Color own;
            if (OwnColor(run, out own) && own != fg) r.Color = own.ToString();
            Color back;
            if (BackgroundOf(run, out back)) r.Back = back.ToString();
            return r;
        }

        // ---------- formatting helpers ----------

        public static TextDecorationCollection Decorations(bool underline, bool strike)
        {
            var c = new TextDecorationCollection();
            if (underline) foreach (var d in TextDecorations.Underline) c.Add(d);
            if (strike) foreach (var d in TextDecorations.Strikethrough) c.Add(d);
            c.Freeze();
            return c;
        }

        public static void Flags(TextDecorationCollection c, out bool underline, out bool strike)
        {
            underline = strike = false;
            if (c == null) return;
            foreach (var d in c)
            {
                if (d.Location == TextDecorationLocation.Underline) underline = true;
                if (d.Location == TextDecorationLocation.Strikethrough) strike = true;
            }
        }

        /// <summary>Underline / strikethrough on a piece of text, including what the spans and paragraph around it add.</summary>
        public static void DecorationsOf(TextElement e, out bool underline, out bool strike)
        {
            underline = strike = false;
            DependencyObject d = e;
            while (d != null)
            {
                TextDecorationCollection c = null;
                var inline = d as Inline;
                var para = d as Paragraph;
                // A link's own underline comes from its style: only count one set on the link by hand.
                if (inline is Hyperlink) c = inline.ReadLocalValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
                else if (inline != null) c = inline.TextDecorations;
                else if (para != null) c = para.TextDecorations;
                bool u, s;
                Flags(c, out u, out s);
                underline |= u;
                strike |= s;
                if (para != null) break;
                var fce = d as FrameworkContentElement;
                d = fce == null ? null : fce.Parent;
            }
        }

        /// <summary>The highlight behind a piece of text (backgrounds aren't inherited, so look at what's around it).</summary>
        static bool BackgroundOf(TextElement e, out Color color)
        {
            color = Colors.Transparent;
            DependencyObject d = e;
            while (d != null)
            {
                var te = d as TextElement;
                var b = te == null ? null : te.Background as SolidColorBrush;
                if (b != null && b.Color.A > 0) { color = b.Color; return true; }
                if (d is Paragraph) break;
                var fce = d as FrameworkContentElement;
                d = fce == null ? null : fce.Parent;
            }
            return false;
        }

        /// <summary>
        /// A color given to the text itself (or to the spans or paragraph around it), as opposed to the notes' own text color
        /// or a link's color, which come from the document and its styles.
        /// </summary>
        static bool OwnColor(TextElement e, out Color color)
        {
            color = Colors.Transparent;
            DependencyObject d = e;
            while (d != null)
            {
                var b = d.ReadLocalValue(TextElement.ForegroundProperty) as SolidColorBrush;
                if (b != null) { color = b.Color; return true; }
                if (d is Paragraph) break;
                var fce = d as FrameworkContentElement;
                d = fce == null ? null : fce.Parent;
            }
            return false;
        }

        // ---------- links ----------

        // Web addresses: http(s)://..., www...., or a name ending in a common domain like youtube.com (with an optional /path).
        static readonly Regex UrlPattern = new Regex(
            @"\b(?:https?://[^\s<>""]+|www\.[^\s<>""]+|[a-z0-9][a-z0-9-]*(?:\.[a-z0-9][a-z0-9-]*)*\." +
            @"(?:com|org|net|edu|gov|mil|int|io|co|us|uk|ca|au|nz|ie|de|fr|nl|be|es|it|se|no|dk|fi|ch|at|pl|jp|in|br|mx|" +
            @"info|biz|app|dev|ai|tv|me|ly|gg|xyz|online|site|tech|store|blog|news)\b(?:/[^\s<>""]*)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Where the web addresses in some text are (start, length), without trailing punctuation or email addresses.</summary>
        public static List<Tuple<int, int>> FindUrls(string text)
        {
            var found = new List<Tuple<int, int>>();
            if (string.IsNullOrEmpty(text)) return found;
            foreach (Match m in UrlPattern.Matches(text))
            {
                if (m.Index > 0 && (text[m.Index - 1] == '@' || text[m.Index - 1] == '.' || text[m.Index - 1] == '/')) continue;
                int len = m.Length;
                while (len > 0)
                {
                    char c = m.Value[len - 1];
                    string so = m.Value.Substring(0, len);
                    if (".,;:!?'\"]}>".IndexOf(c) >= 0) { len--; continue; }
                    if (c == ')' && so.Count(ch => ch == '(') < so.Count(ch => ch == ')')) { len--; continue; }
                    break;
                }
                if (len < 4 || m.Value.Substring(0, len).EndsWith("://")) continue;
                found.Add(Tuple.Create(m.Index, len));
            }
            return found;
        }

        static bool IsWholeUrl(string text)
        {
            var urls = FindUrls(text);
            return urls.Count == 1 && urls[0].Item1 == 0 && urls[0].Item2 == text.Length;
        }

        /// <summary>The address to open for a link's text (https:// added when it has none); null if it isn't a web address.</summary>
        public static string UrlFor(string text)
        {
            text = (text ?? "").Trim();
            if (!IsWholeUrl(text)) return null;
            string url = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? text : "https://" + text;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
            return uri.AbsoluteUri;
        }

        public static string LinkText(Hyperlink link) { return new TextRange(link.ContentStart, link.ContentEnd).Text; }

        static bool IsPlain(ICollection<string> noLinks, string text)
        {
            return noLinks != null && noLinks.Any(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        }

        public static List<Hyperlink> Links(Paragraph p)
        {
            var found = new List<Hyperlink>();
            CollectLinks(p.Inlines, found);
            return found;
        }

        static void CollectLinks(InlineCollection inlines, List<Hyperlink> found)
        {
            foreach (var i in inlines)
            {
                var link = i as Hyperlink;
                if (link != null) { found.Add(link); continue; }
                var span = i as Span;
                if (span != null) CollectLinks(span.Inlines, found);
            }
        }

        /// <summary>The link at a position in the text, if any.</summary>
        public static Hyperlink LinkAt(TextPointer pos)
        {
            DependencyObject d = pos == null ? null : pos.Parent;
            while (d != null && !(d is Paragraph))
            {
                var link = d as Hyperlink;
                if (link != null) return link;
                var fce = d as FrameworkContentElement;
                d = fce == null ? null : fce.Parent;
            }
            return null;
        }

        /// <summary>A web address at a position that isn't a link (for "make it a link"), or null.</summary>
        public static string PlainUrlAt(TextPointer pos)
        {
            var run = pos == null ? null : pos.Parent as Run;
            if (run == null || LinkAt(pos) != null) return null;
            int offset = run.ContentStart.GetOffsetToPosition(pos);
            foreach (var u in FindUrls(run.Text))
                if (offset >= u.Item1 && offset <= u.Item1 + u.Item2) return run.Text.Substring(u.Item1, u.Item2);
            return null;
        }

        /// <summary>Turns a link back into ordinary text (its formatting stays).</summary>
        public static void Unwrap(Hyperlink link)
        {
            var para = link.Parent as Paragraph;
            var span = link.Parent as Span;
            var inlines = para != null ? para.Inlines : span != null ? span.Inlines : null;
            if (inlines == null) return;
            foreach (var child in link.Inlines.ToList())
            {
                link.Inlines.Remove(child);
                inlines.InsertBefore(link, child);
            }
            inlines.Remove(link);
        }

        /// <summary>
        /// Keeps a paragraph's links in step with its text: web addresses become links (not one that was made plain, and not
        /// the one the cursor is still typing at), and links whose text is no longer a web address become plain text.
        /// </summary>
        public static bool Linkify(Paragraph p, ICollection<string> noLinks, TextPointer caret)
        {
            bool changed = false;
            foreach (var link in Links(p))
            {
                string text = LinkText(link);
                if (!IsWholeUrl(text) || IsPlain(noLinks, text)) { Unwrap(link); changed = true; }
            }
            for (int guard = 0; guard < 200; guard++)
            {
                bool wrapped = false;
                foreach (var run in Runs(p))
                {
                    if (LinkAt(run.ContentStart) != null) continue;
                    string text = run.Text;
                    foreach (var u in FindUrls(text))
                    {
                        if (IsPlain(noLinks, text.Substring(u.Item1, u.Item2))) continue;
                        var start = run.ContentStart.GetPositionAtOffset(u.Item1);
                        var end = run.ContentStart.GetPositionAtOffset(u.Item1 + u.Item2);
                        if (start == null || end == null) continue;
                        if (caret != null && caret.Paragraph == p && caret.CompareTo(start) >= 0 && caret.CompareTo(end) <= 0) continue;
                        new Hyperlink(start, end);
                        wrapped = changed = true;
                        break;
                    }
                    if (wrapped) break;
                }
                if (!wrapped) break;
            }
            return changed;
        }

        public static bool TryColor(string s, out Color c)
        {
            c = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(s)) return false;
            try { c = (Color)ColorConverter.ConvertFromString(s.Trim()); return true; }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Joins lists that sit right next to each other and are the same kind (bulleted or numbered), so numbering lines
        /// one at a time counts 1, 2, 3 instead of starting again at 1. Returns whether anything changed.
        /// </summary>
        public static bool JoinLists(FlowDocument doc)
        {
            bool changed = false;
            var b = doc.Blocks.FirstBlock;
            while (b != null)
            {
                var list = b as List;
                var next = b.NextBlock as List;
                if (list != null && next != null && IsNumbered(list.MarkerStyle) == IsNumbered(next.MarkerStyle))
                {
                    foreach (var item in next.ListItems.ToList())
                    {
                        next.ListItems.Remove(item);
                        list.ListItems.Add(item);
                    }
                    doc.Blocks.Remove(next);
                    changed = true;
                    continue;   // the list may now touch another one
                }
                b = b.NextBlock;
            }
            return changed;
        }

        // ---------- walking the document ----------

        public static IEnumerable<Paragraph> Paragraphs(FlowDocument doc)
        {
            var found = new List<Paragraph>();
            foreach (var b in doc.Blocks) CollectParagraphs(b, found);
            return found;
        }

        static void CollectParagraphs(Block b, List<Paragraph> found)
        {
            var p = b as Paragraph;
            if (p != null) { found.Add(p); return; }
            var l = b as List;
            if (l != null) { foreach (var item in l.ListItems) foreach (var child in item.Blocks) CollectParagraphs(child, found); return; }
            var section = b as Section;
            if (section != null) foreach (var child in section.Blocks) CollectParagraphs(child, found);
            var table = b as Table;
            if (table != null)
                foreach (var group in table.RowGroups) foreach (var row in group.Rows) foreach (var cell in row.Cells)
                    foreach (var child in cell.Blocks) CollectParagraphs(child, found);
        }

        public static IEnumerable<Run> Runs(Paragraph p)
        {
            var found = new List<Run>();
            CollectRuns(p.Inlines, found);
            return found;
        }

        static void CollectRuns(InlineCollection inlines, List<Run> found)
        {
            foreach (var i in inlines)
            {
                var run = i as Run;
                if (run != null) { found.Add(run); continue; }
                var span = i as Span;
                if (span != null) CollectRuns(span.Inlines, found);
            }
        }

        public static string Text(Paragraph p)
        {
            return string.Concat(Runs(p).Select(r => r.Text));
        }

        /// <summary>Where some text appears in a paragraph (ignoring case), even across differently formatted pieces.</summary>
        public static List<Tuple<TextPointer, TextPointer>> FindText(Paragraph p, string query)
        {
            var hits = new List<Tuple<TextPointer, TextPointer>>();
            if (string.IsNullOrEmpty(query)) return hits;
            var runs = Runs(p).ToList();
            var starts = new List<int>();
            var all = new System.Text.StringBuilder();
            foreach (var r in runs) { starts.Add(all.Length); all.Append(r.Text); }
            string text = all.ToString();
            int at = 0;
            while ((at = text.IndexOf(query, at, StringComparison.CurrentCultureIgnoreCase)) >= 0)
            {
                var s = PointerAt(runs, starts, at, false);
                var e = PointerAt(runs, starts, at + query.Length, true);
                if (s != null && e != null) hits.Add(Tuple.Create(s, e));
                at += Math.Max(1, query.Length);
            }
            return hits;
        }

        /// <summary>The position of a character offset in a paragraph's text (the end of a piece, or the start of the next).</summary>
        static TextPointer PointerAt(List<Run> runs, List<int> starts, int offset, bool isEnd)
        {
            for (int i = 0; i < runs.Count; i++)
            {
                int len = runs[i].Text.Length;
                bool inside = isEnd ? offset > starts[i] && offset <= starts[i] + len : offset >= starts[i] && offset < starts[i] + len;
                if (inside) return runs[i].ContentStart.GetPositionAtOffset(offset - starts[i]);
            }
            return null;
        }

        // ---------- checkboxes ----------

        public static InlineUIContainer CheckOf(Paragraph p)
        {
            return FindCheck(p.Inlines);
        }

        static InlineUIContainer FindCheck(InlineCollection inlines)
        {
            foreach (var i in inlines)
            {
                var ui = i as InlineUIContainer;
                if (ui != null && ui.Child is CheckBox && ((CheckBox)ui.Child).Tag == CheckTag) return ui;
                var span = i as Span;
                if (span != null) { var inner = FindCheck(span.Inlines); if (inner != null) return inner; }
            }
            return null;
        }

        /// <summary>
        /// The notes' checkboxes, in colors taken from the text area they're on: the box a shade darker than the area,
        /// its edge in the text color, and when ticked the notes' accent color with a mark that reads on it.
        /// </summary>
        public static Style CheckStyle(Color area, Color text, Color accent)
        {
            Func<Color, string> hex = c => "#" + c.A.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            double lum = (0.299 * area.R + 0.587 * area.G + 0.114 * area.B) / 255, k = lum > 0.5 ? 0.9 : 0.72;
            var box = Color.FromRgb((byte)(area.R * k), (byte)(area.G * k), (byte)(area.B * k));
            var edge = Color.FromArgb(0x99, text.R, text.G, text.B);
            double accentLum = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255;
            if (Math.Abs(accentLum - lum) < 0.3)
            {
                // The accent is too close to the page (white on a cream tab, say): a ticked box takes the text color instead.
                accent = text;
                accentLum = (0.299 * text.R + 0.587 * text.G + 0.114 * text.B) / 255;
            }
            var mark = accentLum > 0.55 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White;
            string xaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type CheckBox}'>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type CheckBox}'>
        <Border x:Name='box' Width='14' Height='14' CornerRadius='3' Background='BOX' BorderBrush='EDGE' BorderThickness='1' SnapsToDevicePixels='True'>
          <Path x:Name='mark' Visibility='Collapsed' Data='M 2.5 6.6 L 5.3 9.3 L 10.6 3.9' Stroke='MARK' StrokeThickness='1.7'
                StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='ACCENT'/></Trigger>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='mark' Property='Visibility' Value='Visible'/>
            <Setter TargetName='box' Property='Background' Value='ACCENT'/>
            <Setter TargetName='box' Property='BorderBrush' Value='ACCENT'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            xaml = xaml.Replace("BOX", hex(box)).Replace("EDGE", hex(edge)).Replace("MARK", hex(mark)).Replace("ACCENT", hex(Color.FromRgb(accent.R, accent.G, accent.B)));
            return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        /// <summary>A checkbox for the start of a paragraph: ticking it strikes the paragraph's text through.</summary>
        public static InlineUIContainer MakeCheck(bool done, double fontSize, Action edited)
        {
            double k = Math.Max(0.85, fontSize / 14.0);
            var cb = new CheckBox
            {
                IsChecked = done, Tag = CheckTag, Focusable = false, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0), LayoutTransform = new ScaleTransform(k, k), ToolTip = "Tick when done"
            };
            var container = new InlineUIContainer(cb) { BaselineAlignment = BaselineAlignment.Center };
            RoutedEventHandler changed = delegate
            {
                var p = ParagraphOf(container);
                if (p != null) SetStrike(p, cb.IsChecked == true);
                if (edited != null) edited();
            };
            cb.Checked += changed;
            cb.Unchecked += changed;
            return container;
        }

        public static Paragraph ParagraphOf(TextElement e)
        {
            DependencyObject d = e;
            while (d != null && !(d is Paragraph))
            {
                var fce = d as FrameworkContentElement;
                d = fce == null ? null : fce.Parent;
            }
            return d as Paragraph;
        }

        /// <summary>Strikes a paragraph's text through (or not), keeping any underline.</summary>
        public static void SetStrike(Paragraph p, bool on)
        {
            foreach (var run in Runs(p).ToList())
            {
                bool u, s;
                DecorationsOf(run, out u, out s);
                run.TextDecorations = Decorations(u, on);
            }
            if (on) return;
            // Taking it off: the spans and the paragraph itself may carry it too.
            bool pu, ps;
            Flags(p.TextDecorations, out pu, out ps);
            if (ps) p.TextDecorations = Decorations(pu, false);
            foreach (var span in Spans(p.Inlines))
            {
                bool su, ss;
                Flags(span.TextDecorations, out su, out ss);
                if (ss) span.TextDecorations = Decorations(su, false);
            }
        }

        static IEnumerable<Span> Spans(InlineCollection inlines)
        {
            var found = new List<Span>();
            foreach (var i in inlines)
            {
                var span = i as Span;
                if (span == null) continue;
                found.Add(span);
                found.AddRange(Spans(span.Inlines));
            }
            return found;
        }
    }
}
