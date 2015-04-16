using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Jarvis.Framework.LogViewer.Host.Support;
using Topshelf;

namespace Jarvis.Framework.LogViewer.Host
{
    public class Program
    {

        static int Main(string[] args)
        {
            try
            {
                var executionExitCode = StandardDocumentStoreStart();
                return (Int32)executionExitCode;
            }
            catch (Exception ex)
            {
                var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_lastError.txt");
                File.WriteAllText(fileName, ex.ToString());
                throw;
            }
          
        }

        private static LogViewerConfiguration _configuration;

        private static TopshelfExitCode StandardDocumentStoreStart()
        {
            SetupColors();

            LoadConfiguration();

            var exitCode = HostFactory.Run(host =>
            {
                host.Service<LogViewerBootstrapper>(service =>
                {
                    service.ConstructUsing(() => new LogViewerBootstrapper());
                    service.WhenStarted(s => s.Start(_configuration));
                    service.WhenStopped(s => s.Stop());
                });

                host.RunAsNetworkService();

                host.SetDescription("Jarvis - Log Viewer");
                host.SetDisplayName("Jarvis - Log Viewer");
                host.SetServiceName("JarvisLogViewer");
            });
            return exitCode;
        }

        static void SetupColors()
        {
            if (!Environment.UserInteractive)
                return;
            Console.Title = "JARVIS :: Log Viewer Service";
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.Clear();
        }

        static void LoadConfiguration()
        {
            _configuration = new LogViewerConfiguration();
        }

        private static string FindArgument(string[] args, string prefix)
        {
            var arg = args.FirstOrDefault(a => a.StartsWith(prefix));
            if (String.IsNullOrEmpty(arg)) return String.Empty;
            return arg.Substring(prefix.Length);
        }

        private static void Banner()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================================");
            Console.WriteLine("Jarvis Framework Log Viewer - Proximo srl");
            Console.WriteLine("===================================================================");
            Console.WriteLine("  install                        -> install service");
            Console.WriteLine("  uninstall                      -> remove service");
            Console.WriteLine("  net start JarvisLogViewer      -> start service");
            Console.WriteLine("  net stop JarvisLogViewer       -> stop service");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
