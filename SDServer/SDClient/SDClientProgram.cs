using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using PRSLib;
using SDClientLib;

namespace SDClient
{
    class SDClientProgram
    {
        private static void Usage()
        {
            /*
                -prs <PRS IP address>:<PRS port>
                -s <SD server IP address>
		        -o | -r <session id> | -c <session id>
                [-get <document> | -post <document>]
            */
            Console.WriteLine("Usage: SDClient [-prs <PRS IP>:<PRS port>] [-s <SD Server IP>]");
            Console.WriteLine("\t-o | -r <session id> | -c <session id>");
            Console.WriteLine("\t[-get <document> | -post <document>]");
        }

        static void Main(string[] args)
        {
            // defaults
            string PRSSERVER_IPADDRESS = "127.0.0.1";
            ushort PRSSERVER_PORT = 30000;
            string SDSERVICE_NAME = "SD Server";
            string SDSERVER_IPADDRESS = "127.0.0.1";
            ushort SDSERVER_PORT = 40000;
            string SESSION_CMD = null;
            ulong SESSION_ID = 0;
            string DOCUMENT_CMD = null;
            string DOCUMENT_NAME = null;

            // process the command line arguments
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-o")
                    {
                        SESSION_CMD = "-o";
                    }
                    else if (args[i] == "-r")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-r requires an argument");
                        SESSION_ID = ulong.Parse(args[++i]);
                        SESSION_CMD = "-r";
                    }
                    else if (args[i] == "-c")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-c requires an argument");
                        SESSION_ID = ulong.Parse(args[++i]);
                        SESSION_CMD = "-c";
                    }
                    else if (args[i] == "-post")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-post requires an argument");
                        DOCUMENT_NAME = args[++i];
                        DOCUMENT_CMD = "-post";
                    }
                    else if (args[i] == "-get")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-get requires an argument");
                        DOCUMENT_NAME = args[++i];
                        DOCUMENT_CMD = "-get";
                    }
                    else if (args[i] == "-prs")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-prs needs a value!");
                        // split ip and port
                        string[] stringParts = args[++i].Split(':');
                        if (stringParts.Length != 2)
                            throw new Exception("-prs expects <IPaddress:<port>!");
                        PRSSERVER_IPADDRESS = stringParts[0];
                        PRSSERVER_PORT = ushort.Parse(stringParts[1]);
                    }
                    else if (args[i] == "-s")
                    {
                        if (i + 1 >= args.Length)
                            throw new Exception("-s needs a value!");
                        SDSERVER_IPADDRESS = args[++i];
                    }
                    else
                    {
                        throw new Exception("Invalid cmd lind param!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return;
            }

            Console.WriteLine("PRS Address: " + PRSSERVER_IPADDRESS);
            Console.WriteLine("PRS Port: " + PRSSERVER_PORT);
            Console.WriteLine("SD Server Address: " + SDSERVER_IPADDRESS);
            Console.WriteLine("Session Command: " + SESSION_CMD);
            Console.WriteLine("Session Id: " + SESSION_ID);
            Console.WriteLine("Document Command: " + DOCUMENT_CMD);
            Console.WriteLine("Document Name: " + DOCUMENT_NAME);

            try
            {
                // contact the PRS and lookup port for "SD Server"
                PRSClient prs = new PRSClient(PRSSERVER_IPADDRESS, PRSSERVER_PORT, SDSERVICE_NAME);
                SDSERVER_PORT = prs.LookupPort();

                // create an SDClient to use in talking to the server
                SDClientLib.SDClient sd = new SDClientLib.SDClient(SDSERVER_IPADDRESS, SDSERVER_PORT);
                sd.Connect();
                Console.WriteLine("Connected to server");
                
                // send session command to server
                if (SESSION_CMD == "-o")
                {
                    // open new session
                    Console.WriteLine("Opening new session...");
                    sd.OpenSession();
                    Console.WriteLine("Opened session: " + sd.SessionID.ToString()); ;
                }
                else if (SESSION_CMD == "-r")
                {
                    // resume existing session
                    Console.WriteLine("Resuming session " + SESSION_ID.ToString() + "...");
                    sd.ResumeSession(SESSION_ID);
                    Console.WriteLine("Resumed session: " + SESSION_ID.ToString());
                }
                else if (SESSION_CMD == "-c")
                {
                    // close existing session
                    Console.WriteLine("Closing session " + SESSION_ID.ToString() + "...");
                    sd.SessionID = SESSION_ID;
                    sd.CloseSession();
                    Console.WriteLine("Closed session: " + SESSION_ID.ToString());
                }
                
                // send document request to server
                if (DOCUMENT_CMD == "-post")
                {
                    // read the document contents from stdin
                    string content = Console.In.ReadToEnd();

                    // send the document to the server
                    sd.PostDocument(DOCUMENT_NAME, content);

                }
                else if (DOCUMENT_CMD == "-get")
                {
                    // get document from the server
                    string content = sd.GetDocument(DOCUMENT_NAME);

                    // print out the received document
                    Console.WriteLine("Recieved content: "+ content);
                }

                // disconnect from the server
                sd.Disconnect();
                Console.WriteLine("Disconnected from server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            // wait for a keypress from the user before closing the console window
            // NOTE: the following commented out as they cannot be used when redirecting input to post a file
            //Console.WriteLine("Press Enter to exit");
            //Console.ReadKey();
        }
    }
}
