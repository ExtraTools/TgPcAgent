namespace TgPcAgent.App.Forms;

public sealed class ExistingInstanceDialog : Form
{
    private ExistingInstanceDialog()
    {
        Text = "TgPcAgent";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 520;
        Height = 280;

        UiTheme.ApplyWindow(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = UiTheme.Window
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 16)
        };
        UiTheme.ApplyPanel(card);

        var badge = new Label
        {
            Text = "УЖЕ ЗАПУЩЕН",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 10)
        };
        badge.ForeColor = UiTheme.Accent;

        var title = new Label
        {
            Text = "На этом ПК уже работает TgPcAgent",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15f, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 8)
        };

        var body = new Label
        {
            Text = "Выбери, что сделать с текущим экземпляром: оставить как есть, закрыть его или перезапустить приложение.",
            AutoSize = true,
            MaximumSize = new Size(430, 0)
        };
        UiTheme.ApplyMutedText(body);

        card.Controls.Add(badge, 0, 0);
        card.Controls.Add(title, 0, 1);
        card.Controls.Add(body, 0, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 0)
        };

        buttonPanel.Controls.Add(BuildButton("Отмена", ExistingInstanceChoice.Cancel, UiTheme.ApplySecondaryButton));
        buttonPanel.Controls.Add(BuildButton("Закрыть текущий", ExistingInstanceChoice.CloseExisting, UiTheme.ApplyDangerButton));
        buttonPanel.Controls.Add(BuildButton("Перезапустить", ExistingInstanceChoice.RestartExisting, UiTheme.ApplyPrimaryButton));

        var footer = new Label
        {
            Text = "Перезапуск закроет текущий процесс и сразу поднимет новый экземпляр.",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        UiTheme.ApplyMutedText(footer);

        layout.Controls.Add(card, 0, 0);
        layout.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Fill, BackColor = UiTheme.Window }, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.Controls.Add(footer, 0, 3);

        Controls.Add(layout);
    }

    public ExistingInstanceChoice Choice { get; private set; } = ExistingInstanceChoice.Cancel;

    public static ExistingInstanceChoice ShowChoice()
    {
        using var dialog = new ExistingInstanceDialog();
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private Button BuildButton(string text, ExistingInstanceChoice choice, Action<Button> style)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true
        };
        style(button);
        button.Click += (_, _) =>
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
            Close();
        };

        return button;
    }
}
