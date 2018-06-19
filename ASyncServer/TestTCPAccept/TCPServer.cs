﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Net;

namespace Servers
{
    /// <summary>
    /// Object to store stuff for TCPAsyncListener
    /// </summary>
    public class StateObject
    {
        public Socket workSocket = null;
        public const int Buffersize = 1024;
        public byte[] buffer = new byte[Buffersize];
        public StringBuilder sb = new StringBuilder();
    }

    /// <summary>
    /// The TCPListener
    /// Port: 15000;
    /// </summary>
    public class TCPAsyncListener
    {
        // Just some variables
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private const int port = 15000;

        public static void StartListening()
        {
            // Create the things we need for a server
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // bind the socket and listen
                listener.Bind(localEndPoint);
                listener.Listen(100);

                allDone.Reset();

                // Begin accepting connections
                listener.BeginAccept(new AsyncCallback(AcceptCallBack), listener);

                allDone.WaitOne();
                listener.Close();
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void AcceptCallBack(IAsyncResult ar)
        { 

            Console.WriteLine("Waiting for information...");

            // Socket that was plugged in
            Socket listener = (Socket)ar.AsyncState;
            // Completed the server thing -- now this socket contains all of the information
            Socket handler = listener.EndAccept(ar);

            // Create a stateobject to hold the socket
            StateObject state = new StateObject();
            state.workSocket = handler;
            
            // There's a buffer, buffersize... etc. Passing in state.
            handler.BeginReceive(state.buffer, 0, StateObject.Buffersize, 0, new AsyncCallback(ReadCallBack), state);
        }

        public static void ReadCallBack(IAsyncResult ar)
        {
            String content = String.Empty;

            // There's an asynchronous state object -- get the state object and handler object
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Endreceive stores number of bytes received.
            int bytesRead = handler.EndReceive(ar);

            // Append to the stringbuilder -> print out the result
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
            content = state.sb.ToString();
            Console.WriteLine("\nThe client sent this: {0}\n", content);
            Send(handler, content);
        }

        public static void Send(Socket handler, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallBack), handler);
        }

        private static void SendCallBack(IAsyncResult ar)
        {
            Socket handler = (Socket)ar.AsyncState;

            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

            InitializeServer.UDPDone.Set();
            allDone.Set();
        }
    }

    /// <summary>
    /// A class to represent all of the work done through a UDP connection.
    /// UDPAsyncListener Port: 8888
    /// </summary>
    public class UDPAsyncListener
    {
        const int listenPort = 8888;
        const int replyPort = 9999;

        // Initializing the listener
        public static UdpClient listener = new UdpClient(listenPort);

        /// <summary>
        /// Waits for data from the client -- do we even need to use beginreceive? hmm... I'll leave it in there for now.
        /// </summary>
        public static void StartListening()
        {
            try
            {
                Console.WriteLine("UDP Server Launched. Waiting for connection...");
                listener.BeginReceive(new AsyncCallback(ReadCallBack), listener);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void ReadCallBack(IAsyncResult res)
        {
            UdpClient client = (UdpClient)res.AsyncState;
            IPEndPoint RemoteIPEndPoint = new IPEndPoint(IPAddress.Any, listenPort);

            Console.WriteLine("Receiving data...");
            byte[] received = client.EndReceive(res, ref RemoteIPEndPoint);
            string message = Encoding.ASCII.GetString(received, 0, received.Length);

            Console.WriteLine("Data received: \n {0} \n", message);
            Console.WriteLine("Waiting for sendback request...");

            byte[] toSendBack = client.Receive(ref RemoteIPEndPoint);
            string theReply = Encoding.ASCII.GetString(toSendBack, 0, toSendBack.Length);

            if (theReply.ToUpper() == "SENDBACK")
            {
                Console.WriteLine("Request for sendback received, sending back...");
                SendBack(RemoteIPEndPoint.Address, received, replyPort);
                InitializeServer.UDPDone.Set();
            }
            InitializeServer.UDPDone.Set();
        }

        public static void SendBack(IPAddress otherIP, byte[] data, int replyPort)
        {
            Socket thisSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint replyEndPoint = new IPEndPoint(otherIP, replyPort);

            thisSocket.SendTo(data, replyEndPoint);
            Console.WriteLine("Data sent back.");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// This class is used for initializing the server 
    /// One would have to specify a TCP or UDP connection (through UDP since it's faster) to send something over.
    /// InitializeServer Port: 11000.
    /// </summary>
    public class InitializeServer
    {
        // variables to be used
        public static ManualResetEvent UDPDone = new ManualResetEvent(false);
        public static UdpClient UDPlistener = new UdpClient(11000);
        const int port = 11000;

        /// <summary>
        /// Main method to start the listening process
        /// </summary>
        public static void StartListening()
        {
            while (true)
            {
                try
                {
                    UDPDone.Reset();
                    Console.WriteLine("---------------------------");
                    Console.WriteLine("Waiting for a connection...");
                    UDPlistener.BeginReceive(new AsyncCallback(ReadType), null);
                    UDPDone.WaitOne();
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        /// <summary>
        /// Callback to start the other processes.
        /// </summary>
        public static void ReadType(IAsyncResult ar)
        {
            IPEndPoint other = new IPEndPoint(IPAddress.Any, port);
            byte[] message = UDPlistener.EndReceive(ar, ref other); 
            string encodedMessage = Encoding.ASCII.GetString(message, 0, message.Length);

            if (encodedMessage.ToUpper() == "UDP")
            {
                Console.WriteLine("UDP socket requested. Launching UDP Server...");
                UDPAsyncListener.StartListening();
            }
            else if (encodedMessage.ToUpper() == "TCP")
            {
                Console.WriteLine("TCP socket requested. Launching TCP Server...");
                TCPAsyncListener.StartListening();
            }
            else
            {
                Console.WriteLine("Unknown signal: Socket type not specified.");
                Console.WriteLine("Debug Message: {0}", encodedMessage);
            }
        }
    }
}
