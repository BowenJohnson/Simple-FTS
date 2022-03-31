// Bowen Johnson

using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.IO;

namespace FTServer
{
    class FTConnectedClient
    {
        // represents a single connected ft client, that wants directory contents from the server
        // each client will have its own socket and thread
        // client is given it's socket from the FTServer when the server accepts the connection
        // the client class creates it's own thread
        // the client's thread will process messages on the client's socket

        private Socket clientSocket;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread clientThread;

        public FTConnectedClient(Socket clientSocket)
        {
            // save the client's socket
            this.clientSocket = clientSocket;

            // at this time, there is no stream, reader, writer or thread
            stream = null;
            reader = null;
            writer = null;
            clientThread = null;          
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

            // the param is a FTConnectedClient instance
            // start processing messages with the Run() method
            (param as FTConnectedClient).Run();
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
                    
                    // handle the message
                    if (msg == "get")
                    {
                        // get directoryName
                        string directoryName = reader.ReadLine();
                        Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                            "Recieved get from connected client for directory " + directoryName);

                        // retrieve directory contents and sending all the files
                        DirectoryInfo di = new DirectoryInfo(directoryName);

                        // if directory does not exist! send an error!
                        if (!di.Exists)
                        {
                            SendError("Directory does not exist!");
                            continue;
                        }

                        // directory exists, send each file to the client...
                        // for each file...
                        foreach (FileInfo fi in di.GetFiles())
                        {
                            // get the file's name
                            string fileName = fi.Name;
                            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Found file: " + fileName);

                            // make sure it's a txt file
                            if (fi.Extension == ".txt")
                            {
                                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "File is a .txt file");

                                // get the file contents
                                string fileContents = fi.OpenText().ReadToEnd();
                                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "File contents: " + fileContents);

                                // send a file to the client
                                SendFileName(fileName, fileContents.Length);
                                SendFileContents(fileContents);
                            }
                        }

                        // send done after last file
                        SendDone();
                    }

                    else if (msg == "exit")
                    {
                        // client is done, close it's socket and quit the thread
                        done = true;
                        Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Recieved exit from connected client");
                    }
                    
                    // else invalid message
                    else
                    {
                        // error handling for an invalid message
                        SendError("Invalid message: " + msg);

                        // this client is too broken to waste our time on!
                        // quite processing messages and disconnect
                        done = true;
                        Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                            "Recieved invalid message from connected client, closing connection");
                    }
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                    "Error on client socket, closing connection: " + se.Message);
            }
            catch (IOException ie)
            {
                Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                    "Error reading/writing network stream, closing connection: " + ie.Message);
            }

            // close the client's writer, reader, network stream and socket
            writer.Close();
            reader.Close();
            stream.Close();
            clientSocket.Disconnect(false);
            clientSocket.Close();
        }

        private void SendFileName(string fileName, int fileLength)
        {
            // send file name and file length message
            writer.WriteLine(fileName);
            writer.WriteLine(fileLength.ToString());
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                "Sent file name to client: " + fileName + ", " + fileLength.ToString());
        }

        private void SendFileContents(string fileContents)
        {
            // send file contents only
            // NOTE: no \n at end of contents
            writer.Write(fileContents);
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + 
                "Sent file contents to client: " + fileContents);
        }

        private void SendDone()
        {
            // send done message
            writer.WriteLine("done");
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent done to connected client");
        }

        private void SendError(string errorMessage)
        {
            // send error message
            writer.WriteLine("error");
            writer.WriteLine(errorMessage);
            writer.Flush();
            Console.WriteLine("[" + clientThread.ManagedThreadId.ToString() + "] " + "Sent error to connected client: " + errorMessage);
        }
    }
}
