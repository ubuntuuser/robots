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
using System.Collections.Generic;

namespace Reachstacker {
	class MainClass {
		private Motor motorFwd;
		private Motor motorTurn;
		private Motor motorSwitch;
		private Motor motorExtend;
		private bool extensionMode = false;
		private int maxHeight = 30000;
		private static bool stop = false;
		//TODO change
		private int heightContainerLevel1 = 100;
		//TODO change
		private int heightContainerLevel2 = 20000;
		private int currExtension;
		private int currHeight;
		private List<string> possiblePlaces = new List<string>() {
			"Start",
			"InFrontOfTruck",
			"InFrontOfContainerRow1",
			"InFrontOfContainerRow2"
		};

		public static void Main(string[] args) {
			LcdConsole.WriteLine ("Hello Lcd");
			Console.WriteLine ("stop={0}", stop);
			
			//---------------------------------

			TcpListener serverSocket = new TcpListener(IPAddress.Any, 8888);
			int requestCount = 0;
			TcpClient clientSocket = default(TcpClient);
			serverSocket.Start ();
			LcdConsole.WriteLine (">> Server started");
			clientSocket = serverSocket.AcceptTcpClient ();
			LcdConsole.WriteLine (">> Waiting for connection from client");

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

		private void initReachstacker() {
			motorFwd = new Motor(MotorPort.OutB);
			motorTurn = new Motor(MotorPort.OutC);
			motorSwitch = new Motor(MotorPort.OutA);
			motorExtend = new Motor(MotorPort.OutD);
		}

		private void processMessage(string message) {
			int units = 0;
			switch (message.Split ('$') [0]) {
			case "fwd":
				LcdConsole.WriteLine ("fwd");
				if (int.TryParse (message.Split ('$') [1], out units))
					move (units);
				break;
			case "bwd":
				LcdConsole.WriteLine ("bwd");
				if (int.TryParse (message.Split ('$') [1], out units))
					move (units * -1);
				break;
			case "turn":
				LcdConsole.WriteLine ("turn");
				break;
			case "lift":
				LcdConsole.WriteLine ("lift");
				break;
			case "lower":
				LcdConsole.WriteLine ("lower");
				break;
			case "ext":
				LcdConsole.WriteLine ("ext");
				break;
			case "retr":
				LcdConsole.WriteLine ("retr");
				break;
			case "stop":
				LcdConsole.WriteLine ("stop");
				stop = true;
				break;
			case "goto":
				LcdConsole.WriteLine ("goto");
				goTo (message.Split ('$') [1].Split ('#') [0], message.Split ('$') [1].Split ('#') [1]);
				break;
			default:
				LcdConsole.WriteLine ("Unknown Message");
				break;
			}
		}
		//----movement-functions--------------------------------------------------
		private void move(int units, uint speed = 30) {
			motorFwd.On ((sbyte)speed, (uint)units, true);
		}

		private void turn(uint units, uint speed = 30) {
			motorTurn.On ((sbyte)speed, units, false, false);
			//TODO motorFwd, zurückdrehen
		}

		private void lift(int units, int speed = 30) {
			if (extensionMode)
				switchMode ();
			liftTo (Math.Min (maxHeight, Math.Max (motorExtend.GetTachoCount () + units, 0)), speed);
		}

		private void liftTo(int position, int speed = 30) {
			if (extensionMode)
				switchMode ();
			motorExtend.MoveTo ((byte)speed, position + currExtension, true);
			currHeight = motorExtend.GetTachoCount () - currExtension;
		}

		private void switchMode() {
			Motor motorSwitch = new Motor(MotorPort.OutA);
			if (extensionMode) {
				LcdConsole.WriteLine ("Switching to height adjustment");
				motorSwitch.On (30, 1550, true);
				extensionMode = false;
			} else {
				LcdConsole.WriteLine ("Switching to extension mode");
				motorSwitch.On (-30, 1500, true);
				extensionMode = true;
			}
			motorSwitch.Off ();
		}

		private void goTo(string from, string to) {
			LcdConsole.WriteLine (from + " " + to);
			if (possiblePlaces.Contains (from) && possiblePlaces.Contains (to)) {
				//Implement movement
				LcdConsole.WriteLine ("Moving");
			} else {
				LcdConsole.WriteLine ("Impossibru! moving from/to unknown place");
			}

		}
	}
}