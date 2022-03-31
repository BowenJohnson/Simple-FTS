// Bowen Johnson

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using PRSLib;

namespace PRSServer
{
    class PRSServerProgram
    {
        class PRS
        {
            // represents a PRS Server, keeps all state and processes messages accordingly

            class PortReservation
            {
                private ushort port;
                private bool available;
                private string serviceName;
                private DateTime lastAlive;

                public PortReservation(ushort port)
                {
                    this.port = port;
                    available = true;
                }

                public string ServiceName { get { return serviceName; } }
                public ushort Port { get { return port; } }
                public bool Available { get { return available; } }

                public bool Expired(int timeout)
                {
                    // return true if timeout seconds have elapsed since lastAlive
                    return (DateTime.Now - lastAlive).Seconds > timeout;
                }

                public void Reserve(string serviceName)
                {
                    // reserve this port for serviceName
                    available = false;
                    this.serviceName = serviceName;
                    lastAlive = DateTime.Now;
                }

                public void KeepAlive()
                {
                    // save current time in lastAlive
                    lastAlive = DateTime.Now;
                }

                public void Close()
                {
                    // make this reservation available
                    available = true;
                    serviceName = null;
                }
            }

            // server attribues
            private ushort startingClientPort;
            private ushort endingClientPort;
            private int keepAliveTimeout;
            private int numPorts;
            private PortReservation[] ports;
            private bool stopped;

            public PRS(ushort startingClientPort, ushort endingClientPort, int keepAliveTimeout)
            {
                // save parameters
                this.startingClientPort = startingClientPort;
                this.endingClientPort = endingClientPort;
                this.keepAliveTimeout = keepAliveTimeout;

                // initialize to not stopped
                stopped = false;

                // initialize port reservations
                numPorts = endingClientPort - startingClientPort + 1;
                ports = new PortReservation[numPorts];

                for (ushort i = 0; i < numPorts; i++)
                {
                    ports[i] = new PortReservation((ushort)(startingClientPort + i));
                }
                
            }

            public bool Stopped { get { return stopped; } }

            private void CheckForExpiredPorts()
            {
                // expire any ports that have not been kept alive
                foreach (PortReservation reservation in ports)
                {
                    if (!reservation.Available && reservation.Expired(keepAliveTimeout))
                    {
                        reservation.Close();
                    }
                }
            }

            private PRSMessage RequestPort(string serviceName)
            {
                // client has requested the lowest available port, so find it!
                foreach (PortReservation reservation in ports)
                {
                    // if found an avialable port, reserve it and send SUCCESS
                    if (reservation.Available)
                    {
                        reservation.Reserve(serviceName);
                        return new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, serviceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                    }
                }

                // else, none available, send ALL_PORTS_BUSY
                return new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, serviceName, 0, PRSMessage.STATUS.ALL_PORTS_BUSY);
            }

            public PRSMessage HandleMessage(PRSMessage msg)
            {
                // handle one message and return a response

                // check for expired ports before handling any messages
                CheckForExpiredPorts();

                PRSMessage response = null;

                switch (msg.MsgType)
                {
                    case PRSMessage.MESSAGE_TYPE.REQUEST_PORT:
                        {
                            // send requested report
                            response = RequestPort(msg.ServiceName);
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.KEEP_ALIVE:
                        {
                            // client has requested that we keep their port alive
                            // find the port
                            foreach (PortReservation reservation in ports)
                            {
                                // if found, keep it alive and send SUCCESS
                                if (!reservation.Available && reservation.Port == msg.Port && reservation.ServiceName == msg.ServiceName)
                                {
                                    reservation.KeepAlive();
                                    response =  new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SUCCESS);
                                }
                            }

                            if (response == null)
                            {
                                // else, SERVICE_NOT_FOUND
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                            }                
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.CLOSE_PORT:
                        {
                            // client has requested that we close their port, and make it available for others!
                            // find the port
                            foreach (PortReservation reservation in ports)
                            {
                                // if found, keep it alive and send SUCCESS
                                if (!reservation.Available && reservation.Port == msg.Port && reservation.ServiceName == msg.ServiceName)
                                {
                                    reservation.Close();
                                    response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SUCCESS);
                                }
                            }

                            if (response == null)
                            {
                                // else, SERVICE_NOT_FOUND
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                            }
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.LOOKUP_PORT:
                        {
                            // client wants to know the reserved port number for a named service
                            foreach (PortReservation reservation in ports)
                            {
                                // if found, keep it alive and send SUCCESS
                                if (!reservation.Available && reservation.ServiceName == msg.ServiceName)
                                {
                                    response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                                }
                            }

                            if (response == null)
                            {
                                // else, SERVICE_NOT_FOUND
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                             
                            }
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.STOP:
                        {
                            // client is telling us to close the appliation down
                            // stop the PRS and return SUCCESS
                            response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, "", 0, PRSMessage.STATUS.SUCCESS);
                            stopped = true;
                        }
                        break;
                }

                return response;
            }

        }

        static void Usage()
        {
            Console.WriteLine("usage: PRSServer [options]");
            Console.WriteLine("\t-p < service port >");
            Console.WriteLine("\t-s < starting client port number >");
            Console.WriteLine("\t-e < ending client port number >");
            Console.WriteLine("\t-t < keep alive time in seconds >");
        }

        static void Main(string[] args)
        {
            // defaults
            ushort SERVER_PORT = 30000;
            ushort STARTING_CLIENT_PORT = 40000;
            ushort ENDING_CLIENT_PORT = 40099;
            int KEEP_ALIVE_TIMEOUT = 300;

            // process command options
            // -p < service port >
            // -s < starting client port number >
            // -e < ending client port number >
            // -t < keep alive time in seconds >

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-p")
                    {
                        SERVER_PORT = ushort.Parse(args[++i]);
                    }
                    else if (args[i] == "-s")
                    {
                        STARTING_CLIENT_PORT = ushort.Parse(args[++i]);
                    }
                    else if (args[i] == "e")
                    {
                        ENDING_CLIENT_PORT = ushort.Parse(args[++i]);
                    }
                    if (args[i] == "-t")
                    {
                        KEEP_ALIVE_TIMEOUT = int.Parse(args[++i]);
                    }
                }


            // check for valid STARTING_CLIENT_PORT and ENDING_CLIENT_PORT
            if (STARTING_CLIENT_PORT > ENDING_CLIENT_PORT)
            {
                Console.WriteLine("Error: We're Doomed the start is bigger than the end!!!");
                return;
            }

            // print out parameter values
            Console.WriteLine("SERVER_PORT: " + SERVER_PORT.ToString());
            Console.WriteLine("STARTING_CLIENT_PORT: " + STARTING_CLIENT_PORT.ToString());
            Console.WriteLine("ENDING_CLIENT_PORT: " + ENDING_CLIENT_PORT.ToString());
            Console.WriteLine("KEEP_ALIVE_TIMEOUT: " + KEEP_ALIVE_TIMEOUT.ToString());

            // initialize the PRS server
            PRS prs = new PRS(STARTING_CLIENT_PORT, ENDING_CLIENT_PORT, KEEP_ALIVE_TIMEOUT);

            // create the socket for receiving messages at the server
            Socket listeningSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            // bind the listening socket to the PRS server port
            listeningSocket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));
            
            //
            // Process client messages
            //

            while (!prs.Stopped)
            {
                EndPoint remoteEndPoint = null;
                try
                {
                    // receive a message from a client
                    remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    PRSMessage requestMsg = PRSMessage.ReceiveMessage(listeningSocket, ref remoteEndPoint);

                    // let the PRS handle the message
                    PRSMessage response = prs.HandleMessage(requestMsg);

                    // send response message back to client
                    response.SendMessage(listeningSocket, remoteEndPoint);                    
                }
                catch (Exception ex)
                {
                    // attempt to send a UNDEFINED_ERROR response to the client, if we know who that was
                    PRSMessage errorMsg = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, null, 0, PRSMessage.STATUS.UNDEFINED_ERROR);
                    errorMsg.SendMessage(listeningSocket, remoteEndPoint);
                }
            }

            // close the listening socket
            listeningSocket.Close();
            
            // wait for a keypress from the user before closing the console window
            Console.WriteLine("Press Enter to exit");
            Console.ReadKey();
        }
    }
}
