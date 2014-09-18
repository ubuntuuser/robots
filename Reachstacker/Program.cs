//
//  Program.cs
//
//  Author:
//       Johannes Doblmann <s1310595014@students.fh-hagenberg.at>
//
//  Copyright (c) 2014 Johannes Doblmann
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MonoBrickFirmware;
using MonoBrickFirmware.Display;
using MonoBrickFirmware.Movement;
using MonoBrickFirmware.Sensors;
using MonoBrickFirmware.UserInput;

namespace Reachstacker {
	class MainClass {
		public static void Main (string[] args) {
			LcdConsole.WriteLine (">> Start");
			Console.WriteLine (">> Start");

			Reachstacker rs = new Reachstacker ();
			rs.listen ();

			System.Threading.Thread.Sleep (3000);
			LcdConsole.WriteLine (">> Exit");
			Console.WriteLine (">>Exit");
		}

	}
}