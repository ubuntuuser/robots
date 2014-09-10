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
using System.Collections.Generic;

namespace Reachstacker
{
	public class Reachstacker
	{
		private volatile bool stop = false;
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
		private List<string> possiblePlaces = new List<string> () {
			"Start",
			"InFrontOfTruck",
			"InFrontOfContainerRow1",
			"InFrontOfContainerRow2"
		};

		public Reachstacker ()
		{
			init ();

			turn (300, 5000, 30);
			Thread.Sleep (3000);
			turn (300, 5000, -30);
		}

		private void init ()
		{
			motorFwd = new Motor (MotorPort.OutB);
			motorTurn = new Motor (MotorPort.OutC);
			motorSwitch = new Motor (MotorPort.OutA);
			motorExtend = new Motor (MotorPort.OutD);
			stopMotors ();
		}

		public void listen ()
		{
			Thread tcpListenerThread = new Thread (listenOnTcp);
			Thread worker = new Thread (processMessagestack);
			tcpListenerThread.Start ();
			worker.Start ();
			tcpListenerThread.Join ();
			worker.Join ();
		}

		private void listenOnTcp ()
		{
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

		private void processMessagestack ()
		{
			Console.WriteLine (">> processMessagestack");
			while (!stop) {
				if (messages.Count > 0) {
					string message = null;
					messages.TryTake (out message);
					if (message != null)
						processMessage (message);
				}
			}
		}

		private void processMessage (string message)
		{
			Console.WriteLine (">> processMessage");
			if (message.Contains ("$")) {
				switch (message.Split ('$') [0]) {
				case "goTo"://goTo$truckStart#truckInLoadingZone#
					LcdConsole.WriteLine ("moving");
					string from = (message.Split ('$') [1]).Split ('#') [0];
					string to = ((message.Split ('$') [1]).Split ('#') [1]).Trim ();
					if (from.Equals ("truckStart") && to.Equals ("truckInLoadingZone")) {
						goFromStartToUnloading ();
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
					//motorTurn.SpeedProfileTime (-30, 1, 10, 1, false);
					turn (300, 2000);
					break;
				case "fwd":
					int unitsfwd = int.Parse (message.Split ('$') [1]);
					move (unitsfwd);
					break;
				case "loop":

					move (7500, 30, true, false);
					uturn (440, 2000);
					move (7500, 30, true, false);
					uturn (440, 2000);
					stopMotors ();
					break;
				case "reset":
					reset ();
					break;
				default:
					LcdConsole.WriteLine ("Unknown Message");
					stop = true;
					break;
				}
			} else {
				stop = true;
				Console.WriteLine (">> Invalid Message, shutting down");
				LcdConsole.WriteLine (">> Invalid Message, shutting down");
			}
		}
		//TODO convert cm to units
		private void move (int waitmiliseconds, int speed = 30, bool fwd = true, bool brake = true)
		{
			motorFwd.SetSpeed ((sbyte)speed);
			Thread.Sleep (waitmiliseconds);
			motorFwd.Off ();
		}

		private void turn (int units, int waitmiliseconds, int fwdspeed = 30, bool fwd = true, byte turnspeed = 30, bool brake = true)
		{
			WaitHandle handle;

			uint rampsteps = (uint)(100);
			uint plateausteps = (uint)(units - 2 * rampsteps);
			//motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () + units, brake);
			Console.WriteLine (DateTime.Now);
			handle = motorTurn.SpeedProfile ((sbyte)turnspeed, rampsteps, plateausteps, rampsteps, brake);
			Console.WriteLine (DateTime.Now);
			motorFwd.SetSpeed ((sbyte)fwdspeed);
//			handle.WaitOne ();
			Thread.Sleep (waitmiliseconds);
			Console.WriteLine (">> waiting " + waitmiliseconds);
			//motorTurn.MoveTo (turnspeed, motorTurn.GetTachoCount () - units, brake);
			Console.WriteLine (DateTime.Now);
			handle = motorTurn.SpeedProfile ((sbyte)(turnspeed * -1), rampsteps, plateausteps, rampsteps, brake);
			Console.WriteLine (DateTime.Now);
			Console.WriteLine (">> waiting 1000");
			Thread.Sleep (1000);
			motorFwd.Brake ();
			motorFwd.Off ();
		}

		private void uturn (int units, int waitmiliseconds, int fwdspeed = 20, bool fwd = true, byte turnspeed = 40, bool brake = false)
		{
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

		private void switchMode ()
		{
			Motor motorSwitch = new Motor (MotorPort.OutA);
			if (extensionMode) {
				LcdConsole.WriteLine ("Switching to height adjustment");
//				motorSwitch.On (30, 1550, true);
				motorSwitch.SpeedProfile (30, 200, 1150, 200, true);
				extensionMode = false;
			} else {
				LcdConsole.WriteLine ("Switching to extension mode");
//				motorSwitch.On (-30, 1500, true);
				motorSwitch.SpeedProfile (-30, 200, 1150, 200, true);
				extensionMode = true;
			}
			motorSwitch.Off ();
		}

		void reset ()
		{
			EV3TouchSensor heightSensor = new EV3TouchSensor (SensorPort.In4);
			if (extensionMode)
				switchMode ();
			motorExtend.SetSpeed (-10);
			while (!heightSensor.IsPressed ()) {
				System.Threading.Thread.Sleep (100);
			}
			motorExtend.Off ();
		}

		private void stopMotors ()
		{
			motorTurn.Off ();
			motorFwd.Off ();
		}

		private void goFromStartToUnloading ()
		{
			move (10000, -30, false);
			turn (380, 2000);
			turn (380, 2000);
			move (4000, -30, true);
		}
	}
}

