// Bowen Johnson
 
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace SDClientLib
{
    public class SDClient
    {
        private string sdServerAddress;
        private ushort sdServerPort;
        private bool connected;
        private ulong sessionID;
        Socket clientSocket;
        NetworkStream stream;
        StreamReader reader;
        StreamWriter writer;

        public SDClient(string sdServerAddress, ushort sdServerPort)
        {
            // save server address/port
            this.sdServerAddress = sdServerAddress;
            this.sdServerPort = sdServerPort;

            // initialize to not connected to server
            connected = false;
            clientSocket = null;
            stream = null;
            reader = null;
            writer = null;

            // no session open at this time
            sessionID = 0;
        }

        public ulong SessionID { get { return sessionID; } set { sessionID = value; } }

        public void Connect()
        {
            ValidateDisconnected();

            // create a client socket and connect to the FT Server's IP address and port
            clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse(sdServerAddress), sdServerPort));

            // establish the network stream, reader and writer
            stream = new NetworkStream(clientSocket);
            reader = new StreamReader(stream, UTF8Encoding.ASCII);
            writer = new StreamWriter(stream, UTF8Encoding.ASCII);

            // now connected
            connected = true;
        }

        public void Disconnect()
        {
            ValidateConnected();

            // close writer, reader and stream
            writer.Close();
            reader.Close();
            stream.Close();

            // disconnect and close socket
            clientSocket.Disconnect(false);
            clientSocket.Close();

            // now disconnected
            connected = false;
        }

        public void OpenSession()
        {
            ValidateConnected();

            // send open command to server
            SendOpen();

            // receive server's response, hopefully with a new session id
            sessionID = ReceiveSessionResponse();
        }

        public void ResumeSession(ulong trySessionID)
        {
            ValidateConnected();

            // send resume session to the server
            SendResume(trySessionID);
            
            // receive server's response, hopefully confirming our sessionId
            ulong resumedSessionID = ReceiveSessionResponse();

            // verify that we received the same session ID that we requested
            if (resumedSessionID != trySessionID)
                throw new Exception("Got the wrong session ID!");

            // save opened session
            sessionID = resumedSessionID;
        }

        public void CloseSession()
        {
            ValidateConnected();

            // send close session to the server
            SendClose(sessionID);

            // receive the response
            ulong closedSessionID = RecieveClosed();
            if (closedSessionID == sessionID)
            {
                // no session open
                sessionID = 0;
                Console.WriteLine("Session closed");
            }
            else
            {
                throw new Exception("Expected closed " + sessionID.ToString() + ", but recieved " + closedSessionID.ToString());
            }
        }

        public string GetDocument(string documentName)
        {
            ValidateConnected();

            // send get to the server
            SendGet(documentName);
            
            // get the server's response
            return ReceiveGetResponse();
        }

        public void PostDocument(string documentName, string documentContents)
        {
            ValidateConnected();

            // send the document to the server
            SendPost(documentName, documentContents);

            // get the server's response
            ReceivePostResponse();
        }

        private void ValidateConnected()
        {
            if (!connected)
                throw new Exception("Connot perform action. Not connected to server!");
        }

        private void ValidateDisconnected()
        {
            if (connected)
                throw new Exception("Connot perform action. Already connected to server!");
        }

        private void SendOpen()
        {
            /// send open message to SD server
            writer.WriteLine("open");
            writer.Flush();
            Console.WriteLine("Sent open to server");
        }

        private void SendClose(ulong sessionId)
        {
            // send close message to SD server
            writer.WriteLine("close");
            writer.WriteLine(sessionID.ToString());
            writer.Flush();
            Console.WriteLine("Sent close to server, sessionId: " + sessionID.ToString());
        }

        private ulong RecieveClosed()
        {
            // recieve the closed msg from the server, return the sessionID that was closed
            string line = reader.ReadLine();
            if (line == "closed")
            {
                line = reader.ReadLine();
                Console.WriteLine("Recieved closed from server, sessionID: " + line);
                return ulong.Parse(line);
            }
            else if (line == "error")
            {
                line = reader.ReadLine();
                Console.WriteLine("Recieved error from server, msg: " + line);
                throw new Exception(line);
            }
            else
            {
                throw new Exception("Expected to receive a valid close response, instead got... " + line);
            }
        }

        private void SendResume(ulong sessionId)
        {
            // send resume message to SD server
            writer.WriteLine("resume");
            writer.WriteLine(sessionId.ToString());
            writer.Flush();
            Console.WriteLine("Sent resume to server, sessionId: " + sessionId.ToString());
        }

        private ulong ReceiveSessionResponse()
        {
            // get SD server's response to our last session request (open or resume)
            string line = reader.ReadLine();
            if (line == "accepted")
            {
                // yay, server accepted our session!
                // get the sessionID
                line = reader.ReadLine();
                Console.WriteLine("Recieved accepted from server, sessionID: " + line);
                return ulong.Parse(line);
            }
            else if (line == "rejected")
            {
                // boo, server rejected us!
                line = reader.ReadLine();
                Console.WriteLine("Recieved rejected from server, reason: " + line);
                throw new Exception("session rejected: " + line);
            }
            else if (line == "error")
            {
                // boo, server sent us an error!
                line = reader.ReadLine();
                Console.WriteLine("Recieved error from server, msg: " + line);
                throw new Exception(line);
            }
            else
            {
                throw new Exception("Expected to receive a valid session response, instead got... " + line);
            }
        }

        private void SendPost(string documentName, string documentContents)
        {
            // send post message to SD erer, including document name, length and contents
            writer.WriteLine("post");
            writer.WriteLine(documentName);
            writer.WriteLine(documentContents.Length);
            writer.Write(documentContents);
            writer.Flush();
            Console.WriteLine("Sent post to server, name=" + documentName + ", length=" + documentContents.Length + ", contents=" + documentContents);
        }

        private void SendGet(string documentName)
        {
            // send get message to SD server
            writer.WriteLine("get");
            writer.WriteLine(documentName);
            writer.Flush();
            Console.WriteLine("Sent get to server, document name: " + documentName);
        }

        private void ReceivePostResponse()
        {
            // get server's response to our last post request
            string line = reader.ReadLine();
            if (line == "success")
            {
                // yay, server accepted our request!
                Console.WriteLine("Success!");
            }
            else if (line == "error")
            {
                // boo, server sent us an error!
                line = reader.ReadLine();
                throw new Exception(line);
            }
            else
            {
                throw new Exception("Expected to receive a valid post response, instead got... " + line);
            }
        }

        private string ReceiveGetResponse()
        {
            // get server's response to our last get request and return the content received
            string line = reader.ReadLine();
            if (line == "success")
            {
                // yay, server accepted our request!

                // read the document name, content length and content
                string documentName = reader.ReadLine();
                int contentLength = int.Parse(reader.ReadLine());
                string documentContent = ReceiveDocumentContent(contentLength);

                // return the content
                return documentContent;
            }
            else if (line == "error")
            {
                // boo, server sent us an error!
                string msg = reader.ReadLine();
                throw new Exception(msg);
            }
            else
            {
                throw new Exception("Expected to receive a valid get response, instead got... " + line);
            }
        }

        private string ReceiveDocumentContent(int length)
        {
            // read from the reader until we've received the expected number of characters
            // accumulate the characters into a string and return those when we received enough

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

            return documentContents;
        }
    }
}
