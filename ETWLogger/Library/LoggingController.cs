using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
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

        private TraceEventSession _kernelSession;

        private KernelTraceEventParser _kernelParser;

        private Thread _processingThread;

        private CustomEventFilter _filter;

        public LoggingController()
        {
            try
            {

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
                    ConfigurationManager.AppSettings["NotFileRegexString"],
                    ConfigurationManager.AppSettings["NotNetRegexString"],
                    ConfigurationManager.AppSettings["NotRegRegexString"]
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
        private void ProcessNetworkEvent(TraceEvent data, string formatString)
        {
            try
            {
                if (data.ProcessName == "ETWLogger")
                {
                    return;
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
                if (_filter.NetCheck(formatString))
                {
                    _netLogger.Info(formatString);
                }
            }
            catch(Exception x)
            {
                _logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessRegistryEvent(TraceEvent data, string formatString)
        {
            try
            {
                if (data.ProcessName == "ETWLogger")
                {
                    return;
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
                if (_filter.RegCheck(formatString))
                {
                    
                    _regLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                _logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessFileEvent(TraceEvent data, string formatString)
        {
            try
            {
                if (data.ProcessName == "ETWLogger")
                {
                    return;
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
                    _fileLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                _logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessProcessEvents(TraceEvent data, string formatString)
        {
            try
            {
                if (data.ProcessName == "ETWLogger")
                {
                    return;
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
                    _procLogger.Info(formatString);
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

            _kernelParser.ProcessStart += obj => ProcessProcessEvents(obj, ProcessTraceData);
            //_kernelParser.ProcessStop += obj => ProcessProcessEvents(obj, ProcessTraceData);
            _kernelParser.ProcessStartGroup += obj => ProcessProcessEvents(obj, ProcessTraceData);
            _kernelParser.ProcessDCStart += obj => ProcessProcessEvents(obj, ProcessTraceData);
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

            _kernelParser.TcpIpAcceptIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6ConnectTraceData);
            _kernelParser.TcpIpAccept += obj => ProcessNetworkEvent(obj, TcpIpConnectTraceData);

            _kernelParser.TcpIpConnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6ConnectTraceData);
            _kernelParser.TcpIpConnect += obj => ProcessNetworkEvent(obj, TcpIpConnectTraceData);

            _kernelParser.TcpIpDisconnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpDisconnect += obj => ProcessNetworkEvent(obj, TcpIpTraceData);
            
            _kernelParser.TcpIpSendIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6SendTraceData);
            _kernelParser.TcpIpSend += obj => ProcessNetworkEvent(obj, TcpIpSendTraceData);

            _kernelParser.TcpIpRecvIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpRecv += obj => ProcessNetworkEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpTCPCopyIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpTCPCopy += obj => ProcessNetworkEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpRetransmitIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpRetransmit += obj => ProcessNetworkEvent(obj, TcpIpTraceData);

            //_kernelParser.TcpIpARPCopy += obj => ProcessNetworkEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpFullACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpPartACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpDupACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpReconnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpReconnect += obj => ProcessNetworkEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpFail += obj => ProcessNetworkEvent(obj, TcpIpFailTraceData);
        
            _kernelParser.UdpIpSendIPV6 += obj => ProcessNetworkEvent(obj, UpdIpV6TraceData);
            _kernelParser.UdpIpSend += obj => ProcessNetworkEvent(obj, UdpIpTraceData);

            _kernelParser.UdpIpRecvIPV6 += obj => ProcessNetworkEvent(obj, UpdIpV6TraceData);
            _kernelParser.UdpIpRecv += obj => ProcessNetworkEvent(obj, UdpIpTraceData);

            _kernelParser.UdpIpFail += obj => ProcessNetworkEvent(obj, UdpIpFailTraceData);

        }

        private void SetupRegistryEvents()
        {
            var RegistryTraceData = ConfigurationManager.AppSettings["RegistryTraceData"];

            _kernelParser.RegistryCreate += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryOpen += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryDelete += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQuery += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistrySetValue += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryDeleteValue += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQueryValue += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryEnumerateKey += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQueryMultipleValue += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            _kernelParser.RegistryEnumerateValueKey += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryFlush += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistrySetInformation += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBCreate += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBDelete += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBRundownBegin += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBRundownEnd += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryVirtualize += obj => ProcessRegistryEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryClose += obj => ProcessRegistryEvent(obj, RegistryTraceData);

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

            //_kernelParser.FileIOFSControl += obj => ProcessFileEvent(obj, FileIOInfoTraceData);
            //_kernelParser.FileIODirEnum += obj => ProcessFileEvent(obj, FileIODirEnumTraceData);
            //_kernelParser.FileIODirNotify += obj => ProcessFileEvent(obj, FileIODirEnumTraceData);
            //_kernelParser.FileIOOperationEnd += obj => ProcessFileEvent(obj, FileIOOpEndTraceData);
            _kernelParser.FileIORename += obj => ProcessFileEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIODelete += obj => ProcessFileEvent(obj, FileIOInfoTraceData);
            //_kernelParser.FileIOQueryInfo += obj => ProcessFileEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIOWrite += obj => ProcessFileEvent(obj, FileIOReadWriteTraceData);
            //_kernelParser.FileIOSetInfo += obj => ProcessFileEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIOUnmapFile += obj => ProcessFileEvent(obj, MapFileTraceData);
            _kernelParser.FileIOMapFileDCStart += obj => ProcessFileEvent(obj, MapFileTraceData);
            _kernelParser.FileIOMapFile += obj => ProcessFileEvent(obj, MapFileTraceData);
            //_kernelParser.FileIOName += obj => ProcessFileEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileCreate += obj => ProcessFileEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileDelete += obj => ProcessFileEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileRundown += obj => ProcessFileEvent(obj, FileIONameTraceData);
            _kernelParser.FileIOCreate += obj => ProcessFileEvent(obj, FileIOCreateTraceData);
            //_kernelParser.FileIOCleanup += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIOClose += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIOFlush += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIORead += obj => ProcessFileEvent(obj, FileIOReadWriteTraceData);
            _kernelParser.FileIOMapFileDCStop += obj => ProcessFileEvent(obj, MapFileTraceData);
        }

        #endregion
    }
}
