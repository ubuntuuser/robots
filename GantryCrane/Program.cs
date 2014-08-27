//
//  Program.cs
//
//  Author:
//       Johannes Doblmann <s1310595014@students.fh-hagenberg.at>
//
//  Copyright (c) 2014 Johannes Doblmann
using System;
using System.Net.Sockets;
using System.Net;
using MonoBrickFirmware;
using MonoBrickFirmware.Display;
using MonoBrickFirmware.Sensors;
using MonoBrickFirmware.Movement;
using System.Threading;
using MonoBrickFirmware.UserInput;

namespace GantryCrane {
	class MainClass {
		public static void Main(string[] args) {
			LcdConsole.WriteLine ("Hello Lcd");

			//---------------------------------

			TcpListener serverSocket = new TcpListener(IPAddress.Any, 8888);
			int requestCount = 0;
			TcpClient clientSocket = default(TcpClient);
			serverSocket.Start ();
			LcdConsole.WriteLine (">> Server started");
			clientSocket = serverSocket.AcceptTcpClient ();
			LcdConsole.WriteLine (">> Waiting for connection from client");
			bool stop = false;

			while (!stop) {
				try {
					++requestCount;
					NetworkStream networkStream = clientSocket.GetStream ();
					byte[] bytesFrom = new byte[1000025];
					networkStream.Read (bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
					string dataFromClient = System.Text.Encoding.ASCII.GetString (bytesFrom);
					dataFromClient = dataFromClient.Substring (0, dataFromClient.IndexOf ("$"));
					LcdConsole.WriteLine (">> Data: " + dataFromClient);
					string serverResponse = "Server Response " + Convert.ToString (requestCount);
					byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes (serverResponse);
					networkStream.Write (sendBytes, 0, sendBytes.Length);
					networkStream.Flush ();
					LcdConsole.WriteLine (">> " + serverResponse);
					stop = true;
				} catch (Exception e) {
					LcdConsole.WriteLine (e.ToString ());
					stop = true;
				}
			}

			clientSocket.Close ();
			serverSocket.Stop ();
			System.Threading.Thread.Sleep (3000);
			LcdConsole.WriteLine (">> Exit");

		}

		private void processMessage(string message) {
			switch (message.Split (' ') [0]) {
			case "move":
				LcdConsole.WriteLine ("moving");
				break;
			default:
				LcdConsole.WriteLine ("Unknown Message");
				break;
			}
		}
	}
}
