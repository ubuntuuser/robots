//
//  Truck.cs
//
//  Author:
//       Johannes Doblmann <s1310595014@students.fh-hagenberg.at>
//
//  Copyright (c) 2014 Johannes Doblmann
using System;
using MonoBrickFirmware.Movement;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Net;
using MonoBrickFirmware.Display;
using MonoBrickFirmware.Sensors;

namespace Truck {
	public class Truck {
		private volatile bool stop = false;
		private volatile bool busy = false;
		private Motor motorFwd;
		private Motor motorTurn;
		private BlockingCollection<string> messages = new BlockingCollection<string> ();
		private const float P = 0.8f;
		private const float I = 1800.1f;
		private const float D = 0.5f;

		public Truck () {
			init ();
		}

		private void init () {
			motorFwd = new Motor (MotorPort.OutA);
			motorTurn = new Motor (MotorPort.OutB);
			motorFwd.ResetTacho ();
			motorTurn.ResetTacho ();

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
			while (!stop) {
				if (!busy && messages.Count > 0) {
					string message = null;
					messages.TryTake (out message);
					if (message != null)
						processMessage (message);
				}
			}
		}

		private void processMessage (string message) {
			busy = true;
			if (message.Contains ("$")) {
				switch (message.Split ('$') [0]) {
				case "goTo"://goTo$truckStart#truckInLoadingZone#
					LcdConsole.WriteLine ("moving");
					string from = (message.Split ('$') [1]).Split ('#') [0];
					string to = ((message.Split ('$') [1]).Split ('#') [1]).Trim ();
					if (from.Equals ("truckStart") && to.Equals ("truckInLoadingZone")) {
						goToUnloading ();
					} else if (to.Equals ("truckStart") && from.Equals ("truckInLoadingZone")) {

					} else {
						LcdConsole.WriteLine ("Route not possible");
						LcdConsole.WriteLine ("From:" + from);
						LcdConsole.WriteLine (from.Equals ("truckStart").ToString ());
						LcdConsole.WriteLine ("To:" + to + ".");
						LcdConsole.WriteLine (to.Equals ("truckInLoadingZone").ToString ());
					}
					break;
				case "turn":
//					int units = int.Parse ((message.Split ('$') [1]).Split ('#') [0]);
//					int wait = int.Parse ((message.Split ('$') [1]).Split ('#') [1]);
//					turn (units, wait);
					motorTurn.SpeedProfileTime (-30, 1, 10, 1, false);
					break;
				case "fwd":
					int unitsfwd = int.Parse (message.Split ('$') [1]);
					move (unitsfwd);
					break;
				case "moveTo":
					int unit = int.Parse (message.Split ('$') [1]);
					moveTo (unit);
					break;
				case "goToLoading":
					goToUnloading ();
					break;
				case "goToStart":
					goToStart ();
					break;
				case "goToEnd":
					goToEnd ();
					break;
				case "loop":

					move (7500, 30, true, false);
					uturn (440, 2000);
					move (7500, 30, true, false);
					uturn (440, 2000);
					stopMotors ();
					break;
				default:
					LcdConsole.WriteLine ("Unknown Message");
					goToEnd ();
					stop = true;
					break;
				}
			}
			busy = false;
		}
		//TODO convert cm to units
		private void move (int units, int speed = 30, bool fwd = true, bool brake = true) {
			int toUnit = 0;
			if (fwd)
				toUnit = motorFwd.GetTachoCount () - units;
			else
				toUnit = motorFwd.GetTachoCount () + units;
			moveTo (toUnit, speed, fwd, brake);
		}

		private void moveTo (int units, int speed = 30, bool fwd = true, bool brake = true) {
			Console.WriteLine ("moving from " + motorFwd.GetTachoCount () + " to " + units);
			PositionPID PID = new PositionPID (motorFwd, units, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorFwd.GetTachoCount ().ToString ());
		}

		private void turn (int units, int waitmiliseconds, int fwdspeed = 30, bool fwd = true, byte turnspeed = 40, bool brake = false) {
			if (fwd)
				fwdspeed *= -1;
			WaitHandle handle;
			motorFwd.SetSpeed ((sbyte)fwdspeed);
			uint rampsteps = (uint)(units * 0.2);
			uint plateausteps = (uint)(units - 2 * rampsteps);
			//motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () + units, brake);
			handle = motorTurn.SpeedProfile ((sbyte)turnspeed, rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			Thread.Sleep (waitmiliseconds);
			//motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () - units, brake);
			handle = motorTurn.SpeedProfile ((sbyte)(turnspeed * -1), rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			motorFwd.Off ();
		}

		private void uturn (int units, int waitmiliseconds, int fwdspeed = 20, bool fwd = true, byte turnspeed = 40, bool brake = false) {
			if (fwd)
				fwdspeed *= -1;

			WaitHandle handle;
			motorFwd.SetSpeed ((sbyte)fwdspeed);
			uint rampsteps = (uint)(150);
			Console.WriteLine ("rampstep: " + rampsteps);
			uint plateausteps = (uint)(units - 2 * rampsteps);
			Console.WriteLine ("plateausteps: " + plateausteps);
			Console.WriteLine ("Tachocount: " + motorTurn.GetTachoCount ());
			handle = motorTurn.SpeedProfile ((sbyte)turnspeed, rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			Console.WriteLine ("Tachocount: " + motorTurn.GetTachoCount ());
			Thread.Sleep (waitmiliseconds);
//			motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () - units, brake);
			handle = motorTurn.SpeedProfile ((sbyte)(turnspeed * -1), rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			Console.WriteLine ("Tachocount: " + motorTurn.GetTachoCount ());
//			motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () + units, brake);
			handle = motorTurn.SpeedProfile ((sbyte)turnspeed, rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			Console.WriteLine ("Tachocount: " + motorTurn.GetTachoCount ());
			Thread.Sleep (waitmiliseconds);
//			motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () - units, brake);
			handle = motorTurn.SpeedProfile ((sbyte)(turnspeed * -1), rampsteps, plateausteps, rampsteps, brake);
			handle.WaitOne ();
			Console.WriteLine ("Tachocount: " + motorTurn.GetTachoCount ());

			motorFwd.Off ();
		}

		private void stopMotors () {
			motorTurn.Off ();
			motorFwd.Off ();
		}

		private void goToUnloading () {
			moveTo (-1000);
		}

		private void goToStart () {
			moveTo (0);
		}

		private void goToEnd () {
			moveTo (4000);
		}
	}
}

