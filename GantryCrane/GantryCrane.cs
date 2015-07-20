using System;
using System.Text;
using MonoBrickFirmware.Movement;
using MonoBrickFirmware.Sound;
using System.Collections.Concurrent;
using System.Threading;
using System.Net.Sockets;
using MonoBrickFirmware.Display;
using System.Net;

namespace Robots.GantryCrane {
	public class GantryCrane {
		private volatile bool stop = false;
		private volatile bool busy = false;
		private MotorSync motorTower;
		private Motor motorTowerB;
		private Motor motorShuttle;
		private Motor motorArm;
		private BlockingCollection<string> messages = new BlockingCollection<string> ();
		private const float P = 0.8f;
		private const float I = 1800.1f;
		private const float D = 0.5f;
		private Speaker speaker = new Speaker (50);

		public GantryCrane () {
			init ();
		}

		private void init () {
			motorTower = new MotorSync (MotorPort.OutB, MotorPort.OutC);
			motorShuttle = new Motor (MotorPort.OutA);
			motorArm = new Motor (MotorPort.OutD);
			motorTowerB = new Motor (MotorPort.OutB);
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
			speaker.Buzz (500);
			clientSocket = serverSocket.AcceptTcpClient ();
			LcdConsole.WriteLine (">> Waiting for connection from client");
			speaker.Buzz (500);
			while (!stop) {
				try {
					++requestCount;
					NetworkStream networkStream = clientSocket.GetStream ();
					byte[] bytesFrom = new byte[1000025];
					networkStream.Read (bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
					string dataFromClient = System.Text.Encoding.ASCII.GetString (bytesFrom);
					messages.Add (dataFromClient);
					speaker.Beep (300);
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

		private string getMessageStack () {
			StringBuilder sb = new StringBuilder ();
			foreach (string message in messages) {
				sb.AppendLine (message);
			}
			return sb.ToString ();
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
					case "reset":
						reset ();
						break;
					case "tower":
						units = int.Parse (message.Split ('$') [1]);
						tower (units);
						break;
					case "getContainerFromShip":
						getContainerFromShip ();
						break;
					case "dropContainerOnShip":
						dropContainerOnShip ();
						break;
					case "getContainerFromTrain":
						getContainerFromTrain ();
						break;
					case "dropContainerOnTrain":
						dropContainerOnTrain ();
						break;
					case "getContainerFromStorage":
						getContainerFromStorage ();
						break;
					case "dropContainerOnStorage":
						dropContainerOnStorage ();
						break;
					default:
						Console.WriteLine ("Unknown Message: " + message);
						//stop = true;
						break;
					}
				} else {
					Console.WriteLine ("Unknown Message: " + message);
					stop = true;
				}
				busy = false;
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
				reset ();
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

		private void arm (int toUnit, int speed = 50, bool brake = true) {
			toUnit *= -1;
			Console.WriteLine ("moving arm from " + motorArm.GetTachoCount () + " to " + toUnit);
			PositionPID PID = new PositionPID (motorArm, toUnit, false, (sbyte)speed, P, I, D, 200);
			PID.Run ().WaitOne ();
			Console.WriteLine (motorArm.GetTachoCount ().ToString ());
		}

		private void tower (int toUnit, int speed = 20, bool brake = true) {
			toUnit *= -1;
			Console.WriteLine ("moving tower from " + motorTowerB.GetTachoCount () + " to " + toUnit);
//			PositionPID PID = new PositionPID (motorTower, toUnit, false, (sbyte)speed, P, I, D, 200);
//			PID.Run ().WaitOne ();
			int units = toUnit - motorTowerB.GetTachoCount ();
			if (units < 0) {
				speed *= -1;
				units *= -1;
			}
			var motorWaitHandle = motorTower.StepSync ((sbyte)speed, 0, (uint)units, true);
			motorWaitHandle.WaitOne ();
			Console.WriteLine (motorTowerB.GetTachoCount ().ToString ());
		}

		private void stopMotors () {
			motorArm.Off ();
			motorShuttle.Off ();
			motorTower.Off ();
		}

		private void getContainerFromShip () {
			shuttle (-250);
			arm (2900);
			shuttle (-150);
			arm (500);
		}

		private void dropContainerOnShip () {
			shuttle (-150);
			arm (2900);
			shuttle (-250);
			arm (500);
		}

		private void getContainerFromTrain () {
			shuttle (-950);
			arm (2600);
			shuttle (-880);
			arm (500);
		}

		private void dropContainerOnTrain () {
			shuttle (-880);
			arm (2600);
			shuttle (-950);
			arm (500);
		}

		private void getContainerFromStorage () {
			shuttle (-2000);
			arm (3300);
			shuttle (-1920);
			arm (500);
		}

		private void dropContainerOnStorage () {
			shuttle (-1920);
			arm (3300);
			shuttle (-2000);
			arm (500);
		}

		void reset () {
			shuttle (0);
			arm (0);
		}
	}
}

