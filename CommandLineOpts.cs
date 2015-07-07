using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using IthacaKeyServer.Config;

namespace IthacaKeyServer
{
    public enum CLOptions
    {
        GenerateLogFile,    /* --gen-log-file, -l */
        Quiet,              /* --quiet, -q */
        VeryQuiet,          /* --very-quite, --silent, -s */
        Verbose,            /* --verbose, -v */
        Debug,              /* --debug, -d */
        Help,
        None
    }

    public static class CLOptionUtil
    {
        public static Dictionary<string, CLOptions> OptionDictionary { get; private set; }

        private static bool m_dictionaryInitialized = false;

        private static void InitializeDictionary()
        {
            OptionDictionary = new Dictionary<string, CLOptions>();

            OptionDictionary.Add("--log-output", CLOptions.GenerateLogFile);
            OptionDictionary.Add("-l", CLOptions.GenerateLogFile);
            
            OptionDictionary.Add("--quiet", CLOptions.Quiet);
            OptionDictionary.Add("-q", CLOptions.Quiet);

            OptionDictionary.Add("--very-quiet", CLOptions.VeryQuiet);
            OptionDictionary.Add("--silent", CLOptions.VeryQuiet);
            OptionDictionary.Add("-s", CLOptions.VeryQuiet);

            OptionDictionary.Add("--verbose", CLOptions.Verbose);
            OptionDictionary.Add("-v", CLOptions.Verbose);

            OptionDictionary.Add("--debug", CLOptions.Debug);
            OptionDictionary.Add("-d", CLOptions.Debug);

            OptionDictionary.Add("--help", CLOptions.Help);
            OptionDictionary.Add("-h", CLOptions.Help);

            m_dictionaryInitialized = true;
        }

        /* Returns CLOptions.None if string doesn't match
         * If there are multiple flags, such as -vd (verbose and debug)
         * */
        public static CLOptions GetOptionFromString(string arg)
        {
            if (!m_dictionaryInitialized)
                InitializeDictionary();

            try
            {
                return OptionDictionary[arg];
            }
            catch
            {
                // Assume KeyNotFoundException
                return CLOptions.None;
            }
        }
    }
}
