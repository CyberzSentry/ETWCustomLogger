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
            var ProcessTraceData = ConfigurationManager.AppSettings["ProcessTraceData"];

            _kernelParser.ProcessStart += obj => ProcessEvent(obj, ProcessTraceData, _procLogger);
            //_kernelParser.ProcessStop += obj => ProcessEvent(obj, ProcessTraceData, _procLogger);
            _kernelParser.ProcessStartGroup += obj => ProcessEvent(obj, ProcessTraceData, _procLogger);
            _kernelParser.ProcessDCStart += obj => ProcessEvent(obj, ProcessTraceData, _procLogger);
        }

        private void SetupNetworkEvents()
        {
            var TcpIpV6ConnectTraceData = ConfigurationManager.AppSettings["TcpIpV6ConnectTraceData"];
            var UdpIpTraceData = ConfigurationManager.AppSettings["UdpIpTraceData"];
            var UdpIpFailTraceData = ConfigurationManager.AppSettings["UdpIpFailTraceData"];
            var UpdIpV6TraceData = ConfigurationManager.AppSettings["UpdIpV6TraceData"];
            var TcpIpSendTraceData = ConfigurationManager.AppSettings["TcpIpSendTraceData"];
            var TcpIpTraceData = ConfigurationManager.AppSettings["TcpIpTraceData"];
            var TcpIpConnectTraceData = ConfigurationManager.AppSettings["TcpIpConnectTraceData"];
            var TcpIpFailTraceData = ConfigurationManager.AppSettings["TcpIpFailTraceData"];
            var TcpIpV6SendTraceData = ConfigurationManager.AppSettings["TcpIpV6SendTraceData"];
            var TcpIpV6TraceData = ConfigurationManager.AppSettings["TcpIpV6TraceData"];

            var events = _kernelParser.GetType().GetEvents();

            _kernelParser.TcpIpAcceptIPV6 += obj => ProcessEvent(obj, TcpIpV6ConnectTraceData, _netLogger);
            _kernelParser.TcpIpAccept += obj => ProcessEvent(obj, TcpIpConnectTraceData, _netLogger);

            _kernelParser.TcpIpConnectIPV6 += obj => ProcessEvent(obj, TcpIpV6ConnectTraceData, _netLogger);
            _kernelParser.TcpIpConnect += obj => ProcessEvent(obj, TcpIpConnectTraceData, _netLogger);

            _kernelParser.TcpIpDisconnectIPV6 += obj => ProcessEvent(obj, TcpIpV6TraceData, _netLogger);
            _kernelParser.TcpIpDisconnect += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);
            
            _kernelParser.TcpIpSendIPV6 += obj => ProcessEvent(obj, TcpIpV6SendTraceData, _netLogger);
            _kernelParser.TcpIpSend += obj => ProcessEvent(obj, TcpIpSendTraceData, _netLogger);

            _kernelParser.TcpIpRecvIPV6 += obj => ProcessEvent(obj, TcpIpV6TraceData, _netLogger);
            _kernelParser.TcpIpRecv += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);

            _kernelParser.TcpIpTCPCopyIPV6 += obj => ProcessEvent(obj, TcpIpV6TraceData, _netLogger);
            _kernelParser.TcpIpTCPCopy += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);

            _kernelParser.TcpIpRetransmitIPV6 += obj => ProcessEvent(obj, TcpIpV6TraceData, _netLogger);
            _kernelParser.TcpIpRetransmit += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);

            //_kernelParser.TcpIpARPCopy += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);
            //_kernelParser.TcpIpFullACK += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);
            //_kernelParser.TcpIpPartACK += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);
            //_kernelParser.TcpIpDupACK += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);

            _kernelParser.TcpIpReconnectIPV6 += obj => ProcessEvent(obj, TcpIpV6TraceData, _netLogger);
            _kernelParser.TcpIpReconnect += obj => ProcessEvent(obj, TcpIpTraceData, _netLogger);

            _kernelParser.TcpIpFail += obj => ProcessEvent(obj, TcpIpFailTraceData, _netLogger);
        
            _kernelParser.UdpIpSendIPV6 += obj => ProcessEvent(obj, UpdIpV6TraceData, _netLogger);
            _kernelParser.UdpIpSend += obj => ProcessEvent(obj, UdpIpTraceData, _netLogger);

            _kernelParser.UdpIpRecvIPV6 += obj => ProcessEvent(obj, UpdIpV6TraceData, _netLogger);
            _kernelParser.UdpIpRecv += obj => ProcessEvent(obj, UdpIpTraceData, _netLogger);

            _kernelParser.UdpIpFail += obj => ProcessEvent(obj, UdpIpFailTraceData, _netLogger);

        }

        private void SetupRegistryEvents()
        {
            var RegistryTraceData = ConfigurationManager.AppSettings["RegistryTraceData"];

            _kernelParser.RegistryCreate += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryOpen += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryDelete += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryQuery += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistrySetValue += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryDeleteValue += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryQueryValue += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryEnumerateKey += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryQueryMultipleValue += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            _kernelParser.RegistryEnumerateValueKey += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryFlush += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistrySetInformation += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryKCBCreate += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryKCBDelete += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryKCBRundownBegin += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryKCBRundownEnd += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryVirtualize += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);
            //_kernelParser.RegistryClose += obj => ProcessEvent(obj, RegistryTraceData, _regLogger);

        }

        private void SetupFileEvents()
        {
            var FileIOInfoTraceData = ConfigurationManager.AppSettings["FileIOInfoTraceData"];
            var FileIODirEnumTraceData = ConfigurationManager.AppSettings["FileIODirEnumTraceData"];
            var FileIOOpEndTraceData = ConfigurationManager.AppSettings["FileIOOpEndTraceData"];
            var FileIOReadWriteTraceData = ConfigurationManager.AppSettings["FileIOReadWriteTraceData"];
            var MapFileTraceData = ConfigurationManager.AppSettings["MapFileTraceData"];
            var FileIONameTraceData = ConfigurationManager.AppSettings["FileIONameTraceData"];
            var FileIOCreateTraceData = ConfigurationManager.AppSettings["FileIOCreateTraceData"];
            var FileIOSimpleOpTraceData = ConfigurationManager.AppSettings["FileIOSimpleOpTraceData"];

            //_kernelParser.FileIOFSControl += obj => ProcessEvent(obj, FileIOInfoTraceData, _fileLogger);
            //_kernelParser.FileIODirEnum += obj => ProcessEvent(obj, FileIODirEnumTraceData, _fileLogger);
            //_kernelParser.FileIODirNotify += obj => ProcessEvent(obj, FileIODirEnumTraceData, _fileLogger);
            //_kernelParser.FileIOOperationEnd += obj => ProcessEvent(obj, FileIOOpEndTraceData, _fileLogger);
            _kernelParser.FileIORename += obj => ProcessEvent(obj, FileIOInfoTraceData, _fileLogger);
            _kernelParser.FileIODelete += obj => ProcessEvent(obj, FileIOInfoTraceData, _fileLogger);
            //_kernelParser.FileIOQueryInfo += obj => ProcessEvent(obj, FileIOInfoTraceData, _fileLogger);
            _kernelParser.FileIOWrite += obj => ProcessEvent(obj, FileIOReadWriteTraceData, _fileLogger);
            //_kernelParser.FileIOSetInfo += obj => ProcessEvent(obj, FileIOInfoTraceData, _fileLogger);
            _kernelParser.FileIOUnmapFile += obj => ProcessEvent(obj, MapFileTraceData, _fileLogger);
            _kernelParser.FileIOMapFileDCStart += obj => ProcessEvent(obj, MapFileTraceData, _fileLogger);
            _kernelParser.FileIOMapFile += obj => ProcessEvent(obj, MapFileTraceData, _fileLogger);
            //_kernelParser.FileIOName += obj => ProcessEvent(obj, FileIONameTraceData, _fileLogger);
            //_kernelParser.FileIOFileCreate += obj => ProcessEvent(obj, FileIONameTraceData, _fileLogger);
            //_kernelParser.FileIOFileDelete += obj => ProcessEvent(obj, FileIONameTraceData, _fileLogger);
            //_kernelParser.FileIOFileRundown += obj => ProcessEvent(obj, FileIONameTraceData, _fileLogger);
            _kernelParser.FileIOCreate += obj => ProcessEvent(obj, FileIOCreateTraceData, _fileLogger);
            //_kernelParser.FileIOCleanup += obj => ProcessEvent(obj, FileIOSimpleOpTraceData, _fileLogger);
            _kernelParser.FileIOClose += obj => ProcessEvent(obj, FileIOSimpleOpTraceData, _fileLogger);
            _kernelParser.FileIOFlush += obj => ProcessEvent(obj, FileIOSimpleOpTraceData, _fileLogger);
            _kernelParser.FileIORead += obj => ProcessEvent(obj, FileIOReadWriteTraceData, _fileLogger);
            _kernelParser.FileIOMapFileDCStop += obj => ProcessEvent(obj, MapFileTraceData, _fileLogger);
        }

        #endregion
    }
}
