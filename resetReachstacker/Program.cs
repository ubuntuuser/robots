using System;
using MonoBrickFirmware;
using MonoBrickFirmware.Display;
using MonoBrickFirmware.Sensors;
using MonoBrickFirmware.Movement;
using MonoBrickFirmware.UserInput;
using System.Threading;

namespace reset {
	class MainClass {
		static bool extensionMode = false;

		public static void Main(string[] args) {
			Console.WriteLine ("Resetting the motors");
			TouchSensor ts = new TouchSensor(SensorPort.In2);
			Motor motorFwd = new Motor(MotorPort.OutB);
			Motor motorTurn = new Motor(MotorPort.OutC);
			Motor motorSwitch = new Motor(MotorPort.OutA);
			Motor motorArm = new Motor(MotorPort.OutD);
			motorArm.On (-60);
			ButtonEvents buts = new ButtonEvents();
			bool keepGoing = true;
			while (keepGoing) {
				buts.EscapePressed += () => { 
					keepGoing = false;
				};
				if (ts.IsPressed ()) {
					keepGoing = false;
				}
			}
			motorArm.Off ();
			switchMode ();
			keepGoing = true;
			TouchSensor ts2 = new TouchSensor(SensorPort.In4);
			motorArm.On (-60);
			while (keepGoing) {
				buts.EscapePressed += () => { 
					keepGoing = false;
				};
				if (ts2.IsPressed ()) {
					keepGoing = false;
				}
			}
			motorArm.Off ();
			switchMode ();
			motorArm.ResetTacho ();
			motorFwd.ResetTacho ();
			motorSwitch.ResetTacho ();
			motorTurn.ResetTacho ();
		}

		public static void switchMode() {
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
	}
}