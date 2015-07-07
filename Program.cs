
// Define the debug directive. Undefine this for release builds.
#define DEBUG

// Define the symbol that tells the program to generate keys
// Will be used until a way to send a request to generate more keys is created.
// (Change to define when keys need to be added)
#undef ADDKEYS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using System.Xml;

using System.Net;
using System.Net.Security;

using System.Web;

using System.Threading;

using IthacaKeyServer.RequestProcessing;
using IthacaKeyServer.Config;


namespace IthacaKeyServer
{
    public enum MessageType
    {
        Error,
        Fatal,
        Warning,
        Status,
        Info,       /* Normal informational messages */
        Verbose,    /* For extra informational messages */
        Debug       /* For debug messages */
    }

    class Program
    {
        // Global vars

        static string version = "1.0.0";

        static Configuration config;

        // File Paths
        static string logFilePath;

        // Key Manager
        static KeyFileManager keyMgr;

        // Port Number
        static int portNumber;

        // Verbose
        static bool verboseFlag = false;

        // Quiet (hides verbose messages and info messages)
        static bool quietFlag = false;

        // Silent (suppresses everything except errors)
        static bool silentFlag = false;

        // Log output
        static bool logFlag = false;

        // Set if help is to be printed
        static bool helpFlag = false;

        // If debug is enabled, debug messages print no matter what.
        static bool debugFlag = false;

        // Exit Code
        static int exitCode = 0;

        static void Main(string[] args)
        {
            PrintMsg("\n\n\tStarting Ithaca Key Validation Service v" + version + ".\n", MessageType.Status);

#if DEBUG
            if (File.Exists("config.xml"))
            {
                File.Delete("config.xml");
                File.Copy("..\\..\\config.xml", "config.xml");
            }
            else
            {
                File.Copy("..\\..\\config.xml", "config.xml");
            }

#else

            if (!File.Exists("config.xml")) 
            {
                exitCode = 1;
                PrintMsg("No config.xml found. Cannot proceed.", MessageType.Fatal);
                PrintExitMsg(exitCode);
                return;
            }

#endif
            config = new Configuration();
            using (FileStream fs = File.OpenRead("config.xml"))
            {
                config.ParseConfigXml(fs);
            }

            logFlag = (bool)config.GetConfigValue("logoutput");
            if (logFlag)
            {
                logFilePath = (string)config.GetConfigValue("logfile");
                if (!File.Exists(logFilePath)) File.Create(logFilePath).Close();
                PrintMsg("Opening log file at " + logFilePath, MessageType.Status);
            }

            PrintMsg("Setting configuration variables...", MessageType.Status);
            string[] unmatched = SetOptionFlags(args);
            for (int i = 0; i < unmatched.Length; i++)
            {
                string opt = unmatched[i];
                opt = opt.TrimStart('-');
                string[] optSects = opt.Split(':');
                if (optSects.Length == 2)
                {
                    // It takes the form --name:val
                    string name = optSects[0];
                    string val = optSects[1];
                    config.SetConfigValue(name, val);
                }
                else if (optSects.Length == 3 && optSects[0] == "config")
                {
                    // It takes the form --config:name:val
                    string name = optSects[1];
                    string val = optSects[2];
                    config.SetConfigValue(name, val);
                }
                else
                {
                    PrintMsg("Unknown option '" + unmatched[i] + "'. Use --help for usage", MessageType.Fatal);
                    return;
                }
            }

            string keyFilePath = (string)config.GetConfigValue("keyfile");
            if (!File.Exists(keyFilePath)) File.Create(keyFilePath).Close();
            keyMgr = new KeyFileManager(keyFilePath);

#if ADDKEYS
            keyMgr.CreateKeys(128, "You have been whitelisted on the server!");
#endif

            try
            {
                keyMgr.BuildKeyHashtable();
            }
            catch (Exception e)
            {
                PrintExceptionMsg(e);
            }

            if (helpFlag)
            {
                Usage();
                return;
            }

            HttpListener httpListener = new HttpListener();
            portNumber = (int)config.GetConfigValue("port");

            try
            {
                string prefixString = "http://+:" + portNumber.ToString() + "/";

                // Accept all requests sent to port 8080.
                httpListener.Prefixes.Add(prefixString);
                PrintMsg("Setting accepted url prefix to " + prefixString, MessageType.Status);

                // Must be run as administrator to call Start().
                httpListener.Start();
            }
            catch (Exception e)
            {
                exitCode = 1;

                PrintExceptionMsg(e);
                PrintExitMsg();
                return;
            }

            PrintMsg("HTTP Listener has begun listening for requests.", MessageType.Status);

            using (RequestProcessingManager processor = new RequestProcessingManager(ProcessRequest))
            {

                // Request processing loop
                while (exitCode == 0)
                {
                    try
                    {
                        HttpListenerContext context = httpListener.GetContext();
                        
                        // Create new Request object
                        KeyAuthRequest request = new KeyAuthRequest(context);
                        
                        // Queue it
                        processor.QueueWorkItem(request);
                    }
                    catch (Exception e)
                    {
                        PrintExceptionMsg(e);
                        PrintMsg("Continuing...", MessageType.Status);
                    }
                }

                keyMgr.Close();
            }
        }

        static void ProcessRequest(object state)
        {
            lock (state)
            {
                KeyAuthRequest request = (KeyAuthRequest)state;
                HttpListenerContext context = request.Context;

                // Print information about the request if verbose is enabled:
                PrintMsg("Is Local: " + context.Request.IsLocal, MessageType.Verbose);
                PrintMsg("HTTP Method: " + context.Request.HttpMethod, MessageType.Info);

                Stream responseStream = context.Response.OutputStream;

                // Allowed methods are GET and OPTIONS
                byte[] response;

                if (context.Request.HttpMethod != "POST" && context.Request.HttpMethod != "OPTIONS")
                {
                    PrintMsg("This HTTP Method is not allowed on this server", MessageType.Info);
                    PrintMsg("Sending Error response...", MessageType.Info);
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    response = Encoding.UTF8.GetBytes(FormHttpErrorResponse("Method Not Allowed", context.Response.StatusCode));
                    responseStream.Write(response, 0, response.Length);
                    context.Response.Close();
                    return;
                }

                if (context.Request.HttpMethod == "OPTIONS") { SendOptions(context); return; }

                if (!context.Request.HasEntityBody)
                {
                    PrintMsg("Client Error: Client Sent Badly formed request", MessageType.Info);
                    PrintMsg("\tThe request does not contain an XML body.", MessageType.Info);
                    PrintMsg("Sending Error response...", MessageType.Info);
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response = Encoding.UTF8.GetBytes(FormHttpErrorResponse("Bad Request", context.Response.StatusCode));
                    responseStream.Write(response, 0, response.Length);
                    context.Response.Close();
                    return;
                }

                List<string> keys = new List<string>();
                string userName = string.Empty;

                using (Stream input = context.Request.InputStream)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(input);

                    XmlNode root = doc.FirstChild;

                    // Begin Parsing
                    if (!(root.Name == "keyRequest"))
                    {
                        PrintMsg("Client Sent Badly formed request", MessageType.Info);
                        PrintMsg("Sending Error response...", MessageType.Info);
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response = Encoding.UTF8.GetBytes(FormHttpErrorResponse("Bad Request", context.Response.StatusCode));
                        responseStream.Write(response, 0, response.Length);
                        context.Response.Close();
                        return;
                    }

                    try 
                    {
                        XmlNode userCreds = root["credentials"];
                        userName = userCreds["username"].FirstChild.Value;
                        XmlNode keyNode = root["keys"];
                        foreach (XmlNode key in keyNode.ChildNodes)
                        {
                            if (key.Name == "key")
                            {
                                keys.Add(key.FirstChild.Value);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        PrintExceptionMsg(e);
                        PrintMsg("Client Sent Badly formed request", MessageType.Info);
                        PrintMsg("Sending Error response...", MessageType.Info);
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response = Encoding.UTF8.GetBytes(FormHttpErrorResponse("Bad Request", context.Response.StatusCode));
                        responseStream.Write(response, 0, response.Length);
                        context.Response.Close();
                        return;
                    }
                }

                // Now, match against files:

                // GetKeyDesc checks if a key exists, gets its description, and returns it. It returns string.Empty if the key doesn't exist.
                string desc = string.Empty;
                try
                {
                    desc = keyMgr.GetKeyDesc(keys[0]);
                }
                catch (Exception e)
                {
                    PrintExceptionMsg(e);
                }
                if (desc != string.Empty)
                {

                    // For now, just return OK:
                    PrintMsg("Client sent correct key successfully", MessageType.Verbose);
                    PrintMsg("Sending response...", MessageType.Verbose);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.StatusDescription = "OK";
                    context.Response.Headers["Content-Type"] = "text/xml";
                    // TODO: Insert a description of what the key unlocks in the <desc> tag.
                    response = Encoding.UTF8.GetBytes("<keyResponse><status>OK</status><message>Key Matched Successfully!</message><desc>" 
                        + desc + "</desc></keyResponse>");
                    context.Response.KeepAlive = false;
                    context.Response.ContentLength64 = response.Length;
                    responseStream.Write(response, 0, response.Length);
                    context.Response.Close();
                }
                else
                {
                    PrintMsg("Client sent nonexistent key", MessageType.Verbose);
                    PrintMsg("Sending response...", MessageType.Verbose);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.StatusDescription = "OK";
                    context.Response.Headers["Content-Type"] = "text/xml";
                    response = Encoding.UTF8.GetBytes("<keyResponse><status>NONEXISTENT</status><message>Sorry, but the given key does not exist.</message></keyResponse>");
                    context.Response.KeepAlive = false;
                    context.Response.ContentLength64 = response.Length;
                    responseStream.Write(response, 0, response.Length);
                    context.Response.Close();
                }
            }

        }

        static void SendOptions(HttpListenerContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            context.Response.Headers["Access-Control-Max-Age"] = "1000";
            context.Response.Headers["Access-Control-Allow-Headers"] = "origin, content-type, accept";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.StatusDescription = "OK";
            context.Response.Close();
        }

        static string FormHttpErrorResponse(string errorMsg, int errorCode)
        {
            return string.Format("<HTML><HEAD><TITLE>Error {0} - {1}</TITLE></HEAD><BODY><H1>Error {0} - {1}</H1>If you believe this is a mistake, please contact the server administrator.</BODY></HTML>", 
                errorCode.ToString(), errorMsg);
        }

        // Prints a formatted message
        static void PrintMsg(string msg, MessageType type)
        {

            if (quietFlag && (type == MessageType.Info || type == MessageType.Verbose)) return;
            if (silentFlag && (type == MessageType.Warning || type == MessageType.Status)) return;

            if (!verboseFlag && type == MessageType.Verbose) return;

            // If debug is enabled, debug messages print no matter what.
            if (!debugFlag && type == MessageType.Debug) return;

            string msgTypeStr = string.Empty;
            switch (type)
            {
                case MessageType.Error:
                    msgTypeStr = "ERROR";
                    break;
                case MessageType.Fatal:
                    msgTypeStr = "FATAL";
                    break;
                case MessageType.Status:
                    msgTypeStr = "STATUS";
                    break;
                case MessageType.Warning:
                    msgTypeStr = "WARNING";
                    break;
                case MessageType.Debug:
                    msgTypeStr = "DEBUG";
                    break;
                case MessageType.Verbose:
                    msgTypeStr = "INFO";
                    break;
                case MessageType.Info:
                    msgTypeStr = "INFO";
                    break;
            }

            string write = string.Format("[{0}] {1}: {2}", msgTypeStr, DateTime.Now.ToString(), msg);
            Console.WriteLine(write);
            if (logFlag)
            {
                using (FileStream fs = File.Open(logFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(write);
                        sw.Flush();
                    }
                }
            }
        }

        static void PrintExitMsg()
        {
            PrintMsg("Exiting with code " + exitCode.ToString(), MessageType.Status);
        }

        static void PrintExceptionMsg(Exception e)
        {
            if (exitCode == 0) // Non-fatal
                PrintMsg(e.Message, MessageType.Error);
            else
                PrintMsg(e.Message, MessageType.Fatal);

            if (e.InnerException != null)
                PrintMsg("Inner Exception: " + e.InnerException.Message, MessageType.Debug);

            PrintMsg("SOURCE: " + e.Source, MessageType.Debug);
            PrintMsg("STACK TRACE: " + e.StackTrace, MessageType.Debug);
        }


        // Sets option flags, and returns an array of unmatched options.
        // TODO: Integrate config here too. Possibly change all options to config options then
        // set the bool flags based on the Configuration object, without the string[] args.
        static string[] SetOptionFlags(string[] args)
        {
            List<string> unmatchedArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                CLOptions opt = CLOptionUtil.GetOptionFromString(args[i]);
                switch (opt)
                {
                    case CLOptions.Help:
                        helpFlag = true;
                        break;
                    case CLOptions.Debug:
                        debugFlag = true;
                        break;
                    case CLOptions.Verbose:
                        verboseFlag = true;
                        break;
                    case CLOptions.GenerateLogFile:
                        logFlag = true;
                        break;
                    case CLOptions.Quiet:
                        quietFlag = true;
                        break;
                    case CLOptions.VeryQuiet:
                        silentFlag = true;
                        break;
                    default:
                        unmatchedArgs.Add(args[i]);
                        break;
                }

            }
            return unmatchedArgs.ToArray();
        }


        // Prints the usage of the program
        static void Usage()
        {
            Console.WriteLine(@"Ithaca Key Server (version 0.1.0)

Description:
  Receives requests from the Ithaca website, and returns responses.

Usage:
  keyserv [OPTION]

Options:
  --quiet, -q
        Suppress all output except for errors and warnings.
  --very-quiet, --silent, -s
        Suppress all output (including warnings), except for errors.
  --verbose, -v
        Print extra information.
  --debug, -d
        Print extra debug info.
  --log-output, -l
        Generate log file, with path specified in config.xml.
        If no path is specified, it writes to keyserv_log.txt.
  --help, -h
        Prints this message.
  --config:<OPTION>:<VALUE> or --<OPTION>:<VALUE>
        Sets a config option before the server starts.
  Examples:
    --logfile:<FILE>
          Sets the log file to be used. Equivalent to --config:logfile:<FILE>
    --port:<PORT-NUM>
          Sets the port number. Equivalent to --config:port:<PORT-NUM>

Notes:
  --quiet and --very-quiet both suppress debug messages, but all messages, printed or not can be logged with --log-output.
            ");
        }
    }
}
