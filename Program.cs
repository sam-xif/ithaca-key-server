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


namespace IthacaKeyServer
{
    public enum MessageType
    {
        Error,
        Fatal,
        Warning,
        Status,
        Verbose,    /* For extra informational messages */
        Debug       /* For debug messages */
    }

    class Program
    {
        // Global vars

        // Log file
        static FileStream logFile;

        // Port Number
        static int portNumber = 80;

        // Verbose
        static bool verboseFlag = false;

        // Quiet (overrides verbose messages)
        static bool quietFlag = false;

        // Silent (suppresses everything except errors)
        static bool silentFlag = false;

        // Log output
        static bool logFlag = false;

        // Set if help is to be printed
        static bool helpFlag = false;

        // Exit Code
        static int exitCode = 0;

        static void Main(string[] args)
        {
            SetOptionFlags(args);

            if (helpFlag)
            {
                Usage();
                Console.ReadLine();
                return;
            }

            HttpListener httpListener = new HttpListener();
            portNumber = 8080;

            try
            {
                string prefixString = "http://+:" + portNumber.ToString();

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
                PrintExitMsg(exitCode);
                return;
            }

            PrintMsg("HTTP Listener has begun listening for requests.", MessageType.Status);

            // Request processing loop
            while (exitCode == 0)
            {
                httpListener.BeginGetContext(HttpCallback, httpListener);
            }


        }


        static void HttpCallback(IAsyncResult result)
        {
            try
            {
                if (!result.IsCompleted)
                    result.AsyncWaitHandle.WaitOne();

                HttpListenerContext context = ((HttpListener)result.AsyncState).EndGetContext(result);
                ProcessRequest(context);

                // Clean Up
                result.AsyncWaitHandle.Close();
            }
            catch (Exception e)
            {
                exitCode = 1;
                PrintExceptionMsg(e);
                PrintExitMsg(exitCode);
            }
        }

        static void ProcessRequest(HttpListenerContext context)
        {
            Stream responseStream = context.Response.OutputStream;

            if (context.Request.HttpMethod != "GET")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                byte[] response = Encoding.UTF8.GetBytes(FormHttpErrorResponse("Method Not Allowed", context.Response.StatusCode));
                responseStream.Write(response, 0, response.Length);
                responseStream.Close();
                return;
            }

        }

        static string FormHttpErrorResponse(string errorMsg, int errorCode)
        {
            return string.Format("<HTML><HEAD><TITLE>Error {0} - {1}</TITLE></HEAD><BODY><H1>Error {0} - {1}</H1>If you believe this is a mistake, please contact the server administrator.</BODY></HTML>", 
                errorCode.ToString(), errorMsg);
        }

        // Prints a formatted message
        static void PrintMsg(string msg, MessageType type)
        {
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
            }

            Console.WriteLine(string.Format("[{0}] {1}: {2}", msgTypeStr, DateTime.Now.ToString(), msg));
        }

        static void PrintExitMsg(int code)
        {
            PrintMsg("Exiting with code " + exitCode.ToString(), MessageType.Status);
        }

        static void PrintExceptionMsg(Exception e)
        {
            PrintMsg(e.Message, MessageType.Error);
            PrintMsg("SOURCE: " + e.Source, MessageType.Debug);
            PrintMsg("STACK TRACE: " + e.StackTrace, MessageType.Debug);
        }


        static void SetOptionFlags(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                CLOptions opt = CLOptionUtil.GetOptionFromString(args[i]);
                switch (opt)
                {
                    case CLOptions.Help:
                        helpFlag = true;
                        break;
                }
            }
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

Notes:
  --quiet and --very-quiet both suppress debug messages, but all messages, printed or not can be logged with --log-output.
            ");
        }
    }
}
