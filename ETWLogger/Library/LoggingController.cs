using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ETWLogger.Library
{
    /// <summary>
    /// Controller responsible for all the service logic
    /// </summary>
    class LoggingController : IDisposable
    {
        /// <summary>
        /// Logger used for any necessary information
        /// </summary>
        private static readonly Logger _logger = LogManager.GetLogger("GeneralLogger");

        private static readonly Logger _fileLogger = LogManager.GetLogger("ETWFileLogger");

        private static readonly Logger _netLogger = LogManager.GetLogger("ETWNetLogger");

        private static readonly Logger _regLogger = LogManager.GetLogger("ETWRegLogger");

        private static readonly Logger _procLogger = LogManager.GetLogger("ETWProcLogger");

        private readonly TraceEventSession _kernelSession;

        private readonly KernelTraceEventParser _kernelParser;

        private readonly Thread _processingThread;

        private readonly CustomEventFilter _filter;

        private int _selfProcessId;

        private bool _rejectSelf;

        delegate void ProcessEventDelegate(TraceEvent data, string formatString, Logger logger);

        public LoggingController()
        {
            try
            {
                _selfProcessId = Process.GetCurrentProcess().Id;
                if (Boolean.TryParse(ConfigurationManager.AppSettings["RejectSelf"], out _rejectSelf))
                {
                    if (_rejectSelf)
                    {
                        _logger.Info("Process is not logging own actions by rejecting evetns with processID=" + _selfProcessId);
                    }
                    else
                    {
                        _logger.Info("Process is logging own actions.");
                    }
                }
                else
                {
                    _logger.Info("Could not determine \"RejectSelf\" configuration key. Logging own actions.");
                    _rejectSelf = false;
                }
                
                    
                _logger.Info("Initialising the ETWEngine Class.");
                _kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName, TraceEventSessionOptions.NoRestartOnCreate)
                {
                    BufferSizeMB = 1024,
                    CpuSampleIntervalMSec = 10,
                };
                _kernelSession.EnableKernelProvider(
                    KernelTraceEventParser.Keywords.FileIO |
                    KernelTraceEventParser.Keywords.FileIOInit |
                    KernelTraceEventParser.Keywords.NetworkTCPIP |
                    KernelTraceEventParser.Keywords.Registry |
                    KernelTraceEventParser.Keywords.Process
                    );

                _kernelParser = new KernelTraceEventParser(_kernelSession.Source);
                _processingThread = new Thread(() => { _kernelSession.Source.Process(); });
                _processingThread.Priority = ThreadPriority.Highest;
                _processingThread.IsBackground = true;

                _filter = new CustomEventFilter(
                    ConfigurationManager.AppSettings["FileRegexString"],
                    ConfigurationManager.AppSettings["NetRegexString"],
                    ConfigurationManager.AppSettings["RegRegexString"],
                    ConfigurationManager.AppSettings["ProcRegexString"],
                    ConfigurationManager.AppSettings["NotFileRegexString"],
                    ConfigurationManager.AppSettings["NotNetRegexString"],
                    ConfigurationManager.AppSettings["NotRegRegexString"],
                    ConfigurationManager.AppSettings["NotProcRegexString"]
                    );

                _logger.Info("Seting up network events.");
                SetupNetworkEvents();

                _logger.Info("Seting up registry events.");
                SetupRegistryEvents();

                _logger.Info("Seting up file events.");
                SetupFileEvents();

                _logger.Info("Seting up process events.");
                SetupProcessEvents();
            }
            catch (Exception x)
            {
                _logger.Error(x, "Failed to properly create LoggingController");
            }
        }

        public void Start()
        {
            try
            {
                _logger.Info("Starting processing events");
                if (!(TraceEventSession.IsElevated() ?? false))
                {
                    _logger.Error("Program doesn't have administrative privilege and might not work properly.");
                    return;
                }
                _processingThread.Start();
            } catch (Exception x)
            {
                _logger.Error(x, "Filed to start processing events.");
            }
        }

        public void LogLostEvents()
        {
            _logger.Info("Lost " + _kernelSession.EventsLost + " events");
        }

        public void Dispose()
        {
            _kernelSession.Dispose();
        }

        #region Processing methods
        private void ProcessEvent(TraceEvent data, string formatString, Logger logger)
        {
            try
            {
                if (data.ProcessID == _selfProcessId)
                {
                    if (_rejectSelf)
                    {
                        return;
                    }
                }
                PropertyInfo[] property_infos = data.GetType().GetProperties();

                foreach (var property in property_infos)
                {
                    var key = "{" + property.Name + "}";
                    if (formatString.Contains(key) == true)
                    {
                        var value = property.GetValue(data).ToString() ?? "";
                        formatString = formatString.Replace(key, value);
                    }
                }
                if (_filter.FileCheck(formatString))
                {
                    logger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                _logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }
        #endregion

        #region Event setup methods

        private void SetupProcessEvents()
        {
            var events = _kernelParser.GetType().GetEvents();
            var processEvents = ConfigurationManager.AppSettings["ProcessEvents"];
            if (processEvents != null)
            {
                foreach (var x in events)
                {
                    if (processEvents.Contains(x.Name))
                    {
                        var eventTraceFormatString = ConfigurationManager.AppSettings[x.EventHandlerType.GenericTypeArguments[0].Name];
                        if (eventTraceFormatString != null)
                        {
                            //ProcessEventDelegate deleg = ProcessEvent;
                            _logger.Info("Subscribing to event " + x.Name + " with the parsing string \"" + eventTraceFormatString + "\"");
                            Action<TraceEvent> handler = obj => ProcessEvent(obj, eventTraceFormatString, _procLogger);
                            Delegate convertedHandler = Delegate.CreateDelegate(x.EventHandlerType, handler.Target, handler.Method);
                            x.AddEventHandler(_kernelParser, convertedHandler);
                        }
                        else
                        {
                            _logger.Info("Parsing string for " + x.EventHandlerType.GenericTypeArguments[0].Name + " not found. " + x.Name + " event not subscribed");
                        }
                    }
                }
            }
            else
            {
                _logger.Info("Process events not configured. Subscription aborted.");
            }
        }

        private void SetupNetworkEvents()
        {
            var events = _kernelParser.GetType().GetEvents();
            var networkEvents = ConfigurationManager.AppSettings["NetworkEvents"];
            if (networkEvents != null)
            {
                foreach (var x in events)
                {
                    if (networkEvents.Contains(x.Name))
                    {
                        var eventTraceFormatString = ConfigurationManager.AppSettings[x.EventHandlerType.GenericTypeArguments[0].Name];
                        if(eventTraceFormatString != null){
                            //ProcessEventDelegate deleg = ProcessEvent;
                            _logger.Info("Subscribing to event " + x.Name + " with the parsing string \"" + eventTraceFormatString + "\"");
                            Action<TraceEvent> handler = obj => ProcessEvent(obj, eventTraceFormatString, _netLogger);
                            Delegate convertedHandler = Delegate.CreateDelegate(x.EventHandlerType, handler.Target, handler.Method);
                            x.AddEventHandler(_kernelParser, convertedHandler);
                        }
                        else
                        {
                            _logger.Info("Parsing string for " + x.EventHandlerType.GenericTypeArguments[0].Name + " not found. " + x.Name + " event not subscribed");
                        }
                    }
                }
            }
            else
            {
                _logger.Info("Network events not configured. Subscription aborted.");
            }
        }

        private void SetupRegistryEvents()
        {
            var events = _kernelParser.GetType().GetEvents();
            var registryEvents = ConfigurationManager.AppSettings["RegistryEvents"];
            if (registryEvents != null)
            {
                foreach (var x in events)
                {
                    if (registryEvents.Contains(x.Name))
                    {
                        var eventTraceFormatString = ConfigurationManager.AppSettings[x.EventHandlerType.GenericTypeArguments[0].Name];
                        if (eventTraceFormatString != null)
                        {
                            //ProcessEventDelegate deleg = ProcessEvent;
                            _logger.Info("Subscribing to event " + x.Name + " with the parsing string \"" + eventTraceFormatString + "\"");
                            Action<TraceEvent> handler = obj => ProcessEvent(obj, eventTraceFormatString, _regLogger);
                            Delegate convertedHandler = Delegate.CreateDelegate(x.EventHandlerType, handler.Target, handler.Method);
                            x.AddEventHandler(_kernelParser, convertedHandler);
                        }
                        else
                        {
                            _logger.Info("Parsing string for " + x.EventHandlerType.GenericTypeArguments[0].Name + " not found. " + x.Name + " event not subscribed");
                        }
                    }
                }
            }
            else
            {
                _logger.Info("Registry events not configured. Subscription aborted.");
            }
        }

        private void SetupFileEvents()
        {
            var events = _kernelParser.GetType().GetEvents();
            var fileEvents = ConfigurationManager.AppSettings["FileEvents"];
            if (fileEvents != null)
            {
                foreach (var x in events)
                {
                    if (fileEvents.Contains(x.Name))
                    {
                        var eventTraceFormatString = ConfigurationManager.AppSettings[x.EventHandlerType.GenericTypeArguments[0].Name];
                        if (eventTraceFormatString != null)
                        {
                            //ProcessEventDelegate deleg = ProcessEvent;
                            _logger.Info("Subscribing to event " + x.Name + " with the parsing string \"" + eventTraceFormatString + "\"");
                            Action<TraceEvent> handler = obj => ProcessEvent(obj, eventTraceFormatString, _fileLogger);
                            Delegate convertedHandler = Delegate.CreateDelegate(x.EventHandlerType, handler.Target, handler.Method);
                            x.AddEventHandler(_kernelParser, convertedHandler);
                        }
                        else
                        {
                            _logger.Info("Parsing string for " + x.EventHandlerType.GenericTypeArguments[0].Name + " not found. " + x.Name + " event not subscribed");
                        }
                    }
                }
            }
            else
            {
                _logger.Info("File events not configured. Subscription aborted.");
            }
        }

        #endregion
    }
}
