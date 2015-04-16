using Jarvis.Framework.ElasticLogPoller.Importers;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace Jarvis.Framework.ElasticLogPoller
{
    public class Program
    {
        private ILog _logger;

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
               
                x.UseOldLog4Net("log4net.config");
                x.Service<Program>(s =>
                {
                    s.ConstructUsing(name => new Program());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();
                x.DependsOnMsmq();

                x.SetDescription("Intranet.ProcessManager service");
                x.SetDisplayName("Intranet.ProcessManager service");
                x.SetServiceName("IntranetProcessManager");
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
            Console.WriteLine("Intranet.ProcessManager service - Proximo srl");
            Console.WriteLine("===================================================================");
            Console.WriteLine("  install                            -> Installa il servizio");
            Console.WriteLine("  uninstall                          -> Rimuove il servizio");
            Console.WriteLine("  net start IntranetProcessManager   -> Avvia il servizio");
            Console.WriteLine("  net stop IntranetProcessManager    -> Arresta il servizio");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
            Console.WriteLine();
        }

        private void Stop()
        {
            timer.Dispose();
        }

        private Timer timer;

        ImporterEngine importer;

        private void Start()
        {
            try
            {
                var converter = new ImporterConverter();

                importer = JsonConvert.DeserializeObject<ImporterEngine>(
                    File.ReadAllText("config.json"), converter);

                importer.Configure();
                Console.WriteLine("Importing Started. CTRL+C to exit");
                timer = new Timer(Poll, null, 0, 60 * 1000);

            }
            catch (Exception ex)
            {
                _logger.Error("Error during startup. " + ex.Message, ex);
                throw;
            }
        }

        Boolean isPolling = false;
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
