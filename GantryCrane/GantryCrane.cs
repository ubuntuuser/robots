using System;
using MonoBrickFirmware.Movement;
using System.Collections.Concurrent;
using System.Threading;
using System.Net.Sockets;
using MonoBrickFirmware.Display;
using System.Net;

namespace Robots.GantryCrane {
	public class GantryCrane {
		private volatile bool stop = false;
		private volatile bool busy = false;
		private Motor motorTower;
		private Motor motorShuttle;
		private Motor motorArm;
		private BlockingCollection<string> messages = new BlockingCollection<string> ();
		private const float P = 0.8f;
		private const float I = 1800.1f;
		private const float D = 0.5f;

		public GantryCrane () {
			init ();
		}

		private void init () {
			motorTower = new Motor (MotorPort.OutC);
			motorShuttle = new Motor (MotorPort.OutA);
			motorArm = new Motor (MotorPort.OutD);
			motorShuttle.ResetTacho ();
			motorArm.ResetTacho ();
			stopMotors ();
		}

		public void listen () {
			try {
				Thread tcpListenerThread = new Thread (listenOnTcp);
				Thread worker = new Thread (processMessagestack);
				tcpListenerThread.Start ();
				worker.Start ();
				tcpListenerThread.Join ();
				worker.Join ();
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
				stop = true;
			}
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
					Console.WriteLine (e.ToString ());
					stop = true;
				}
			}
			clientSocket.Close ();
			serverSocket.Stop ();
		}

		private void processMessagestack () {
			try {
				while (!stop) {
					if (!busy && messages.Count > 0) {
						string message = null;
						messages.TryTake (out message);
						if (message != null)
							processMessage (message);
					}
					Thread.Sleep (100);
				}
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
				stop = true;
			}
		}

		private void processMessage (string message) {
			try {
				busy = true;
				if (message.Contains ("$")) {
					switch (message.Split ('$') [0]) {
					case "goTo"://goTo$truckStart#truckInLoadingZone#
						LcdConsole.WriteLine ("moving");
						string from = (message.Split ('$') [1]).Split ('#') [0];
						string to = ((message.Split ('$') [1]).Split ('#') [1]).Trim ();
						if (from.Equals ("truckStart") && to.Equals ("truckInLoadingZone")) {
							//goFromStartToUnloading ();
						} else if (to.Equals ("truckStart") && from.Equals ("truckInLoadingZone")) {

						} else {
							LcdConsole.WriteLine ("Route not possible");
							LcdConsole.WriteLine ("From:" + from);
							LcdConsole.WriteLine (from.Equals ("truckStart").ToString ());
							LcdConsole.WriteLine ("To:" + to + ".");
							LcdConsole.WriteLine (to.Equals ("truckInLoadingZone").ToString ());
						}
						break;
					case "arm":
						int units = int.Parse (message.Split ('$') [1]);
						arm (units);
						break;
					case "shuttle":
						units = int.Parse (message.Split ('$') [1]);
						shuttle (units);
						break;
					case "tower":
						units = int.Parse (message.Split ('$') [1]);
						tower (units);
						break;
					default:
						Console.WriteLine ("Unknown Message: " + message);
						stop = true;
						break;
					}
				} else {
					Console.WriteLine ("Unknown Message: " + message);
					stop = true;
				}
				busy = false;
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
				stop = true;
			}
		}
		//TODO convert cm to units
		private void shuttle (int toUnit, int speed = 30, bool brake = true) {
			toUnit *= -1;
			Console.WriteLine ("moving shuttle from " + motorShuttle.GetTachoCount () + " to " + toUnit);
			PositionPID PID = new PositionPID (motorShuttle, toUnit, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorShuttle.GetTachoCount ().ToString ());
		}

		private void arm (int toUnit, int speed = 40, bool brake = true) {
			toUnit *= -1;
			Console.WriteLine ("moving arm from " + motorArm.GetTachoCount () + " to " + toUnit);
			PositionPID PID = new PositionPID (motorArm, toUnit, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorArm.GetTachoCount ().ToString ());
		}

		private void tower (int toUnit, int speed = 30, bool brake = true) {
			toUnit *= -1;
			Console.WriteLine ("moving tower from " + motorTower.GetTachoCount () + " to " + toUnit);
			PositionPID PID = new PositionPID (motorTower, toUnit, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorTower.GetTachoCount ().ToString ());
		}

		private void stopMotors () {
			motorArm.Off ();
			motorShuttle.Off ();
			motorTower.Off ();
		}
	}
}

