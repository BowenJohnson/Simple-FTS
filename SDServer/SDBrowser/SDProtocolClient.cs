// Bowen Johnson

using System;
using System.Collections.Generic;
using System.Text;
using PRSLib;
using System.Net;
using System.Net.Sockets;
using System.IO;
using SDClientLib;

namespace SDBrowser
{
    // implements IProtocolClient
    // uses the SD protcol
    // keeps track of sessions for each SD Server
    //  appropriately opens or resumes a session
    //  closes the session when the protocol client is closed
    // retrieves a single requested "document" by name
    // implemented using the SDClient class in SDClientLib project

    class SDProtocolClient : IProtocolClient
    {
        // represents an open session for a single SD Server
        private class SDSession
        {
            public string ipAddr;
            public ushort port;
            public ulong sessionId;

            public SDSession(string ipAddr, ushort port, ulong sessionId)
            {
                this.ipAddr = ipAddr;
                this.port = port;
                this.sessionId = sessionId;
            }
        }

        private string prsIP;
        private ushort prsPort;
        private Dictionary<string, SDSession> sessions;     // server IP address --> session  info on the SD server


        public SDProtocolClient(string prsIP, ushort prsPort)
        {
            // save the PRS server's IP address and port
            // will be used later to lookup the port for the SD Server when needed
            this.prsIP = prsIP;
            this.prsPort = prsPort;

            // initially empty dictionary of sessions
            sessions = new Dictionary<string, SDSession>();
        }

        public string GetDocument(string serverIP, string documentName)
        {
            // retrieve requested document from the specified server
            // manage the session with the SD Server
            //  opening or resuming as needed
            // connect to and disconnect from the server w/in this method

            // SDProtocolClient.GetDocument() make sure we have valid parameters
            // serverIP is the SD Server's IP address
            // documentName is the name of a docoument on the SD Server
            // both should not be empty
            if (string.IsNullOrWhiteSpace(serverIP))
                throw new Exception("Server IP required!");

            if (string.IsNullOrWhiteSpace(documentName))
                throw new Exception("Docuent name is required!");

            // contact the PRS and lookup port for "SD Server"
            PRSClient prs = new PRSClient(prsIP, prsPort, "SD Server");
            ushort sdPort = prs.LookupPort();

            // connect to SD server by ipAddr and port
            // use OpenOrResumeSession() to ensure session is handled correctly
            SDClient sd = OpenOrResumeSession(serverIP, sdPort);

            // get the requested document from the SD Server
            string documentContent = sd.GetDocument(documentName);

            // disconnect from server
            sd.Disconnect();

            // return the content
            return documentContent;
        }

        public void Close()
        {
            // close each open session with the various servers

            // for each session...
            foreach (SDSession session in sessions.Values)
            {
                // connect to the SD Server's IP address and port
                // leave the SDClient connected and return it for further communication to the server
                SDClient sd = new SDClient(session.ipAddr, session.port);
                sd.Connect();

                // create network stream, reader and writer
                sd.SessionID = session.sessionId;
                sd.CloseSession();
                              
                // disconnect from server and close the socket
                sd.Disconnect();
            }

        }

        private SDClient OpenOrResumeSession(string ipAddr, ushort port)
        {
            // create and connect an SDClient instance to the given SD Server
            // open or resume a session
            // leave the SDClient connected and return it for further communication to the server
            SDClient sd = new SDClient(ipAddr, port);
            sd.Connect();

            // do we already have a session for this server?
            if (sessions.ContainsKey(ipAddr))
            {
                // yes, session already open
                // retrieve the sessionId and send resume message to server
                sd.ResumeSession(sessions[ipAddr].sessionId);
            }
            else
            {
                // no, session not open for this server
                // open a new session and save the sessionId
                sd.OpenSession();

                // save this open session in the sessions dictionary for later
                sessions[ipAddr] = new SDSession(ipAddr, port, sd.SessionID);
            }
            
            // keep the SDClient open and return it
            return sd;
        }       
    }
}
