using Jarvis.Framework.ElasticLogPoller.Importers;
using log4net;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Threading;
using Topshelf;

namespace Jarvis.Framework.ElasticLogPoller
{
    public class Program
    {
        private readonly ILog _logger;

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            if (Environment.UserInteractive)
            {
                Console.Title = "Jarvis - Elastic Log Poller";
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.Clear();
                Banner();
            }

            HostFactory.Run(x =>
            {
                x.UseLog4Net("log4net.config");
                x.Service<Program>(s =>
                {
                    s.ConstructUsing(name => new Program());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("Jarvis - Elastic Poller Service");
                x.SetDisplayName("Jarvis - Elastic Poller Service");
                x.SetServiceName("JarvisElasticPoller");
            });
        }

        public Program()
        {
            _logger = LogManager.GetLogger(this.GetType());
            _logger.Debug("Starting.....");
        }

        private static void Banner()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================================");
            Console.WriteLine("Jarvis - Elastic Poller service - Proximo srl");
            Console.WriteLine("===================================================================");
            Console.WriteLine("  install                            -> Installa il servizio");
            Console.WriteLine("  uninstall                          -> Rimuove il servizio");
            Console.WriteLine("  net start JarvisElasticPoller   -> Avvia il servizio");
            Console.WriteLine("  net stop JarvisElasticPoller    -> Arresta il servizio");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
            Console.WriteLine();
        }

        private void Stop()
        {
            timer.Dispose();
        }

        private Timer timer;

        private ImporterEngine importer;

        private void Start()
        {
            try
            {
                var converter = new ImporterConverter();
                var fi = new FileInfo("config.json");
                Console.WriteLine("Configuration file: {0}", fi.FullName);
                importer = JsonConvert.DeserializeObject<ImporterEngine>(
                    File.ReadAllText(fi.FullName), converter);

                importer.HandleWildcards();
                importer.Configure();

                Console.WriteLine("Importing Started. CTRL+C to exit");
                var pollingIntervalInSecondsString = ConfigurationManager.AppSettings["PollingIntervalInSeconds"];
                Int32 pollingIntervalInSeconds;
                if (!Int32.TryParse(pollingIntervalInSecondsString, out pollingIntervalInSeconds))
                {
                    pollingIntervalInSeconds = 60;
                }
                timer = new Timer(Poll, null, 0, pollingIntervalInSeconds * 1000);
            }
            catch (Exception ex)
            {
                _logger.Error("Error during startup. " + ex.Message, ex);
                throw;
            }
        }

        private Boolean isPolling = false;

        private void Poll(object state)
        {
            if (!isPolling)
            {
                isPolling = true;
                try
                {
                    Boolean hasMore;
                    do
                    {
                        hasMore = importer.Poll();
                    } while (hasMore);
                }
                finally
                {
                    isPolling = false;
                }
            }
        }
    }
}
