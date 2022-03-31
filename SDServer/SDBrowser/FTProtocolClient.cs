// Bowen Johnson


using System;
using System.Text;
using PRSLib;
using System.Net;
using System.Net.Sockets;
using System.IO;
using FTClientLib;

namespace SDBrowser
{
    // implements IProtocolClient
    // uses the FT protcol
    // retrieves an entire directory and represents it as a single text "document"

    class FTProtocolClient : IProtocolClient
    {
        private string prsIP;
        private ushort prsPort;

        public FTProtocolClient(string prsIP, ushort prsPort)
        {
            // save the PRS server's IP address and port
            // will be used later to lookup the port for the FT Server when needed
            this.prsIP = prsIP;
            this.prsPort = prsPort;
        }

        public string GetDocument(string serverIP, string documentName)
        {
            // make sure we have valid parameters
            // serverIP is the FT Server's IP address
            // documentName is the name of a directory on the FT Server
            // both should not be empty
            if (string.IsNullOrWhiteSpace(serverIP))
                throw new Exception("Server IP required!");

            if (string.IsNullOrWhiteSpace(documentName))
                throw new Exception("Docuent name is required!");

            // contact the PRS and lookup port for "FT Server"
            PRSClient prs = new PRSClient(prsIP, prsPort, "FT Server");
            ushort ftPort = prs.LookupPort();

            // connect to FT server by ipAddr and port
            FTClient ft = new FTClient(serverIP, ftPort);
            ft.Connect();

            // send request to server to get directory
            FTClient.Directory dir = ft.GetDirectory(documentName);

            // translate the files in the dir into a result string
            string result = "";
            foreach (FTClient.Directory.File f in dir.Files)
            {
                result += f.Name + "\r\n";
                result += f.Contents + "\r\n";
                result += "\r\n";
            }

            // disconnect from server and close the socket
            ft.Disconnect();

            // return the content
            return result;
        }

        public void Close()
        {
            // nothing to do here!
            // the FT Protocol does not expect a client to close a session
            // everything is handled in the GetDocument() method
        }
    }
}
