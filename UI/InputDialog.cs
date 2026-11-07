namespace fernien.UI;

/// <summary>Tek satırlık metin isteyen basit iletişim kutusu (yeni klasör, yeniden adlandır).</summary>
internal sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    private InputDialog(string title, string prompt, string initialValue)
    {
        Text = title;
        Font = SystemFonts.MessageBoxFont ?? Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        var label = new Label { Text = prompt, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        _textBox = new TextBox { Text = initialValue, Width = 380, Margin = new Padding(0, 0, 0, 12) };

        var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 28) };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(88, 28) };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_textBox, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            _textBox.Focus();
            // Dosya adlarında uzantıyı seçme (Gezgin davranışı).
            int dot = initialValue.LastIndexOf('.');
            if (dot > 0) _textBox.Select(0, dot);
            else _textBox.SelectAll();
        };
    }

    /// <summary>Kullanıcı iptal ederse veya boş bırakırsa null döner.</summary>
    public static string? Prompt(IWin32Window owner, string title, string prompt, string initialValue = "")
    {
        using var dialog = new InputDialog(title, prompt, initialValue);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        string value = dialog._textBox.Text.Trim();
        return value.Length == 0 ? null : value;
    }
}
