using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PooTrucksClient
{
    public partial class Service : ServiceBase
    {
        public Service()
        {
            InitializeComponent();
            fs = new FarmingSimulator();
        }

        protected override void OnStart(string[] args)
        {
            Console.Write("Server host: ");
            var host = Console.ReadLine();
            Console.Write("Server port: ");
            var port = Console.ReadLine();
            try
            {
                Uri server_uri = new Uri("http://" + host + ":" + port);
                fs.Start(server_uri);
            }
            catch ( UriFormatException e )
            {
                Console.WriteLine(e.Message);
            }
        }

        protected override void OnStop()
        {
        }

        internal void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }

        FarmingSimulator fs;
    }
}
