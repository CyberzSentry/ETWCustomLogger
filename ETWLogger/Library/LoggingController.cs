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
        private static readonly Logger logger = LogManager.GetLogger("GeneralLogger");

        private TraceEventSession _kernelSession;

        private KernelTraceEventParser _kernelParser;

        private Thread _processingThread;

        private Thread _eventsLostThread;

        private bool _eventsLostSwitch = true;

        private CustomEventFilter _filter;

        public LoggingController()
        {
            try
            {

                logger.Info("Initialising the ETWEngine Class.");
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

                //if (_kernelSession.IsRealTime)
                //{
                //    logger.Info("Kernel session is real time");
                //}
                //else
                //{
                //    logger.Info("Kernel session is NOT real time");
                //}

                _kernelParser = new KernelTraceEventParser(_kernelSession.Source);
                _processingThread = new Thread(() => { _kernelSession.Source.Process(); });
                _processingThread.Priority = ThreadPriority.Highest;
                _processingThread.IsBackground = true;

                _eventsLostThread = new Thread(() =>
                {
                    while (_eventsLostSwitch)
                    {
                        Thread.Sleep(10000);
                        LogLostEvents();
                    }
                });

                _filter = new CustomEventFilter(
                    ConfigurationManager.AppSettings["FileRegexString"],
                    ConfigurationManager.AppSettings["NetRegexString"],
                    ConfigurationManager.AppSettings["RegRegexString"],
                    ConfigurationManager.AppSettings["NotFileRegexString"],
                    ConfigurationManager.AppSettings["NotNetRegexString"],
                    ConfigurationManager.AppSettings["NotRegRegexString"]
                    );

                logger.Info("Seting up network events.");
                SetupNetworkEvents();

                logger.Info("Seting up registry events.");
                SetupRegistryEvents();

                logger.Info("Seting up file events.");
                SetupFileEvents();

                logger.Info("Seting up process events.");
                SetupProcessEvents();
            }
            catch (Exception x)
            {
                logger.Error(x, "Failed to properly create LoggingController");
            }
        }

        public void Start()
        {
            try
            {
                logger.Info("Starting processing events");
                if (!(TraceEventSession.IsElevated() ?? false))
                {
                    logger.Error("Program doesn't have administrative privilege and might not work properly.");
                    return;
                }
                _processingThread.Start();
                _eventsLostThread.Start();
            } catch (Exception x)
            {
                logger.Error(x, "Filed to start processing events.");
            }
        }

        public void LogLostEvents()
        {
            logger.Info("Lost " + _kernelSession.EventsLost + " events");
        }

        public void Dispose()
        {
            _eventsLostSwitch = false;
            _kernelSession.Dispose();
        }

        private void ProcessNetworkEvent(TraceEvent data, string formatString, Logger eventLogger)
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
                    //outputNet.WriteLine(formatString);
                    eventLogger.Info(formatString);
                }
            }
            catch(Exception x)
            {
                logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessRegistryEvent(TraceEvent data, string formatString, Logger eventLogger)
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
                    
                    eventLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessFileEvent(TraceEvent data, string formatString, Logger eventLogger)
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
                    //outputFile.WriteLine(formatString);
                    eventLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessProcessEvents(TraceEvent data, string formatString, Logger eventLogger)
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
                    //outputFile.WriteLine(formatString);
                    eventLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        #region Event setup methods

        private void SetupProcessEvents()
        {
            var ProcessTraceData = ConfigurationManager.AppSettings["ProcessTraceData"];

            var ETWProcLogger = LogManager.GetLogger("ETWProcLogger");
            _kernelParser.ProcessStart += obj => ProcessProcessEvents(obj, ProcessTraceData, ETWProcLogger);
            //_kernelParser.ProcessStop += obj => ProcessProcessEvents(obj, ProcessTraceData, ETWProcLogger);
            _kernelParser.ProcessStartGroup += obj => ProcessProcessEvents(obj, ProcessTraceData, ETWProcLogger);
            _kernelParser.ProcessDCStart += obj => ProcessProcessEvents(obj, ProcessTraceData, ETWProcLogger);
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
            
            var ETWNetLogger = LogManager.GetLogger("ETWNetLogger");

            _kernelParser.TcpIpAcceptIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6ConnectTraceData, ETWNetLogger);
            _kernelParser.TcpIpAccept += obj => ProcessNetworkEvent(obj, TcpIpConnectTraceData, ETWNetLogger);

            _kernelParser.TcpIpConnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6ConnectTraceData, ETWNetLogger);
            _kernelParser.TcpIpConnect += obj => ProcessNetworkEvent(obj, TcpIpConnectTraceData, ETWNetLogger);

            _kernelParser.TcpIpDisconnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData, ETWNetLogger);
            _kernelParser.TcpIpDisconnect += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);
            
            _kernelParser.TcpIpSendIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6SendTraceData, ETWNetLogger);
            _kernelParser.TcpIpSend += obj => ProcessNetworkEvent(obj, TcpIpSendTraceData, ETWNetLogger);

            _kernelParser.TcpIpRecvIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData, ETWNetLogger);
            _kernelParser.TcpIpRecv += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);

            _kernelParser.TcpIpTCPCopyIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData, ETWNetLogger);
            _kernelParser.TcpIpTCPCopy += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);

            _kernelParser.TcpIpRetransmitIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData, ETWNetLogger);
            _kernelParser.TcpIpRetransmit += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);

            //_kernelParser.TcpIpARPCopy += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);
            //_kernelParser.TcpIpFullACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);
            //_kernelParser.TcpIpPartACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);
            //_kernelParser.TcpIpDupACK += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);

            _kernelParser.TcpIpReconnectIPV6 += obj => ProcessNetworkEvent(obj, TcpIpV6TraceData, ETWNetLogger);
            _kernelParser.TcpIpReconnect += obj => ProcessNetworkEvent(obj, TcpIpTraceData, ETWNetLogger);

            _kernelParser.TcpIpFail += obj => ProcessNetworkEvent(obj, TcpIpFailTraceData, ETWNetLogger);
        
            _kernelParser.UdpIpSendIPV6 += obj => ProcessNetworkEvent(obj, UpdIpV6TraceData, ETWNetLogger);
            _kernelParser.UdpIpSend += obj => ProcessNetworkEvent(obj, UdpIpTraceData, ETWNetLogger);

            _kernelParser.UdpIpRecvIPV6 += obj => ProcessNetworkEvent(obj, UpdIpV6TraceData, ETWNetLogger);
            _kernelParser.UdpIpRecv += obj => ProcessNetworkEvent(obj, UdpIpTraceData, ETWNetLogger);

            _kernelParser.UdpIpFail += obj => ProcessNetworkEvent(obj, UdpIpFailTraceData, ETWNetLogger);

        }

        private void SetupRegistryEvents()
        {
            var RegistryTraceData = ConfigurationManager.AppSettings["RegistryTraceData"];

            var ETWRegLogger = LogManager.GetLogger("ETWRegLogger");

            _kernelParser.RegistryCreate += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryOpen += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryDelete += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryQuery += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistrySetValue += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryDeleteValue += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryQueryValue += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryEnumerateKey += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryQueryMultipleValue += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            _kernelParser.RegistryEnumerateValueKey += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryFlush += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistrySetInformation += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryKCBCreate += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryKCBDelete += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryKCBRundownBegin += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryKCBRundownEnd += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryVirtualize += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);
            //_kernelParser.RegistryClose += obj => ProcessRegistryEvent(obj, RegistryTraceData, ETWRegLogger);

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

            var ETWFileLogger = LogManager.GetLogger("ETWFileLogger");

            //_kernelParser.FileIOFSControl += obj => ProcessFileEvent(obj, FileIOInfoTraceData, ETWFileLogger);
            //_kernelParser.FileIODirEnum += obj => ProcessFileEvent(obj, FileIODirEnumTraceData, ETWFileLogger);
            //_kernelParser.FileIODirNotify += obj => ProcessFileEvent(obj, FileIODirEnumTraceData, ETWFileLogger);
            //_kernelParser.FileIOOperationEnd += obj => ProcessFileEvent(obj, FileIOOpEndTraceData, ETWFileLogger);
            _kernelParser.FileIORename += obj => ProcessFileEvent(obj, FileIOInfoTraceData, ETWFileLogger);
            _kernelParser.FileIODelete += obj => ProcessFileEvent(obj, FileIOInfoTraceData, ETWFileLogger);
            //_kernelParser.FileIOQueryInfo += obj => ProcessFileEvent(obj, FileIOInfoTraceData, ETWFileLogger);
            _kernelParser.FileIOWrite += obj => ProcessFileEvent(obj, FileIOReadWriteTraceData, ETWFileLogger);
            //_kernelParser.FileIOSetInfo += obj => ProcessFileEvent(obj, FileIOInfoTraceData, ETWFileLogger);
            _kernelParser.FileIOUnmapFile += obj => ProcessFileEvent(obj, MapFileTraceData, ETWFileLogger);
            _kernelParser.FileIOMapFileDCStart += obj => ProcessFileEvent(obj, MapFileTraceData, ETWFileLogger);
            _kernelParser.FileIOMapFile += obj => ProcessFileEvent(obj, MapFileTraceData, ETWFileLogger);
            //_kernelParser.FileIOName += obj => ProcessFileEvent(obj, FileIONameTraceData, ETWFileLogger);
            //_kernelParser.FileIOFileCreate += obj => ProcessFileEvent(obj, FileIONameTraceData, ETWFileLogger);
            //_kernelParser.FileIOFileDelete += obj => ProcessFileEvent(obj, FileIONameTraceData, ETWFileLogger);
            //_kernelParser.FileIOFileRundown += obj => ProcessFileEvent(obj, FileIONameTraceData, ETWFileLogger);
            _kernelParser.FileIOCreate += obj => ProcessFileEvent(obj, FileIOCreateTraceData, ETWFileLogger);
            //_kernelParser.FileIOCleanup += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData, ETWFileLogger);
            _kernelParser.FileIOClose += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData, ETWFileLogger);
            _kernelParser.FileIOFlush += obj => ProcessFileEvent(obj, FileIOSimpleOpTraceData, ETWFileLogger);
            _kernelParser.FileIORead += obj => ProcessFileEvent(obj, FileIOReadWriteTraceData, ETWFileLogger);
            _kernelParser.FileIOMapFileDCStop += obj => ProcessFileEvent(obj, MapFileTraceData, ETWFileLogger);
        }

        #endregion
    }
}
