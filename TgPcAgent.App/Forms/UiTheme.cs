namespace TgPcAgent.App.Forms;

internal static class UiTheme
{
    public static readonly Color Window = Color.FromArgb(247, 249, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(242, 245, 250);
    public static readonly Color Text = Color.FromArgb(24, 33, 48);
    public static readonly Color MutedText = Color.FromArgb(90, 98, 110);
    public static readonly Color Accent = Color.FromArgb(47, 111, 237);
    public static readonly Color AccentPressed = Color.FromArgb(35, 94, 213);
    public static readonly Color Secondary = Color.FromArgb(230, 234, 240);
    public static readonly Color SecondaryPressed = Color.FromArgb(214, 220, 229);
    public static readonly Color Danger = Color.FromArgb(225, 232, 245);
    public static readonly Color Border = Color.FromArgb(214, 220, 229);

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Window;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
    }

    public static void ApplyPanel(Control control, bool alternate = false)
    {
        control.BackColor = alternate ? SurfaceAlt : Surface;
        control.ForeColor = Text;
    }

    public static void ApplyMutedText(Control control)
    {
        control.ForeColor = MutedText;
    }

    public static void ApplyInput(TextBox textBox)
    {
        textBox.BackColor = SurfaceAlt;
        textBox.ForeColor = Text;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void ApplyCheckbox(CheckBox checkBox)
    {
        checkBox.ForeColor = Text;
        checkBox.BackColor = Color.Transparent;
    }

    public static void ApplyPrimaryButton(Button button)
    {
        PrepareButton(button, Accent, AccentPressed, Color.White);
    }

    public static void ApplySecondaryButton(Button button)
    {
        PrepareButton(button, Secondary, SecondaryPressed, Text);
    }

    public static void ApplyDangerButton(Button button)
    {
        PrepareButton(button, Danger, Color.FromArgb(220, 38, 38), Text);
    }

    private static void PrepareButton(Button button, Color background, Color pressed, Color foreground)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = background;
        button.ForeColor = foreground;
        button.Padding = new Padding(14, 0, 14, 0);
        button.Margin = new Padding(0, 0, 12, 0);

        button.MouseDown += (_, _) => button.BackColor = pressed;
        button.MouseUp += (_, _) => button.BackColor = background;
        button.MouseLeave += (_, _) => button.BackColor = background;
    }
}
