// Bowen Johnson

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;

namespace FTClientLib
{
    public class FTClient
    {
        public class Directory
        {
            public class File
            {
                public string Name { get; }
                public string Contents { get; }
                public File(string name, string contents) { Name = name; Contents = contents; }
            }

            public string Name { get; }
            public List<File> Files { get; }

            public Directory(string name){ Name = name; Files = new List<File>(); }
            public void AddFile(string name, string contents) { Files.Add(new File(name, contents)); }
        }

        private string ftServerAddress;
        private ushort ftServerPort;
        bool connected;
        Socket clientSocket;
        NetworkStream stream;
        StreamReader reader;
        StreamWriter writer;

        public FTClient(string ftServerAddress, ushort ftServerPort)
        {
            // save server address/port
            this.ftServerAddress = ftServerAddress;
            this.ftServerPort = ftServerPort;

            // initialize to not connected to server
            connected = false;
            clientSocket = null;
            stream = null;
            reader = null;
            writer = null;
        }

        public void Connect()
        {
            if (!connected)
            {
                // create a client socket and connect to the FT Server's IP address and port\
                clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ftServerAddress), ftServerPort));

                // establish the network stream, reader and writer
                stream = new NetworkStream(clientSocket);
                reader = new StreamReader(stream, UTF8Encoding.ASCII);
                writer = new StreamWriter(stream, UTF8Encoding.ASCII);

                // now connected
                connected = true;
                Console.WriteLine("Connected to FT server");
            }
        }

        public void Disconnect()
        {
            if (connected)
            {
                // send exit to FT server
                SendExit();

                // close writer, reader and stream
                writer.Close();
                reader.Close();
                stream.Close();

                // disconnect and close socket
                clientSocket.Disconnect(false);
                clientSocket.Close();

                // now disconnected
                connected = false;
                clientSocket = null;
                stream = null;
                writer = null;
                reader = null;
            }
        }

        public Directory GetDirectory(string directoryName)
        {
            // send get to the server for the specified directory and receive files
            if (connected)
            {
                // send get command for the directory
                SendGet(directoryName);
                //SendInvalidMessage();

                // create empty dir to hold results
                Directory dir = new Directory(directoryName);

                // receive and process files until "done"
                while (ReceiveFile(dir)) ;

                return dir;
            }

            return null;
        }

        #region implementation

        private void SendGet(string directoryName)
        {
            // send get message for the directory
            writer.WriteLine("get");
            writer.WriteLine(directoryName);
            writer.Flush();
            Console.WriteLine("Sent get to FT server for directory " + directoryName);
        }

        private void SendExit()
        {
            // send exit message
            writer.WriteLine("exit");
            writer.Flush();
            Console.WriteLine("Sent exit to FT server");
        }

        private void SendInvalidMessage()
        {
            // allows for testing of server's error handling code
            writer.WriteLine("garbage");
            writer.Flush();
            Console.WriteLine("Sent garbage to FT server");
        }

        private bool ReceiveFile(Directory dir)
        {
            // receive a single file from the server, stores it in the directory param and returns true
            // returns false if not more files throw exception if there is an error

            // expect file name from server
            string fileName = reader.ReadLine();

            // when the server sends "done", then there are no more files!
            if (fileName == "done")
            {
                Console.WriteLine("Recieved done from server");
                return false;
            }

            // handle error messages from the server
            else if (fileName == "error")
            {
                string msg = reader.ReadLine();
                Console.WriteLine("Received error from server: " + msg);
                throw new Exception(msg);
            }

            // received a file name
            Console.WriteLine("Recieved file name from server: " + fileName);

            // receive file length from server
            int fileLength = int.Parse(reader.ReadLine());

            // receive file contents
            string fileContents = "";

            // loop until all of the file contents are received
            int charsToRead = fileLength;

            while (charsToRead > 0)
            {
                // receive as many characters from the server as available
                char[] buffer = new char[charsToRead];
                int actuallyRead = reader.Read(buffer, 0, charsToRead);

                // accumulate bytes read into the contents
                fileContents += new string(buffer);
                charsToRead -= actuallyRead;
            }
            Console.WriteLine("Recieved file contents from server: " + fileContents);

            // add the file to the Directory parameter
            dir.AddFile(fileName, fileContents);
         
            return true;
        }

        #endregion
    }
}
