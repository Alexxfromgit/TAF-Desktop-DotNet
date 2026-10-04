namespace ShopDesk.WinForms;

/// <summary>Small layout helpers: every control gets a Name, which WinForms exposes as its UI Automation id.</summary>
internal static class Ui
{
    public static T Named<T>(this T control, string name) where T : Control
    {
        control.Name = name;
        return control;
    }

    public static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };

    public static FlowLayoutPanel Column(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16),
        };
        panel.Controls.AddRange(controls);
        return panel;
    }

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        panel.Controls.AddRange(controls);
        return panel;
    }
}
