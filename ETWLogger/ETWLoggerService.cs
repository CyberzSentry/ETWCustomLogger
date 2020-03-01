using ETWLogger.Library;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ETWLogger
{
    public partial class ETWLoggerService : ServiceBase
    {
        /// <summary>
        /// Logger used for any necessary information
        /// </summary>
        private static readonly Logger logger = LogManager.GetLogger("GeneralLogger");

        /// <summary>
        /// Controller responsible for all the service logic
        /// </summary>
        private static readonly LoggingController loggingController = new LoggingController();

        public ETWLoggerService()
        {
            logger.Info("Creating ETWLoggerService");
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            logger.Info("Starting service");
            loggingController.Start();

        }

        protected override void OnStop()
        {
            loggingController.LogLostEvents();
            logger.Info("Stoping service");

        }
    }
}
