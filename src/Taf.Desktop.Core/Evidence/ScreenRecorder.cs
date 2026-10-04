using FlaUI.Core.Capturing;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Reporting;

namespace Taf.Desktop.Core.Evidence;

/// <summary>When to record the screen (<c>Evidence:Video</c>).</summary>
public enum VideoMode
{
    Off,
    OnFailure,
    Always,
}

/// <summary>
/// Optional screen recording per test with FlaUI's recorder. Needs ffmpeg: <c>Evidence:FfmpegPath</c> or
/// <c>ffmpeg.exe</c> on the PATH. Recordings of passed tests are deleted in <see cref="VideoMode.OnFailure"/> mode.
/// </summary>
public static class ScreenRecorder
{
    private static VideoRecorder? recorder;
    private static string? file;
    private static bool warned;

    public static void Start(string testName)
    {
        var config = TafConfig.Current;
        if (config.Enum("Evidence:Video", VideoMode.Off) == VideoMode.Off || recorder != null)
        {
            return;
        }
        var ffmpeg = FindFfmpeg(config);
        if (ffmpeg == null)
        {
            if (!warned)
            {
                warned = true;
                Log.Warn("Evidence:Video is on, but ffmpeg was not found: set Evidence:FfmpegPath or put ffmpeg.exe on the PATH.");
            }
            return;
        }
        var directory = config.ResolvePath(config.Get("Evidence:VideoDirectory", "artifacts/videos"));
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, Safe(testName) + ".mp4");
        recorder = new VideoRecorder(new VideoRecorderSettings
        {
            ffmpegPath = ffmpeg,
            TargetVideoPath = file,
            FrameRate = 5,
            VideoQuality = 26,
            VideoFormat = VideoFormat.x264,
            EncodeWithLowPriority = true,
        }, _ => Capture.MainScreen());
    }

    public static void Stop(bool failed)
    {
        if (recorder == null)
        {
            return;
        }
        try
        {
            recorder.Stop();
            recorder.Dispose();
            var keep = failed || TafConfig.Current.Enum("Evidence:Video", VideoMode.Off) == VideoMode.Always;
            if (keep)
            {
                Attach.File("Screen recording", file!, "video/mp4");
            }
            else if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception e)
        {
            Log.Warn($"Could not finish the screen recording: {e.Message}");
        }
        finally
        {
            recorder = null;
            file = null;
        }
    }

    private static string? FindFfmpeg(TafConfig config)
    {
        if (config.Find("Evidence:FfmpegPath") is { } configured)
        {
            var path = config.ResolvePath(configured);
            return File.Exists(path) ? path : null;
        }
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Where(dir => dir.Length > 0)
            .Select(dir => Path.Combine(dir, "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static string Safe(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
}
