﻿//
//  Truck.cs
//
//  Author:
//       Johannes Doblmann <s1310595014@students.fh-hagenberg.at>
//
//  Copyright (c) 2014 Johannes Doblmann
using System;
using MonoBrickFirmware.Movement;
using MonoBrickFirmware.Sound;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Net;
using MonoBrickFirmware.Display;
using MonoBrickFirmware.Sensors;
using System.Collections.Generic;

namespace Reachstacker {
	public class Reachstacker {
		private volatile bool stop = false;
		private volatile bool busy = false;
		private Motor motorFwd;
		private Motor motorTurn;
		private Motor motorSwitch;
		private Motor motorExtend;
		private BlockingCollection<string> messages = new BlockingCollection<string> ();
		private bool extensionMode = false;
		private int maxHeight = 30000;
		//TODO change
		private int heightContainerLevel1 = 100;
		//TODO change
		private int heightContainerLevel2 = 20000;
		private int currExtension;
		private int currHeight;
		private const float P = 4.8f;
		private const float I = 800.1f;
		private const float D = 8.5f;
		private Speaker speaker = new Speaker (50);
		private List<string> possiblePlaces = new List<string> () {
			"Start",
			"InFrontOfTruck",
			"InFrontOfContainerRow1",
			"InFrontOfContainerRow2"
		};

		public Reachstacker () {
			init ();

		}

		private void init () {
			Console.WriteLine (">> Initializing");
			motorFwd = new Motor (MotorPort.OutB);
			motorTurn = new Motor (MotorPort.OutC);
			motorSwitch = new Motor (MotorPort.OutA);
			motorExtend = new Motor (MotorPort.OutD);
			motorFwd.ResetTacho ();
			motorTurn.ResetTacho ();
			motorSwitch.ResetTacho ();
			motorExtend.ResetTacho ();
			currHeight = 0;
			currExtension = 0;
			stopMotors ();
		}

		public void listen () {
			Thread tcpListenerThread = new Thread (listenOnTcp);
			Thread worker = new Thread (processMessagestack);
			tcpListenerThread.Start ();
			worker.Start ();
			tcpListenerThread.Join ();
			worker.Join ();
		}

		private void listenOnTcp () {
			Console.WriteLine (">> ListenOnTcp");
			TcpListener serverSocket = new TcpListener (IPAddress.Any, 8888);
			int requestCount = 0;
			TcpClient clientSocket = default(TcpClient);
			serverSocket.Start ();
			LcdConsole.WriteLine (">> Server started");
			Console.WriteLine (">> Server started");
			clientSocket = serverSocket.AcceptTcpClient ();
			LcdConsole.WriteLine (">> Waiting for connection from client");

			while (!stop) {
				try {
					++requestCount;
					NetworkStream networkStream = clientSocket.GetStream ();
					byte[] bytesFrom = new byte[1000025];
					networkStream.Read (bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
					string dataFromClient = System.Text.Encoding.ASCII.GetString (bytesFrom);
					speaker.Beep ();
					messages.Add (dataFromClient);
					dataFromClient = dataFromClient.Substring (0, dataFromClient.IndexOf ("$"));
					LcdConsole.WriteLine (">> Data: " + dataFromClient);
					string serverResponse = "Server Response " + Convert.ToString (requestCount);
					byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes (serverResponse);
					networkStream.Write (sendBytes, 0, sendBytes.Length);
					networkStream.Flush ();
					LcdConsole.WriteLine (">> " + serverResponse);
					//stop = true;
				} catch (Exception e) {
					LcdConsole.WriteLine (e.ToString ());
					stop = true;
				}
			}
			clientSocket.Close ();
			serverSocket.Stop ();
		}

		private void processMessagestack () {
			bool beeped = true;
			try {
				while (!stop) {
					if (!busy && messages.Count > 0) {
						string message = null;
						messages.TryTake (out message);
						if (message != null)
							processMessage (message);
						beeped = false;
					} else if (!busy && messages.Count == 0 && !beeped) {
						speaker.Beep ();
						Thread.Sleep (200);
						speaker.Beep ();
						beeped = true;
					}
					Thread.Sleep (500);
				}
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
				stop = true;
			}
		}

		private void processMessage (string message) {
			busy = true;
			Console.WriteLine (">> processMessage");
			if (message.Contains ("$")) {
				
				switch (message.Split ('$') [0]) {
				case "turnfwd":
					turn (int.Parse (message.Split ('$') [1]), int.Parse (message.Split ('$') [2]));
					break;
				case "turnbwd":
					turn (int.Parse (message.Split ('$') [1]), int.Parse (message.Split ('$') [2]), 30, false);
					break;
				case "fwd":
					int unitsfwd = int.Parse (message.Split ('$') [1]);
					move (unitsfwd);
					break;
				case "moveTo":
					int unit = int.Parse (message.Split ('$') [1]);
					moveTo (unit);
					break;
				case "switch":
					switchMode ();
					break;
				case "reset":
					reset ();
					break;
				case "lift":
					int units = int.Parse (message.Split ('$') [1]);
					liftTo (units);
					break;
				case "extend":
					units = int.Parse (message.Split ('$') [1]);
					extendTo (units);
					break;
				case "getContainerFromTruck":
					getContainerFromTruck ();
					break;
				case "dropContainerOnTruck":
					dropContainerOnTruck ();
					break;
				case "getContainerFromStorage":
					getContainerFromStorage ();
					break;
				case "dropContainerOnStorage":
					dropContainerOnStorage ();
					break;
				default:
					Console.WriteLine ("Unknown Message");
					//stop = true;
					break;
				}
			} else {
				stop = true;
				reset ();
				Console.WriteLine (">> Invalid Message, shutting down");
				LcdConsole.WriteLine (">> Invalid Message, shutting down");
			}
			busy = false;
		}
		//TODO convert cm to units
		private void move (int units, int speed = 60, bool fwd = true, bool brake = true) {
			int toUnit = 0;
			if (fwd)
				toUnit = motorFwd.GetTachoCount () + units;
			else
				toUnit = motorFwd.GetTachoCount () - units;
			moveTo (toUnit, speed, fwd, brake);
		}

		private void moveTo (int units, int speed = 60, bool fwd = true, bool brake = true) {
			Console.WriteLine ("moving from " + motorFwd.GetTachoCount () + " to " + units);
			PositionPID PID = new PositionPID (motorFwd, units, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorFwd.GetTachoCount ().ToString ());
		}

		private void turn (int units, int waitmiliseconds, int fwdspeed = 20, bool fwd = true, byte turnspeed = 30, bool brake = true) {
			if (!fwd)
				fwdspeed *= -1;
			motorFwd.SetSpeed ((sbyte)fwdspeed);
			motorTurn.SetSpeed (30);
			Thread.Sleep (units);
			motorTurn.Off ();
			Thread.Sleep (waitmiliseconds);
			motorTurn.SetSpeed (-30);
			Thread.Sleep (units);
			motorTurn.Off ();
			motorFwd.Off ();
		}

		private void switchMode () {
			if (extensionMode) {
				Console.WriteLine ("Switching to height adjustment");
				PositionPID PID = new PositionPID (motorSwitch, 0, false, 50, P, I, D, 200);
				PID.Run ().WaitOne ();
				Console.WriteLine (motorSwitch.GetTachoCount ().ToString ());
				extensionMode = false;
			} else {
				Console.WriteLine ("Switching to extension mode");
				PositionPID PID = new PositionPID (motorSwitch, -1500, false, 50, P, I, D, 200);
				PID.Run ().WaitOne ();
				Console.WriteLine (motorSwitch.GetTachoCount ().ToString ());
				extensionMode = true;
			}
			motorSwitch.Off ();
		}

		void reset () {
			moveTo (0);
			liftTo (0);
			if (currExtension != 0)
				extendTo (0);
			if (extensionMode)
				switchMode ();
		}

		private void stopMotors () {
			motorFwd.Off ();
			motorTurn.Off ();
			motorSwitch.Off ();
			motorExtend.Off ();
		}

		private void getContainerFromTruck () {
			liftTo (25000);
			moveTo (2200);
			liftTo (12000);
			moveTo (2270);
			liftTo (25000);
		}

		void dropContainerOnTruck () {
			liftTo (25000);
			moveTo (2270);
			liftTo (12000);
			moveTo (2100);
			liftTo (25000);
		}

		void getContainerFromStorage () {
			liftTo (25000);
			moveTo (1700);
			liftTo (1000);
			moveTo (1770);
			liftTo (15000);
		}

		void dropContainerOnStorage () {
			liftTo (25000);
			moveTo (1670);
			liftTo (1000);
			moveTo (1600);
			liftTo (15000);
		}

		private void liftTo (int unit) {
			if (extensionMode)
				switchMode ();
//			unit *= -1;
			Console.WriteLine ("Current height: " + currHeight + ", new Height: " + unit);
			int actualPosition = unit + currExtension;
			currHeight = unit;
			Console.WriteLine ("moving arm from " + motorExtend.GetTachoCount () + " to " + actualPosition);
			PositionPID PID = new PositionPID (motorExtend, actualPosition, true, 80, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine ("done");
		}

		private void extendTo (int unit) {
			if (!extensionMode)
				switchMode ();
			Console.WriteLine ("Current extension: " + currExtension + ", new Extension: " + unit);
			int actualPosition = unit + currHeight;
			currExtension = unit;
			Console.WriteLine ("moving arm from " + motorExtend.GetTachoCount () + " to " + actualPosition);
			PositionPID PID = new PositionPID (motorExtend, actualPosition, true, 80, P, I, D, 200);
			PID.Run ().WaitOne ();
		}
	}
}

