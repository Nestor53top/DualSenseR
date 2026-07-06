using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DSX.V4.Shared;

namespace DSX.V4.Client
{
    class Program
    {
        static UdpClient client;
        static IPEndPoint endPoint;
        static DateTime TimeSent;
        static List<Device> devices = new List<Device>();

        static void Main(string[] args)
        {
            Connect();
            GetConnectedDevicesFromDSX();

            while (true)
            {
                if (!devices.Any())
                {
                    GetConnectedDevicesFromDSX();
                }
                else
                {
                    for (int i = 0; i < devices.Count; i++)
                    {
                        Packet packet = new Packet();
                        int controllerIndex = devices[i].Index;

                        // Adaptive Triggers
                        packet = AddAdaptiveTriggerToPacket(packet, controllerIndex, Trigger.Left, TriggerMode.AutomaticGun, new List<int> { 0, 8, 10 });
                        packet = AddAdaptiveTriggerToPacket(packet, controllerIndex, Trigger.Right, TriggerMode.Normal, new List<int>());

                        // Trigger Threshold
                        packet = AddTriggerThresholdToPacket(packet, controllerIndex, Trigger.Left, 0);
                        packet = AddTriggerThresholdToPacket(packet, controllerIndex, Trigger.Right, 0);

                        // RGB LED
                        packet = AddRGBToPacket(packet, controllerIndex, 255, 255, 255, 255);

                        // Player LED
                        packet = AddPlayerLEDToPacket(packet, controllerIndex, PlayerLEDNewRevision.One);

                        // Mic LED
                        packet = AddMicLEDToPacket(packet, controllerIndex, MicLEDMode.Pulse);

                        SendDataToDSX(packet);
                    }

                    GetDataFromDSX();
                }

                Console.WriteLine("Press any key to send again\n");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Establishes connection to DSX server. Tries AppData first (v3.1+), falls back to C:\Temp (v2.0).
        /// </summary>
        static void Connect()
        {
            try
            {
                var port = FetchPortNumber();
                Console.WriteLine($"Connecting to Server on Port: {port}\n");
                client = new UdpClient();
                endPoint = new IPEndPoint(Triggers.localhost, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Fetches UDP port number. Tries AppData\Local\DSX\DSX_UDP_PortNumber.txt (v3.1+)
        /// then falls back to C:\Temp\DualSenseX\DualSenseX_PortNumber.txt (v2.0),
        /// then defaults to 6969.
        /// </summary>
        static int FetchPortNumber()
        {
            const int defaultPort = 6969;

            // Try v3.1+ location: AppData\Local\DSX
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DSX", "DSX_UDP_PortNumber.txt");

                if (File.Exists(appDataPath))
                {
                    string content = File.ReadAllText(appDataPath).Trim();
                    if (int.TryParse(content, out int port))
                    {
                        Console.WriteLine($"Port read from AppData: {port}");
                        return port;
                    }
                }
            }
            catch { }

            // Try v2.0 location: C:\Temp\DualSenseX
            try
            {
                string tempPath = @"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt";
                if (File.Exists(tempPath))
                {
                    string content = File.ReadAllText(tempPath).Trim();
                    if (int.TryParse(content, out int port))
                    {
                        Console.WriteLine($"Port read from C:\\Temp: {port}");
                        return port;
                    }
                }
            }
            catch { }

            Console.WriteLine($"Using default port: {defaultPort}");
            return defaultPort;
        }

        static void SendDataToDSX(Packet data)
        {
            try
            {
                var RequestData = Encoding.ASCII.GetBytes(Triggers.PacketToJson(data));
                client.Send(RequestData, RequestData.Length, endPoint);
                TimeSent = DateTime.Now;
                Console.WriteLine($"Instructions Sent at {DateTime.Now}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void GetDataFromDSX()
        {
            Console.WriteLine("Waiting for Server Response...\n");

            try
            {
                byte[] bytesReceivedFromServer = client.Receive(ref endPoint);

                if (bytesReceivedFromServer.Length > 0)
                {
                    ServerResponse ServerResponseJson = JsonConvert.DeserializeObject<ServerResponse>(
                        Encoding.ASCII.GetString(bytesReceivedFromServer, 0, bytesReceivedFromServer.Length));

                    Console.WriteLine("===================================================================");

                    DateTime CurrentTime = DateTime.Now;
                    TimeSpan Timespan = CurrentTime - TimeSent;

                    Console.WriteLine($"Status                  - {ServerResponseJson.Status}");
                    Console.WriteLine($"Time Received           - {ServerResponseJson.TimeReceived}, took: {Timespan.TotalMilliseconds} ms");
                    Console.WriteLine($"isControllerConnected   - {ServerResponseJson.isControllerConnected}");
                    Console.WriteLine($"BatteryLevel            - {ServerResponseJson.BatteryLevel}\n");

                    if (ServerResponseJson.Devices != null)
                    {
                        Console.WriteLine($"Devices Connected to DSX: {ServerResponseJson.Devices.Count}");
                        devices.Clear();

                        foreach (Device device in ServerResponseJson.Devices)
                        {
                            devices.Add(device);
                            Console.WriteLine("-------------------------------");
                            Console.WriteLine($"Controller Index        - {device.Index}");
                            Console.WriteLine($"MacAddress              - {device.MacAddress}");
                            Console.WriteLine($"DeviceType              - {device.DeviceType}");
                            Console.WriteLine($"ConnectionType          - {device.ConnectionType}");
                            Console.WriteLine($"BatteryLevel            - {device.BatteryLevel}");
                            Console.WriteLine($"IsSupportAT             - {device.IsSupportAT}");
                            Console.WriteLine($"IsSupportLightBar       - {device.IsSupportLightBar}");
                            Console.WriteLine($"IsSupportPlayerLED      - {device.IsSupportPlayerLED}");
                            Console.WriteLine($"IsSupportMicLED         - {device.IsSupportMicLED}");
                            Console.WriteLine("-------------------------------\n");
                        }
                    }

                    Console.WriteLine("===================================================================\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void GetConnectedDevicesFromDSX()
        {
            Packet packet = new Packet();
            packet = AddGetDSXStatusToPacket(packet);
            SendDataToDSX(packet);
            GetDataFromDSX();
        }

        // === Helper Methods ===

        static Packet AddAdaptiveTriggerToPacket(Packet packet, int controllerIndex, Trigger trigger, TriggerMode triggerMode, List<int> parameters)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            var combinedParameters = new object[3 + parameters.Count];
            combinedParameters[0] = controllerIndex;
            combinedParameters[1] = trigger;
            combinedParameters[2] = triggerMode;
            for (int i = 0; i < parameters.Count; i++)
                combinedParameters[3 + i] = parameters[i];

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.TriggerUpdate,
                parameters = combinedParameters
            };
            return packet;
        }

        static Packet AddCustomAdaptiveTriggerToPacket(Packet packet, int controllerIndex, Trigger trigger, TriggerMode triggerMode, CustomTriggerValueMode valueMode, List<int> parameters)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            var combinedParameters = new object[4 + parameters.Count];
            combinedParameters[0] = controllerIndex;
            combinedParameters[1] = trigger;
            combinedParameters[2] = triggerMode;
            combinedParameters[3] = valueMode;
            for (int i = 0; i < parameters.Count; i++)
                combinedParameters[4 + i] = parameters[i];

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.TriggerUpdate,
                parameters = combinedParameters
            };
            return packet;
        }

        static Packet AddTriggerThresholdToPacket(Packet packet, int controllerIndex, Trigger trigger, int threshold)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.TriggerThreshold,
                parameters = new object[] { controllerIndex, trigger, threshold }
            };
            return packet;
        }

        static Packet AddRGBToPacket(Packet packet, int controllerIndex, int red, int green, int blue, int brightness)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.RGBUpdate,
                parameters = new object[] { controllerIndex, red, green, blue, brightness }
            };
            return packet;
        }

        static Packet AddPlayerLEDToPacket(Packet packet, int controllerIndex, PlayerLEDNewRevision playerLED)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.PlayerLEDNewRevision,
                parameters = new object[] { controllerIndex, playerLED }
            };
            return packet;
        }

        static Packet AddMicLEDToPacket(Packet packet, int controllerIndex, MicLEDMode micLED)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.MicLED,
                parameters = new object[] { controllerIndex, micLED }
            };
            return packet;
        }

        static Packet AddResetToPacket(Packet packet, int controllerIndex)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.ResetToUserSettings,
                parameters = new object[] { controllerIndex }
            };
            return packet;
        }

        static Packet AddGetDSXStatusToPacket(Packet packet)
        {
            int instCount;
            if (packet.instructions == null)
            {
                packet.instructions = new Instruction[1];
                instCount = 0;
            }
            else
            {
                instCount = packet.instructions.Length;
                Array.Resize(ref packet.instructions, instCount + 1);
            }

            packet.instructions[instCount] = new Instruction
            {
                type = InstructionType.GetDSXStatus,
                parameters = new object[] { }
            };
            return packet;
        }
    }
}
