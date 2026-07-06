using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;

namespace Client
{
    class Program
    {

        static UdpClient client;
        static IPEndPoint endPoint;
        static void Connect()
        {
            client = new UdpClient();
            var portNumber = File.ReadAllText(@"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt");
            endPoint = new IPEndPoint(Triggers.localhost, Convert.ToInt32(portNumber));
        }

        static void Send(Packet data)
        {
            var RequestData = Encoding.ASCII.GetBytes(Triggers.PacketToJson(data));
            client.Send(RequestData, RequestData.Length, endPoint);
        }

        static void Main(string[] args)
        {

            Connect();

            while (true)
            {
                Packet p = new Packet();

                int controllerIndex = 0;

                p.instructions = new Instruction[4];

                // ----------------------------------------------------------------------------------------------------------------------------

                //Normal:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Normal };

                //GameCube:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.GameCube };

                //VerySoft:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VerySoft };

                //Soft:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Soft };

                //Hard:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Hard };

                //VeryHard:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VeryHard };

                //Hardest:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Hardest };

                //Rigid:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Rigid };

                //VibrateTrigger needs 1 param of value from 0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VibrateTrigger, 10 };

                //Choppy:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Choppy };

                //Medium:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Medium };

                //VibrateTriggerPulse:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VibrateTriggerPulse };

                //CustomTriggerValue with CustomTriggerValueMode:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.CustomTriggerValue, CustomTriggerValueMode.PulseAB, 0, 101, 255, 255, 0, 0, 0 };

                //Resistance needs 2 params Start: 0-9 Force:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Resistance, 0, 8 };

                //Bow needs 4 params Start: 0-8 End:0-8 Force:0-8 SnapForce:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Bow, 0, 8, 2, 5 };

                //Galloping needs 5 params Start: 0-8 End:0-9 FirstFoot:0-6 SecondFoot:0-7 Frequency:0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Galloping, 0, 9, 2, 4, 10 };

                //SemiAutomaticGun needs 3 params Start: 2-7 End:0-8 Force:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.SemiAutomaticGun, 2, 7, 8 };

                //AutomaticGun needs 3 params Start: 0-8 End:0-9 StrengthA:0-7 StrengthB:0-7 Frequency:0-255 Period 0-2:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.AutomaticGun, 0, 8, 10 };

                //AutomaticGun needs 6 params Start: 0-9 Strength:0-8 Frequency:0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Machine, 0, 9, 7, 7, 10, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                p.instructions[1].type = InstructionType.RGBUpdate;
                p.instructions[1].parameters = new object[] { controllerIndex, 0, 255, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                // PLAYER LED 1-5 true/false state
                p.instructions[2].type = InstructionType.PlayerLED;
                p.instructions[2].parameters = new object[] { controllerIndex, true, false, true, false, true };

                // ----------------------------------------------------------------------------------------------------------------------------

                // TriggerThreshold needs 2 params LeftTrigger:0-255 RightTrigger:0-255
                p.instructions[3].type = InstructionType.TriggerThreshold;
                p.instructions[3].parameters = new object[] { controllerIndex, Trigger.Right, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                Send(p);

                Console.WriteLine("Press any key to send again");
                Console.ReadKey();
            }
        }
    }
}
