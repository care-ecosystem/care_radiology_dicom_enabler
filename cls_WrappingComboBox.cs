using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// A MaterialComboBox whose open list wraps a long item at spaces onto more lines, in a row tall enough for
    /// them, and puts a tick before the item IsSaved picks. It measures and draws the list's items itself and
    /// does not pass them on, so MaterialComboBox's own single-line drawing cannot cover them; the closed box
    /// is still drawn by MaterialComboBox.
    /// </summary>
    public class cls_WrappingComboBox : MaterialComboBox
    {
        private const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
        private const TextFormatFlags SingleLine = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
        private const string Tick = "✔";
        private const int VerticalPadding = 10;
        // MaterialComboBox's row height, the least a row gets
        private const int MinItemHeight = 43;

        private Font tickFont;

        /// <summary>Picks the item marked with a tick (the saved one); null marks none.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<object, bool> IsSaved { get; set; }

        private static MaterialSkinManager Skin => MaterialSkinManager.Instance;

        private static Font ItemFont => Skin.getFontByType(MaterialSkinManager.fontType.Subtitle1);

        // The item font has no tick, so it is drawn in a symbol font
        private Font TickFont => tickFont ?? (tickFont = new Font("Segoe UI Symbol", ItemFont.Size, FontStyle.Regular, ItemFont.Unit));

        // Every item leaves room for the tick, so the names stay lined up
        private int TickWidth => TextRenderer.MeasureText(Tick + " ", TickFont, Size.Empty, SingleLine).Width;

        private int TextWidth => Math.Max(1, Width - Skin.FORM_PADDING * 2 - TickWidth);

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
            {
                base.OnMeasureItem(e);
                return;
            }
            int textHeight = TextRenderer.MeasureText(GetItemText(Items[e.Index]), ItemFont,
                                                      new Size(TextWidth, int.MaxValue), Wrap).Height;
            e.ItemHeight = Math.Max(MinItemHeight, textHeight + VerticalPadding * 2);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            // The closed box (ComboBoxEdit) is MaterialComboBox's to draw
            if (e.Index < 0 || e.Index >= Items.Count || (e.State & DrawItemState.ComboBoxEdit) != 0 || !DroppedDown)
            {
                base.OnDrawItem(e);
                return;
            }

            object item = Items[e.Index];
            string text = GetItemText(item);
            Font font = ItemFont;
            Color textColor = Skin.TextHighEmphasisColor;
            bool hovered = (e.State & (DrawItemState.Selected | DrawItemState.Focus)) != 0;
            using (var background = new SolidBrush(hovered ? Skin.BackgroundHoverColor : Skin.BackgroundColor))
                e.Graphics.FillRectangle(background, e.Bounds);

            // Wrapped text is centred by hand: GDI centres vertically only on a single line
            int width = TextWidth;
            int textHeight = TextRenderer.MeasureText(e.Graphics, text, font, new Size(width, int.MaxValue), Wrap).Height;
            int left = e.Bounds.Left + Skin.FORM_PADDING;
            int top = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - textHeight) / 2);
            if (IsSaved != null && IsSaved(item))
                TextRenderer.DrawText(e.Graphics, Tick, TickFont, new Rectangle(left, top, TickWidth, font.Height), textColor, SingleLine);
            TextRenderer.DrawText(e.Graphics, text, font, new Rectangle(left + TickWidth, top, width, textHeight), textColor, Wrap);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tickFont?.Dispose();
                tickFont = null;
            }
            base.Dispose(disposing);
        }
    }
}
