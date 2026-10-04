namespace ShopDesk;

/// <summary>
/// Command line of the demo app: <c>--api http://localhost:5080 --data-dir C:\temp\shopdesk</c>.
/// Tests start the app with the URL of their mock backend and a throw-away data directory.
/// </summary>
public sealed record LaunchOptions(Uri ApiUrl, string DataDirectory)
{
    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var api = "http://localhost:5080";
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopDesk");
        for (var i = 0; i < args.Count - 1; i++)
        {
            switch (args[i])
            {
                case "--api":
                    api = args[++i];
                    break;
                case "--data-dir":
                    dataDir = args[++i];
                    break;
            }
        }
        return new LaunchOptions(new Uri(api.TrimEnd('/') + "/"), dataDir);
    }
}
