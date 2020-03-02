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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;

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

        private readonly bool _logOwner;

        private readonly string _ownerKey;

        private readonly ManagementObjectSearcher _processSearcher;

        delegate void ProcessEvent(TraceEvent data, string formatString);

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
                    ConfigurationManager.AppSettings["ProcRegexString"],
                    ConfigurationManager.AppSettings["NotFileRegexString"],
                    ConfigurationManager.AppSettings["NotNetRegexString"],
                    ConfigurationManager.AppSettings["NotRegRegexString"],
                    ConfigurationManager.AppSettings["NotProcRegexString"]
                    );

                
                if (Boolean.TryParse(ConfigurationManager.AppSettings["LogOwner"], out _logOwner))
                {
                    _ownerKey = ConfigurationManager.AppSettings["OwnerKey"];
                    _logger.Info("Logging process owners with key \"" + _ownerKey + "\"");
                    _processSearcher = new ManagementObjectSearcher();
                }
                else
                {
                    _logOwner = false;
                }

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
            _processSearcher.Dispose();
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

        private void ProcessNetworkEventWithOwner(TraceEvent data, string formatString)
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

                var procOwner = GetProcessOwner(data.ProcessID) ?? "NO OWNER";
                formatString = formatString.Replace(_ownerKey, procOwner);

                if (_filter.NetCheck(formatString))
                {
                    _netLogger.Info(formatString);
                }
            }
            catch (Exception x)
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

        private void ProcessRegistryEventWithOwner(TraceEvent data, string formatString)
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

                var procOwner = GetProcessOwner(data.ProcessID) ?? "NO OWNER";
                formatString = formatString.Replace(_ownerKey, procOwner);

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

        private void ProcessFileEventWithOwner(TraceEvent data, string formatString)
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

                var procOwner = GetProcessOwner(data.ProcessID) ?? "NO OWNER";
                formatString = formatString.Replace(_ownerKey, procOwner);

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

        private void ProcessProcessEvent(TraceEvent data, string formatString)
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
                if (_filter.ProcCheck(formatString))
                {
                    _procLogger.Info(formatString);
                }
            }
            catch (Exception x)
            {
                _logger.Error(x, "Failed to properly log " + data.ProcessName);
            }
        }

        private void ProcessProcessEventWithOwner(TraceEvent data, string formatString)
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

                var procOwner = GetProcessOwner(data.ProcessID) ?? "NO OWNER";
                formatString = formatString.Replace(_ownerKey, procOwner);

                if (_filter.ProcCheck(formatString))
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

            ProcessEvent processEvent;

            if(_logOwner == true)
            {
                processEvent = ProcessProcessEventWithOwner;
            }
            else
            {
                processEvent = ProcessProcessEvent;
            }

            _kernelParser.ProcessStart += obj => processEvent(obj, ProcessTraceData);
            //_kernelParser.ProcessStop += obj => processEvent(obj, ProcessTraceData);
            _kernelParser.ProcessStartGroup += obj => processEvent(obj, ProcessTraceData);
            _kernelParser.ProcessDCStart += obj => processEvent(obj, ProcessTraceData);
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

            ProcessEvent processEvent;

            if (_logOwner == true)
            {
                processEvent = ProcessNetworkEventWithOwner;
            }
            else
            {
                processEvent = ProcessNetworkEvent;
            }

            _kernelParser.TcpIpAcceptIPV6 += obj => processEvent(obj, TcpIpV6ConnectTraceData);
            _kernelParser.TcpIpAccept += obj => processEvent(obj, TcpIpConnectTraceData);

            _kernelParser.TcpIpConnectIPV6 += obj => processEvent(obj, TcpIpV6ConnectTraceData);
            _kernelParser.TcpIpConnect += obj => processEvent(obj, TcpIpConnectTraceData);

            _kernelParser.TcpIpDisconnectIPV6 += obj => processEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpDisconnect += obj => processEvent(obj, TcpIpTraceData);
            
            _kernelParser.TcpIpSendIPV6 += obj => processEvent(obj, TcpIpV6SendTraceData);
            _kernelParser.TcpIpSend += obj => processEvent(obj, TcpIpSendTraceData);

            _kernelParser.TcpIpRecvIPV6 += obj => processEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpRecv += obj => processEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpTCPCopyIPV6 += obj => processEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpTCPCopy += obj => processEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpRetransmitIPV6 += obj => processEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpRetransmit += obj => processEvent(obj, TcpIpTraceData);

            //_kernelParser.TcpIpARPCopy += obj => processEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpFullACK += obj => processEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpPartACK += obj => processEvent(obj, TcpIpTraceData);
            //_kernelParser.TcpIpDupACK += obj => processEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpReconnectIPV6 += obj => processEvent(obj, TcpIpV6TraceData);
            _kernelParser.TcpIpReconnect += obj => processEvent(obj, TcpIpTraceData);

            _kernelParser.TcpIpFail += obj => processEvent(obj, TcpIpFailTraceData);
        
            _kernelParser.UdpIpSendIPV6 += obj => processEvent(obj, UpdIpV6TraceData);
            _kernelParser.UdpIpSend += obj => processEvent(obj, UdpIpTraceData);

            _kernelParser.UdpIpRecvIPV6 += obj => processEvent(obj, UpdIpV6TraceData);
            _kernelParser.UdpIpRecv += obj => processEvent(obj, UdpIpTraceData);

            _kernelParser.UdpIpFail += obj => processEvent(obj, UdpIpFailTraceData);

        }

        private void SetupRegistryEvents()
        {
            var RegistryTraceData = ConfigurationManager.AppSettings["RegistryTraceData"];

            ProcessEvent processEvent;

            if (_logOwner == true)
            {
                processEvent = ProcessRegistryEventWithOwner;
            }
            else
            {
                processEvent = ProcessRegistryEvent;
            }

            _kernelParser.RegistryCreate += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryOpen += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryDelete += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQuery += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistrySetValue += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryDeleteValue += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQueryValue += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryEnumerateKey += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryQueryMultipleValue += obj => processEvent(obj, RegistryTraceData);
            _kernelParser.RegistryEnumerateValueKey += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryFlush += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistrySetInformation += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBCreate += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBDelete += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBRundownBegin += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryKCBRundownEnd += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryVirtualize += obj => processEvent(obj, RegistryTraceData);
            //_kernelParser.RegistryClose += obj => processEvent(obj, RegistryTraceData);

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

            ProcessEvent processEvent;

            if (_logOwner == true)
            {
                processEvent = ProcessFileEventWithOwner;
            }
            else
            {
                processEvent = ProcessFileEvent;
            }

            //_kernelParser.FileIOFSControl += obj => processEvent(obj, FileIOInfoTraceData);
            //_kernelParser.FileIODirEnum += obj => processEvent(obj, FileIODirEnumTraceData);
            //_kernelParser.FileIODirNotify += obj => processEvent(obj, FileIODirEnumTraceData);
            //_kernelParser.FileIOOperationEnd += obj => processEvent(obj, FileIOOpEndTraceData);
            _kernelParser.FileIORename += obj => processEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIODelete += obj => processEvent(obj, FileIOInfoTraceData);
            //_kernelParser.FileIOQueryInfo += obj => processEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIOWrite += obj => processEvent(obj, FileIOReadWriteTraceData);
            //_kernelParser.FileIOSetInfo += obj => processEvent(obj, FileIOInfoTraceData);
            _kernelParser.FileIOUnmapFile += obj => processEvent(obj, MapFileTraceData);
            _kernelParser.FileIOMapFileDCStart += obj => processEvent(obj, MapFileTraceData);
            _kernelParser.FileIOMapFile += obj => processEvent(obj, MapFileTraceData);
            //_kernelParser.FileIOName += obj => processEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileCreate += obj => processEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileDelete += obj => processEvent(obj, FileIONameTraceData);
            //_kernelParser.FileIOFileRundown += obj => processEvent(obj, FileIONameTraceData);
            _kernelParser.FileIOCreate += obj => processEvent(obj, FileIOCreateTraceData);
            //_kernelParser.FileIOCleanup += obj => processEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIOClose += obj => processEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIOFlush += obj => processEvent(obj, FileIOSimpleOpTraceData);
            _kernelParser.FileIORead += obj => processEvent(obj, FileIOReadWriteTraceData);
            _kernelParser.FileIOMapFileDCStop += obj => processEvent(obj, MapFileTraceData);
        }

        #endregion
    
        private string GetProcessOwner(int processId)
        {
            var queryString = "Select * From Win32_Process Where ProcessID = " + processId;

            _processSearcher.Query = new ObjectQuery(queryString);

            var procList = _processSearcher.Get();

            foreach(ManagementObject obj in procList)
            {
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));

                if (returnVal == 0)
                {
                    // return DOMAIN\user
                    return argList[1] + "\\" + argList[0];

                }
            }
            return null;
        }
    }
}
