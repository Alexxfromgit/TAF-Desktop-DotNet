using System.Xml.Linq;
using FlaUI.Core.Capturing;
using Taf.Desktop.Core.App;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Reporting;
using Taf.Desktop.Core.Ui;

namespace Taf.Desktop.Core.Evidence;

/// <summary>Screenshot and UI tree of the app for failed tests and soft-assertion failures.</summary>
public static class FailureEvidence
{
    /// <summary>Attaches a screenshot, the UI tree of the app, or the exit code if the app is gone.</summary>
    public static void Capture(string suffix = "on failure")
    {
        var session = AppSession.Current;
        if (session == null)
        {
            return;
        }
        if (session.HasExited)
        {
            Attach.Text($"App process {suffix}", $"{session.Name} is not running any more (it exited or crashed).");
            return;
        }
        var config = TafConfig.Current;
        if (config.Bool("Evidence:ScreenshotOnFailure", true))
        {
            BestEffort(() => Attach.Png($"Screenshot {suffix}", Screen()));
        }
        if (config.Bool("Evidence:UiTreeOnFailure", true))
        {
            BestEffort(() => Attach.Xml($"UI tree {suffix}", UiTree.Dump(session.TopLevelNodes())));
        }
    }

    /// <summary>A screenshot for the <paramref name="number"/>th soft-assertion failure of a test.</summary>
    public static void OnSoftFailure(int number)
    {
        if (AppSession.Current is { HasExited: false } && TafConfig.Current.Bool("Evidence:ScreenshotOnSoftFailure", true))
        {
            BestEffort(() => Attach.Png($"Screenshot (soft failure {number})", Screen()));
        }
    }

    /// <summary>The primary screen as PNG (dialogs and popups included).</summary>
    public static byte[] Screen()
    {
        var file = Path.Combine(Path.GetTempPath(), $"taf-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = FlaUI.Core.Capturing.Capture.MainScreen())
            {
                image.ToFile(file);
            }
            return File.ReadAllBytes(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static void BestEffort(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Warn($"Could not collect failure evidence: {e.Message}");
        }
    }
}

/// <summary>XML dump of a UI Automation tree: what the framework could see when a test failed.</summary>
public static class UiTree
{
    public static string Dump(IEnumerable<IUiNode> roots, int maxDepth = 30, int maxNodes = 3000)
    {
        var count = 0;
        var tree = new XElement("ui-tree", roots.Select(r => Node(r, 0)));
        return tree.ToString();

        XElement Node(IUiNode node, int depth)
        {
            count++;
            var element = new XElement(SafeName(node.ControlType.ToString()));
            Add(element, "AutomationId", node.AutomationId);
            Add(element, "Name", node.Name);
            Add(element, "ClassName", node.ClassName);
            if (node.Value is { Length: > 0 } value && !node.IsPassword)
            {
                Add(element, "Value", value);
            }
            if (!node.IsEnabled)
            {
                element.SetAttributeValue("Enabled", "false");
            }
            if (node.IsOffscreen)
            {
                element.SetAttributeValue("Offscreen", "true");
            }
            var b = node.Bounds;
            element.SetAttributeValue("Bounds", $"{b.X},{b.Y},{b.Width},{b.Height}");
            if (depth < maxDepth && count < maxNodes)
            {
                foreach (var child in node.FindAll(Locator.Any, childrenOnly: true))
                {
                    if (count >= maxNodes)
                    {
                        element.Add(new XComment("truncated"));
                        break;
                    }
                    element.Add(Node(child, depth + 1));
                }
            }
            return element;
        }
    }

    private static void Add(XElement element, string name, string value)
    {
        if (value.Length > 0)
        {
            element.SetAttributeValue(name, value);
        }
    }

    private static string SafeName(string name) => XmlNameOk(name) ? name : "Element";

    private static bool XmlNameOk(string name)
    {
        try
        {
            _ = XName.Get(name);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }
}
