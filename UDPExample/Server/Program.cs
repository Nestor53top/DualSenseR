using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Shared;

namespace PipeExample
{
    class Program
    {
        static bool running;

        static void HandleTriggerInstruction(object[] parameters)
        {
            int controllerIndex = Convert.ToInt32(parameters[0]);

            Trigger trigger =   (Trigger)Convert.ToInt32(parameters[1]);

            TriggerMode mode = (TriggerMode)Convert.ToInt32(parameters[2]);

            if (parameters.Length == 4)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}]");
            }
            else if (parameters.Length == 5)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}]");
            }
            else  if (parameters.Length == 6)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}, {parameters[5]}]");
            }
            else if (parameters.Length == 7)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}, {parameters[5]}, {parameters[6]}]");
            }
            else  if (parameters.Length == 8)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}, {parameters[5]}, {parameters[6]}, {parameters[7]}]");
            }
            else  if (parameters.Length == 9)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}, {parameters[5]}, {parameters[6]}, {parameters[7]}, {parameters[8]}]");
            }
            else  if (parameters.Length == 10)
            {
                Console.WriteLine($"Trigger update [{controllerIndex}]  = [{trigger}] [{mode}] [{parameters[3]}, {parameters[4]}, {parameters[5]}, {parameters[6]}, {parameters[7]}, {parameters[8]}, {parameters[9]}]");
            }

        }

        static void HandleRGBInstruction(object[] parameters)
        {
            int controllerIndex = Convert.ToInt32(parameters[0]);
            byte R = Convert.ToByte(parameters[1]);
            byte G = Convert.ToByte(parameters[2]);
            byte B = Convert.ToByte(parameters[3]);


            Console.WriteLine($"Controller RGB Update: [{controllerIndex}] = [{R}, {G}, {B}]");
        }

        static void HandleTriggerThresholdInstruction(object[] parameters)
        {
            int controllerIndex = Convert.ToInt32(parameters[0]);

            Trigger trigger = (Trigger)Convert.ToInt32(parameters[1]);

            Console.WriteLine($"Trigger Threshold update [{controllerIndex}]  = [{trigger}] [{parameters[2]}]");
        }

        static void HandlePlayerLedInstruction(object[] parameters)
        {
            int controllerIndex = Convert.ToInt32(parameters[0]);

            Console.WriteLine($"Player LED update [{controllerIndex}]  = [{parameters[1]}] [{parameters[2]}] [{parameters[3]}] [{parameters[4]}] [{parameters[5]}]");
        }




        static void StartServer()
        {
            var portNumber = File.ReadAllText(@"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt");

            //Run UDP server on a different thread
            Task.Run(() =>
            {
                running = true;

                var Server = new UdpClient(Convert.ToInt32(portNumber));
                
                while (running)
                {
                    try
                    {
                        var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                        var ClientRequestData = Server.Receive(ref ClientEp);
                        var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

                        if (ClientEp.Address.Equals(Triggers.localhost))
                        {
                            Console.WriteLine("Recived '{0}' from {1}", ClientRequest, ClientEp.Address.ToString());
                            Console.WriteLine("----------");
                            Packet p = Triggers.JsonToPacket(ClientRequest);


                            foreach (Instruction instruction in p.instructions)
                            {

                                switch (instruction.type)
                                {
                                    case InstructionType.TriggerUpdate:
                                        HandleTriggerInstruction(instruction.parameters);
                                        Console.WriteLine("----------");
                                        break;

                                    case InstructionType.RGBUpdate:
                                        HandleRGBInstruction(instruction.parameters);
                                        Console.WriteLine("----------");
                                        break;

                                    case InstructionType.PlayerLED:
                                        HandlePlayerLedInstruction(instruction.parameters);
                                        Console.WriteLine("----------");
                                        break;

                                    case InstructionType.TriggerThreshold:
                                        HandleTriggerThresholdInstruction(instruction.parameters);
                                        Console.WriteLine("----------");
                                        break;
                                }
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    //We dont really need to send a response, but this is how you can if you want
                    //var ResponseData = Encoding.ASCII.GetBytes("Response");
                    //Server.Send(ResponseData, ResponseData.Length, ClientEp);
                }
            });
        }

        static void StopServer()
        {
            running = false;
        }

        static void Main(string[] args)
        {

            StartServer();

            //Stop app from closing
            while (true)
            {
                string input = Console.ReadLine();
            }
        }
    }
}
