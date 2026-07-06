using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DSX.V4.Shared;

namespace DSX.V4.Server
{
    class Program
    {
        static UdpClient server;
        static IPEndPoint clientEndPoint;

        static void Main(string[] args)
        {
            Connect();
            Console.WriteLine("Listening for data...\n");

            while (true)
            {
                try
                {
                    byte[] receivedBytes = server.Receive(ref clientEndPoint);
                    Packet receivedPacket = JsonConvert.DeserializeObject<Packet>(
                        Encoding.UTF8.GetString(receivedBytes));

                    ServerResponse serverResponse = new ServerResponse
                    {
                        Status = "OK",
                        TimeReceived = DateTime.Now.ToString("h:mm:ss tt"),
                        Devices = new List<Device>()
                    };

                    List<InstructionType> instructionTypes = receivedPacket.instructions
                        .Select(x => x.type).Distinct().ToList();

                    if (instructionTypes.Contains(InstructionType.GetDSXStatus))
                    {
                        Console.WriteLine($"[GetDSXStatus] from {clientEndPoint}");
                        serverResponse = StatusUpdate(serverResponse);
                    }

                    if (instructionTypes.Contains(InstructionType.TriggerUpdate))
                    {
                        Console.WriteLine($"[TriggerUpdate] from {clientEndPoint}");
                        foreach (var instruction in receivedPacket.instructions
                            .Where(x => x.type == InstructionType.TriggerUpdate))
                        {
                            TriggerUpdate(instruction);
                        }
                    }

                    if (instructionTypes.Contains(InstructionType.RGBUpdate))
                    {
                        Console.WriteLine($"[RGBUpdate] from {clientEndPoint}");
                        foreach (var instruction in receivedPacket.instructions
                            .Where(x => x.type == InstructionType.RGBUpdate))
                        {
                            RGBUpdate(instruction);
                        }
                    }

                    if (instructionTypes.Contains(InstructionType.PlayerLEDNewRevision))
                    {
                        Console.WriteLine($"[PlayerLEDNewRevision] from {clientEndPoint}");
                        foreach (var instruction in receivedPacket.instructions
                            .Where(x => x.type == InstructionType.PlayerLEDNewRevision))
                        {
                            PlayerLED(instruction);
                        }
                    }

                    if (instructionTypes.Contains(InstructionType.MicLED))
                    {
                        Console.WriteLine($"[MicLED] from {clientEndPoint}");
                        foreach (var instruction in receivedPacket.instructions
                            .Where(x => x.type == InstructionType.MicLED))
                        {
                            MicLED(instruction);
                        }
                    }

                    if (instructionTypes.Contains(InstructionType.ResetToUserSettings))
                    {
                        Console.WriteLine($"[ResetToUserSettings] from {clientEndPoint}");
                        foreach (var instruction in receivedPacket.instructions
                            .Where(x => x.type == InstructionType.ResetToUserSettings))
                        {
                            ResetToUserSettings(instruction);
                        }
                    }

                    serverResponse.isControllerConnected = serverResponse.Devices.Any();
                    byte[] serverResponseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(serverResponse));
                    server.Send(serverResponseBytes, serverResponseBytes.Length, clientEndPoint);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Socket error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        static void Connect()
        {
            server = new UdpClient(6969);
            clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Console.WriteLine("Server listening on port 6969\n");
        }

        static ServerResponse StatusUpdate(ServerResponse serverResponse)
        {
            serverResponse.Devices = new List<Device>
            {
                new Device
                {
                    Index = 0,
                    DeviceType = DeviceType.DUALSENSE,
                    ConnectionType = ConnectionType.BLUETOOTH,
                    BatteryLevel = 95,
                    IsSupportAT = true,
                    IsSupportLightBar = true,
                    IsSupportPlayerLED = true,
                    IsSupportMicLED = true
                }
            };
            return serverResponse;
        }

        static void TriggerUpdate(Instruction instruction)
        {
            var controllerIndex = (int)instruction.parameters[0];
            var trigger = (Trigger)instruction.parameters[1];
            var triggerMode = (TriggerMode)instruction.parameters[2];

            Console.WriteLine($"  Controller {controllerIndex}, {trigger}: {triggerMode}");

            if (triggerMode == TriggerMode.CustomTriggerValue)
            {
                var valueMode = (CustomTriggerValueMode)instruction.parameters[3];
                Console.WriteLine($"    CustomValueMode: {valueMode}");
                for (int i = 4; i < instruction.parameters.Length; i++)
                    Console.WriteLine($"    Param[{i - 4}]: {instruction.parameters[i]}");
            }
        }

        static void RGBUpdate(Instruction instruction)
        {
            var controllerIndex = (int)instruction.parameters[0];
            var r = (int)instruction.parameters[1];
            var g = (int)instruction.parameters[2];
            var b = (int)instruction.parameters[3];
            var brightness = (int)instruction.parameters[4];
            Console.WriteLine($"  Controller {controllerIndex}: RGB({r},{g},{b}) Brightness={brightness}");
        }

        static void PlayerLED(Instruction instruction)
        {
            var controllerIndex = (int)instruction.parameters[0];
            var led = (PlayerLEDNewRevision)instruction.parameters[1];
            Console.WriteLine($"  Controller {controllerIndex}: PlayerLED={led}");
        }

        static void MicLED(Instruction instruction)
        {
            var controllerIndex = (int)instruction.parameters[0];
            var micLed = (MicLEDMode)instruction.parameters[1];
            Console.WriteLine($"  Controller {controllerIndex}: MicLED={micLed}");
        }

        static void ResetToUserSettings(Instruction instruction)
        {
            var controllerIndex = (int)instruction.parameters[0];
            Console.WriteLine($"  Controller {controllerIndex}: Reset to user settings");
        }
    }
}
