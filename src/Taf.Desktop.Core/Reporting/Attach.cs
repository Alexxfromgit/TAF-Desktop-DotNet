using System.Text;
using Allure.Net.Commons;

namespace Taf.Desktop.Core.Reporting;

/// <summary>Allure attachments of the running test (ignored outside a test).</summary>
public static class Attach
{
    public static void Png(string name, byte[] content) => Add(name, "image/png", content, ".png");

    public static void Text(string name, string content) => Add(name, "text/plain", Encoding.UTF8.GetBytes(content), ".txt");

    public static void Xml(string name, string content) => Add(name, "application/xml", Encoding.UTF8.GetBytes(content), ".xml");

    public static void Json(string name, string content) => Add(name, "application/json", Encoding.UTF8.GetBytes(content), ".json");

    public static void Html(string name, string content) => Add(name, "text/html", Encoding.UTF8.GetBytes(content), ".html");

    public static void File(string name, string path, string mimeType)
    {
        if (System.IO.File.Exists(path))
        {
            Add(name, mimeType, System.IO.File.ReadAllBytes(path), Path.GetExtension(path));
        }
    }

    private static void Add(string name, string type, byte[] content, string extension)
    {
        if (Step.InReport)
        {
            AllureApi.AddAttachment(name, type, content, extension);
        }
    }
}
