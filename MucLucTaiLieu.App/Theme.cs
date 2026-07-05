using System.Drawing;

namespace MucLucTaiLieu.App;

/// <summary>Colors and fonts from the prototype (mota3 §3).</summary>
public static class Theme
{
    public static readonly Color Accent = ColorTranslator.FromHtml("#0043a5");     // primary accent
    public static readonly Color ReadCyan = ColorTranslator.FromHtml("#00ccd6");   // "Đọc dữ liệu" button
    public static readonly Color OkGreen = ColorTranslator.FromHtml("#107c10");
    public static readonly Color WarnAmber = ColorTranslator.FromHtml("#b8860b");
    public static readonly Color ErrRed = ColorTranslator.FromHtml("#c42b1c");
    public static readonly Color PanelBg = ColorTranslator.FromHtml("#f4f6fa");
    public static readonly Color ProgressTrack = ColorTranslator.FromHtml("#e6e6e6");
    public static readonly Color ConsoleBg = ColorTranslator.FromHtml("#1e1e1e");
    public static readonly Color ConsoleFg = ColorTranslator.FromHtml("#d4d4d4");

    public static readonly Font Ui = new("Segoe UI", 9f);
    public static readonly Font UiBold = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9f);

    /// <summary>Style a button as the primary accent action.</summary>
    public static void Primary(System.Windows.Forms.Button b, Color? bg = null)
    {
        b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = bg ?? Accent;
        b.ForeColor = Color.White;
        b.Font = UiBold;
        b.Cursor = System.Windows.Forms.Cursors.Hand;
    }
}
