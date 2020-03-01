using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ETWLogger.Library
{
    /// <summary>
    /// Class used to filter the three log types with the provided regular expression strings.
    /// </summary>
    class CustomEventFilter
    {
        /// <summary>
        /// Logger used for any necessary information
        /// </summary>
        //private static readonly Logger logger = LogManager.GetLogger("GeneralLogger");

        private Regex FileRegex;

        private Regex NetRegex;

        private Regex RegRegex;

        private Regex NotFileRegex;

        private Regex NotRegRegex;

        private Regex NotNetRegex;

        /// <summary>
        /// Regex string used in file filter method to check if present.
        /// </summary>
        public string FileRegexString
        {
            set
            {
                FileRegex = new Regex(value);
            } 
        }

        /// <summary>
        /// Regex string used in network filter method to check if present.
        /// </summary>
        public string NetRegexString 
        {
            set
            {
                NetRegex = new Regex(value);
            } 
        }

        /// <summary>
        /// Regex string used in registry filter method to check if present.
        /// </summary>
        public string RegRegexString 
        { 
            set 
            {
                RegRegex = new Regex(value);
            } 
        }

        /// <summary>
        /// Regex string used in file filter method to check if not present.
        /// </summary>
        public string NotFileRegexString
        {
            set
            {
                NotFileRegex = new Regex(value);
            }
        }

        /// <summary>
        /// Regex string used in network filter method to check if not present.
        /// </summary>
        public string NotNetRegexString
        {
            set
            {
                NotNetRegex = new Regex(value);
            }
        }

        /// <summary>
        /// Regex string used in registry filter method to check if not present.
        /// </summary>
        public string NotRegRegexString
        {
            set
            {
                NotRegRegex = new Regex(value);
            }
        }

        /// <summary>
        /// Constructor of the filter class.
        /// </summary>
        /// <param name="fileRegexString">Regex string used in file filter method to check if present.</param>
        /// <param name="netRegexString">Regex string used in network filter method to check if present.</param>
        /// <param name="regRegexString">Regex string used in registry filter method to check if present.</param>
        /// <param name="notFileRegexString">Regex string used in file filter method to check if not present.</param>
        /// <param name="notNetRegexString">Regex string used in network filter method to check if not present.</param>
        /// <param name="notRegRegexString">Regex string used in registry filter method to check if not present.</param>
        public CustomEventFilter(
            string fileRegexString, 
            string netRegexString, 
            string regRegexString, 
            string notFileRegexString, 
            string notNetRegexString, 
            string notRegRegexString
            )
        {
            RegRegexString = regRegexString;
            NetRegexString = netRegexString;
            FileRegexString = fileRegexString;
            NotFileRegexString = notFileRegexString;
            NotNetRegexString = notNetRegexString;
            NotRegRegexString = notRegRegexString;
        }

        /// <summary>
        /// Checks if strings contains regex specified in RegRegexString and doesn't contain regex from NotRegRegexString 
        /// </summary>
        /// <param name="logMessage">Message string to be checked</param>
        /// <returns>True if message passes filtering, else False</returns>
        public bool RegCheck(string logMessage)
        {
            if (RegRegex.IsMatch(logMessage) == true)
            {
                if(NotRegRegex.IsMatch(logMessage) == false)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if strings contains regex specified in FileRegexString and doesn't contain regex from NotFileRegexString 
        /// </summary>
        /// <param name="logMessage">Message string to be checked</param>
        /// <returns>True if message passes filtering, else False</returns>
        public bool FileCheck(string logMessage)
        {
            if (FileRegex.IsMatch(logMessage) == true)
            {
                if (NotFileRegex.IsMatch(logMessage) == false)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if strings contains regex specified in NetRegexString and doesn't contain regex from NotNetRegexString 
        /// </summary>
        /// <param name="logMessage">Message string to be checked</param>
        /// <returns>True if message passes filtering, else False</returns>
        public bool NetCheck(string logMessage)
        {
            if (NetRegex.IsMatch(logMessage) == true)
            {
                if (NotNetRegex.IsMatch(logMessage) == false)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
