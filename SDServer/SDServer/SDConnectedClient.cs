// Bowen Johnson

using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.IO;

namespace SDServer
{
    class SDConnectedClient
    {
        // represents a single connected sd client
        // each client will have its own socket and thread while its connected
        // client is given it's socket from the SDServer when the server accepts the connection
        // this class creates it's own thread
        // the client's thread will process messages on the client's socket until it disconnects
        // NOTE: an sd client can connect/send messages/disconnect many times over it's lifetime

        private Socket clientSocket;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread clientThread;
        private SessionTable sessionTable;      // server's session table
        private ulong sessionId;                // session id for this session, once opened or resumed

        public SDConnectedClient(Socket clientSocket, SessionTable sessionTable)
        {
            // save the client's socket
            this.clientSocket = clientSocket;

            // at this time, there is no stream, reader, write or thread
            stream = null;
            reader = null;
            writer = null;
            clientThread = null;

            // save the server's session table
            this.sessionTable = sessionTable;

            // at this time, there is no session open
            sessionId = 0;
        }

        public void Start()
        {
            // called by the main thread to start the clientThread and process messages for the client

            // create and start the clientThread, pass in a reference to this class instance as a parameter
            clientThread = new Thread(ThreadProc);
            clientThread.Start(this);
        }

        private static void ThreadProc(Object param)
        {
            // the procedure for the clientThread
            // when this method returns, the clientThread will exit

            // the param is a SDConnectedClient instance
            // start processing messages with the Run() method
            (param as SDConnectedClient).Run();
        }

        private void Run()
        {
            // this method is executed on the clientThread           

            try
            {
                // create network stream, reader and writer over the socket
                stream = new NetworkStream(clientSocket);
                reader = new StreamReader(stream, UTF8Encoding.ASCII);
                writer = new StreamWriter(stream, UTF8Encoding.ASCII);

                // process client requests
                bool done = false;
                while (!done)
                {
                    // receive a message from the client
                    string msg = reader.ReadLine();
                    if (msg == null)
                    {
                        // no message means the client disconnected
                        // remember that the client will connect and disconnect as desired
                        done = true;
                        Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Client disconnected");
                    }
                    else
                    {
                        // handle the message
                        Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Recieved message from connected client: " + msg);
                        switch (msg)
                        {
                            case "open":
                                HandleOpen();
                                break;

                            case "resume":
                                HandleResume();
                                break;

                            case "close":
                                HandleClose();
                                break;

                            case "get":
                                HandleGet();
                                break;

                            case "post":
                                HandlePost();
                                break;

                            default:
                                {
                                    // error handling for an invalid message
                                    SendError("Invalid message!");

                                    // this client is too broken to waste our time on!
                                    done = true;
                                }
                                break;
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Error on client socket, closing connection: " + se.Message);
            }
            catch (IOException ioe)
            {
                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "IO Error on client socket, closing connection: " + ioe.Message);
            }

            // close the client's writer, reader, network stream and socket
            writer.Close();
            reader.Close();
            stream.Close();
            clientSocket.Disconnect(false);
            clientSocket.Close();
        }

        private void HandleOpen()
        {
            // handle an "open" request from the client

            // if no session currently open, then...
            if (sessionId == 0)
            {
                try
                {
                    // ask the SessionTable to open a new session and save the session ID
                    sessionId = sessionTable.OpenSession();

                    // send accepted message, with the new session's ID, to the client
                    SendAccepted(sessionId);
                }
                catch (SessionException se)
                {
                    SendError(se.Message);
                }
                catch (Exception ex)
                {
                    SendError(ex.Message);
                }
            }
            else
            {
                // error!  the client already has a session open!
                SendError("Session already open!");
            }
        }

        private void HandleResume()
        {
            // handle a "resume" request from the client

            // get the sessionId that the client just asked us to resume
            ulong resumeSessionId = ulong.Parse(reader.ReadLine());

            try
            {
                // if we don't have a session open currently for this client...
                if (sessionId == 0)
                {
                    // try to resume the session in the session table              
                    // if success, remember the session that we're now using and send accepted to client
                    if (sessionTable.ResumeSession(resumeSessionId))
                    {
                        sessionId = resumeSessionId;
                        SendAccepted(sessionId);
                    }
                    // if failed to resume session, send rejected to client
                    else
                    {
                        SendRejected("Cannot resume session!");
                    }
                }
                else
                {
                    // error! we already have a session open
                    SendError("Session already open, cannot resume!");
                }
            }
            catch (SessionException se)
            {
                SendError(se.Message);
            }
            catch (Exception ex)
            {
                SendError(ex.Message);
            }
        }

        private void HandleClose()
        {
            // handle a "close" request from the client

            // get the sessionId that the client just asked us to close
            ulong closeSessionId = ulong.Parse(reader.ReadLine());
            
            try
            {
                // close the session in the session table
                sessionTable.CloseSession(closeSessionId);

                // send closed message back to client
                SendClosed(closeSessionId);

                // record that this client no longer has an open session
                if (sessionId == closeSessionId)
                {
                    sessionId = 0;
                }
            }
            catch (SessionException se)
            {
                SendError(se.Message);
            }
            catch (Exception ex)
            {
                SendError(ex.Message);
            }
        }

        private void HandleGet()
        {
            // handle a "get" request from the client

            // if the client has a session open
            if (sessionId != 0)
            {
                try
                {
                    // get the document name from the client
                    string documentName = reader.ReadLine();

                    string documentContents = "";
                    // determine doc type
                    if (documentName.Length > 0)
                    {
                        if (documentName[0] == '/')
                        {
                            // if file...                            
                            string filePath = Path.Combine(Environment.CurrentDirectory, documentName.Substring(1));
                            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Client requested a file: " + filePath);
                            documentContents = File.ReadAllText(filePath);
                        }
                        else
                        {
                            // if session variable...
                            // get the document content from the session table
                            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Client requested a session variable: " + documentName);
                            documentContents = sessionTable.GetSessionValue(sessionId, documentName);
                        }

                        // send success and document to the client
                        SendSuccess(documentName, documentContents);
                    }
                    else
                    {
                        SendError("Document name empty!");
                    }
                }
                catch (SessionException se)
                {
                    SendError(se.Message);
                }
                catch (Exception ex)
                {
                    SendError(ex.Message);
                }
            }
            else
            {
                // error, cannot get without a session
                SendError("Cannot get document, no open session!");
            }
        }

        private void HandlePost()
        {
            // handle a "post" request from the client

            // if the client has a session open
            if (sessionId != 0)
            {
                try
                {
                    // get the document name, content length and contents from the client
                    string documentName = reader.ReadLine();
                    int contentLength = int.Parse(reader.ReadLine());
                    string documentContent = ReceiveDocument(contentLength);

                    if (documentName.Length > 0)
                    {
                        if (documentName[0] == '/')
                        {
                            // if file...                            
                            string filePath = Path.Combine(Environment.CurrentDirectory, documentName.Substring(1));
                            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Client requested a file: " + filePath);

                            // append content to the file on a new line
                            documentContent = "\n" + documentContent;
                            File.AppendAllText(filePath, documentContent);
                        }
                        else
                        {
                            // if session variable...
                            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Client requested a session variable: " + documentName);

                            // put the document into the session
                            sessionTable.PutSessionValue(sessionId, documentName, documentContent);
                        }
                        // send success to the client
                        SendSuccess();
                    }
                    else
                    {
                        SendError("Document name empty!");
                    }
                }
                catch (SessionException se)
                {
                    SendError(se.Message);
                }
                catch (Exception ex)
                {
                    SendError(ex.Message);
                }
            }
            else
            {
                // error, cannot post without a session
                SendError("Cannot post document, no open session!");
            }
        }

        private void SendAccepted(ulong sessionId)
        {
            // send accepted message to SD client, including session id of now open session
            writer.WriteLine("accepted");
            writer.WriteLine(sessionId.ToString());
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent accepted to connected client, sessionId: " + sessionId.ToString());
        }

        private void SendRejected(string reason)
        {
            // send rejected message to SD client, including reason for rejection
            writer.WriteLine("rejected");
            writer.WriteLine(reason);
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent rejected to connected client, reason: " + reason);
        }

        private void SendClosed(ulong sessionId)
        {
            // send closed message to SD client, including session id that was just closed
            writer.WriteLine("closed");
            writer.WriteLine(sessionId.ToString());
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent closed to connected client, sessionId: " + sessionId.ToString());
        }

        private void SendSuccess()
        {
            // send sucess message to SD client, with no further info
            // NOTE: in response to a post request
            writer.WriteLine("success");
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent success to connected client");
        }

        private void SendSuccess(string documentName, string documentContent)
        {
            // send success message to SD client, including retrieved document name, length and content
            // NOTE: in response to a get request
            writer.WriteLine("success");
            writer.WriteLine(documentName);
            writer.WriteLine(documentContent.Length);
            writer.Write(documentContent);
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                "Sent success to connected client, documentName=" + documentName + ", length=" + documentContent.Length + ", content=" + documentContent);
        }

        private void SendError(string errorString)
        {
            // send error message to SD client, including error string
            writer.WriteLine("error");
            writer.WriteLine(errorString);
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent error to connected client, msg: " + errorString);
        }

        private string ReceiveDocument(int length)
        {
            // receive a document from the SD client, of expected length
            // NOTE: as part of processing a post request

            // read from the reader until we've received the expected number of characters
            // accumulate the characters into a string and return those when we got enough
            string documentContents = "";
            int charsToRead = length;
            while (charsToRead > 0)
            {
                // receive as many characters from the server as available
                char[] buffer = new char[charsToRead];
                int actuallyRead = reader.Read(buffer, 0, charsToRead);

                // accumulate bytes read into the contents
                documentContents += new string(buffer);
                charsToRead -= actuallyRead;
            }
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Recieved document contents from connected client: " + documentContents);

            return documentContents;
        }
    }
}
