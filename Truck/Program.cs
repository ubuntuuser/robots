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

namespace Truck {
	class MainClass {


		public static void Main(string[] args) {
			LcdConsole.WriteLine ("Hello Lcd");

			//---------------------------------
			Truck truck = new Truck();
//			truck.listen ();



			System.Threading.Thread.Sleep (3000);
			LcdConsole.WriteLine (">> Exit");

		}


	}
}
