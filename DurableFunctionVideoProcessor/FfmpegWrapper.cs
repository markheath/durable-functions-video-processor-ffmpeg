using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    static class FfmpegWrapper
    {
        public static async Task Transcode(string inputPath, string ffmpegParams, string outputFile, TraceWriter log)
        {
            var prefix = ffmpegParams.Contains("-i ") ? "" : $"-i \"{inputPath}\" ";
            var arguments = $"{prefix}{ffmpegParams} \"{outputFile}\"";
            await RunFfmpeg(arguments, log);
        }

        private static string GetFfmpegPath()
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home))
            {
                return Path.Combine(GetAssemblyDirectory(), "..\\Tools\\ffmpeg.exe");
            }
            return Path.Combine(home, "site\\wwwroot\\Tools\\ffmpeg.exe");
        }

        public static string GetAssemblyDirectory()
        {
            var codeBase = typeof(FfmpegWrapper).Assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        private static async Task RunFfmpeg(string arguments, TraceWriter log)
        {
            var ffmpegPath = GetFfmpegPath();
            var processStartInfo = new ProcessStartInfo(ffmpegPath, arguments);
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardError = true;
            var sb = new StringBuilder();
            var p = new Process();
            p.StartInfo = processStartInfo;
            p.ErrorDataReceived += (s, a) => sb.AppendLine(a.Data);
            p.EnableRaisingEvents = true;

            p.Start();
            p.BeginErrorReadLine();

            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
            {
                log.Error(sb.ToString());
                throw new InvalidOperationException($"Ffmpeg failed with exit code {p.ExitCode}");
            }
        }

        public static Task WaitForExitAsync(this Process process,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(tcs.SetCanceled);

            return tcs.Task;
        }
    }
}