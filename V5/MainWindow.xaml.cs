using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using DSXTrigger = DSX.V5.Trigger;

namespace DSX.V5
{
    public partial class MainWindow : Window
    {
        // Server
        private UdpClient server;
        private Thread serverThread;
        private volatile bool serverRunning = false;

        // Client
        private UdpClient client;
        private IPEndPoint clientEndPoint;
        private DateTime timeSent;
        private Device currentDevice;

        public MainWindow()
        {
            InitializeComponent();
            SliderTL.ValueChanged += (s, e) => { if (ValTL != null) ValTL.Text = ((int)SliderTL.Value).ToString(); };
            SliderTR.ValueChanged += (s, e) => { if (ValTR != null) ValTR.Text = ((int)SliderTR.Value).ToString(); };
            BtnServerMode_Click(null, null);
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                string ts = DateTime.Now.ToString("HH:mm:ss");
                LogText.Text += $"[{ts}] {msg}\n";
            });
        }

        // ===== MODE SELECTION =====

        private void BtnServerMode_Click(object sender, RoutedEventArgs e)
        {
            BtnServerMode.Style = (Style)FindResource("Acc");
            BtnClientMode.Style = (Style)FindResource("Btn");
        }

        private void BtnClientMode_Click(object sender, RoutedEventArgs e)
        {
            BtnClientMode.Style = (Style)FindResource("Acc");
            BtnServerMode.Style = (Style)FindResource("Btn");
        }

        // ===== SERVER =====

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            if (serverRunning)
            {
                StopServer();
                return;
            }

            if (!int.TryParse(TxtPort.Text.Trim(), out int port))
            {
                Log("Invalid port number");
                return;
            }

            try
            {
                server = new UdpClient(port);
                serverRunning = true;
                BtnStartServer.Content = "Stop";
                BtnStartServer.Style = (Style)FindResource("Red");
                ServerStatus.Text = $"Listening ({port})";
                ServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                SetDot();
                Log($"Server started on port {port}");

                serverThread = new Thread(ServerLoop) { IsBackground = true };
                serverThread.Start();
            }
            catch (Exception ex)
            {
                Log($"Server error: {ex.Message}");
            }
        }

        private void StopServer()
        {
            serverRunning = false;
            server?.Close();
            server = null;
            BtnStartServer.Content = "Start";
            BtnStartServer.Style = (Style)FindResource("Green");
            ServerStatus.Text = "Stopped";
            ServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            SetDot();
            Log("Server stopped");
        }

        private void ServerLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            while (serverRunning)
            {
                try
                {
                    byte[] data = server.Receive(ref remoteEP);
                    string json = Encoding.UTF8.GetString(data);
                    Packet packet = JsonConvert.DeserializeObject<Packet>(json);

                    ServerResponse response = new ServerResponse
                    {
                        Status = "OK",
                        TimeReceived = DateTime.Now.ToString("h:mm:ss tt"),
                        Devices = new System.Collections.Generic.List<Device>()
                    };

                    var types = packet.instructions.Select(x => x.type).Distinct().ToList();

                    foreach (var inst in packet.instructions)
                    {
                        switch (inst.type)
                        {
                            case InstructionType.GetDSXStatus:
                                Log("[Server] GetDSXStatus");
                                response.Devices.Add(new Device
                                {
                                    Index = 0,
                                    DeviceType = DeviceType.DUALSENSE,
                                    ConnectionType = ConnectionType.BLUETOOTH,
                                    BatteryLevel = 95,
                                    IsSupportAT = true, IsSupportLightBar = true,
                                    IsSupportPlayerLED = true, IsSupportMicLED = true
                                });
                                break;
                            case InstructionType.TriggerUpdate:
                                var ti = inst.parameters;
                                Log($"[Server] Trigger: {(int)ti[0]} {ti[1]} {ti[2]}");
                                break;
                            case InstructionType.RGBUpdate:
                                var ri = inst.parameters;
                                Log($"[Server] RGB: ({ri[1]},{ri[2]},{ri[3]})");
                                break;
                            case InstructionType.PlayerLEDNewRevision:
                                Log($"[Server] PlayerLED: {inst.parameters[1]}");
                                break;
                            case InstructionType.MicLED:
                                Log($"[Server] MicLED: {inst.parameters[1]}");
                                break;
                            case InstructionType.TriggerThreshold:
                                Log($"[Server] Threshold: {inst.parameters[1]}={inst.parameters[2]}");
                                break;
                            case InstructionType.ResetToUserSettings:
                                Log($"[Server] Reset: Controller {inst.parameters[0]}");
                                break;
                        }
                    }

                    response.isControllerConnected = response.Devices.Any();
                    byte[] resp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                    server.Send(resp, resp.Length, remoteEP);
                }
                catch { if (!serverRunning) break; }
            }
        }

        // ===== CLIENT =====

        private void BtnConnectDSX_Click(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                client.Close();
                client = null;
                ClientStatus.Text = "Disconnected";
                ClientStatus.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                BtnConnectDSX.Content = "Connect";
                BtnRefresh.IsEnabled = false;
                SetDot();
                Log("Disconnected");
                return;
            }

            int port = 6969;
            try
            {
                string p = System.IO.File.ReadAllText(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DSX", "DSX_UDP_PortNumber.txt")).Trim();
                int.TryParse(p, out port);
            }
            catch { }
            try
            {
                string p = System.IO.File.ReadAllText(@"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt").Trim();
                int.TryParse(p, out port);
            }
            catch { }

            client = new UdpClient();
            clientEndPoint = new IPEndPoint(SharedUtils.Localhost, port);
            ClientStatus.Text = $"Connected ({port})";
            ClientStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            BtnConnectDSX.Content = "Disconnect";
            BtnRefresh.IsEnabled = true;
            SetDot();
            Log($"Client connected to port {port}");

            SendAndReceive(InstructionType.GetDSXStatus, new object[] { });
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            SendAndReceive(InstructionType.GetDSXStatus, new object[] { });
        }

        private void SendAndReceive(InstructionType type, object[] parameters)
        {
            if (client == null) { Log("Not connected"); return; }
            try
            {
                var packet = new Packet
                {
                    instructions = new[] { new Instruction { type = type, parameters = parameters } }
                };
                byte[] data = Encoding.ASCII.GetBytes(SharedUtils.PacketToJson(packet));
                client.Send(data, data.Length, clientEndPoint);
                timeSent = DateTime.Now;

                byte[] resp = client.Receive(ref clientEndPoint);
                var sr = JsonConvert.DeserializeObject<ServerResponse>(Encoding.ASCII.GetString(resp));
                UpdateDevice(sr);
                Log($"Received: {sr.Status}");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        private void SendPacket(Packet packet)
        {
            if (client == null) { Log("Not connected"); return; }
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(SharedUtils.PacketToJson(packet));
                client.Send(data, data.Length, clientEndPoint);
                timeSent = DateTime.Now;

                byte[] resp = client.Receive(ref clientEndPoint);
                var sr = JsonConvert.DeserializeObject<ServerResponse>(Encoding.ASCII.GetString(resp));
                UpdateDevice(sr);
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        private void UpdateDevice(ServerResponse sr)
        {
            if (sr == null) return;
            StatusText.Text = $"Connected | {sr.Status} | {(DateTime.Now - timeSent).TotalMilliseconds:F0}ms";

            if (sr.Devices != null && sr.Devices.Count > 0)
            {
                currentDevice = sr.Devices[0];
                DeviceInfoPanel.Children.Clear();
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AddInfo(g, 0, "Index", currentDevice.Index.ToString());
                AddInfo(g, 0, "Type", currentDevice.DeviceType.ToString());
                AddInfo(g, 0, "Connection", currentDevice.ConnectionType.ToString());
                AddInfo(g, 0, "Mac", currentDevice.MacAddress ?? "N/A");
                AddInfo(g, 1, "AT", currentDevice.IsSupportAT ? "Yes" : "No");
                AddInfo(g, 1, "LightBar", currentDevice.IsSupportLightBar ? "Yes" : "No");
                AddInfo(g, 1, "PlayerLED", currentDevice.IsSupportPlayerLED ? "Yes" : "No");
                AddInfo(g, 1, "MicLED", currentDevice.IsSupportMicLED ? "Yes" : "No");
                DeviceInfoPanel.Children.Add(g);

                BatteryBar.Value = currentDevice.BatteryLevel;
                BatteryText.Text = $"{currentDevice.BatteryLevel}%";
                BatteryText.Foreground = currentDevice.BatteryLevel > 50
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                    : currentDevice.BatteryLevel > 20
                        ? new SolidColorBrush(Color.FromRgb(255, 193, 7))
                        : new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }

        private void AddInfo(Grid grid, int col, string label, string value)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)), FontSize = 9 });
            sp.Children.Add(new TextBlock { Text = value, Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = FontWeights.SemiBold });
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }

        private void SetDot()
        {
            bool any = serverRunning || client != null;
            Dot.Fill = new SolidColorBrush(any ? Color.FromRgb(76, 175, 80) : Color.FromRgb(85, 85, 85));
            DotLabel.Text = any ? "Active" : "Idle";
        }

        // ===== CONTROLS =====

        private void SliderRGB_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ValR != null) ValR.Text = ((int)SliderR.Value).ToString();
            if (ValG != null) ValG.Text = ((int)SliderG.Value).ToString();
            if (ValB != null) ValB.Text = ((int)SliderB.Value).ToString();
            if (ColorPreview != null)
                ColorPreview.Fill = new SolidColorBrush(Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value));
        }

        private TriggerMode GetMode(ComboBox c)
        {
            return c.SelectedIndex switch
            {
                0 => TriggerMode.Normal, 1 => TriggerMode.VerySoft,
                2 => TriggerMode.Soft, 3 => TriggerMode.Hard,
                4 => TriggerMode.VeryHard, 5 => TriggerMode.Hardest,
                6 => TriggerMode.Rigid, 7 => TriggerMode.Bow,
                8 => TriggerMode.Galloping, 9 => TriggerMode.SemiAutomaticGun,
                10 => TriggerMode.AutomaticGun, 11 => TriggerMode.Machine,
                12 => TriggerMode.Resistance, 13 => TriggerMode.VibrateTrigger,
                _ => TriggerMode.Normal,
            };
        }

        private void BtnApplyRGB_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.RGBUpdate, parameters = new object[] { idx, (int)SliderR.Value, (int)SliderG.Value, (int)SliderB.Value, 255 } } } };
            SendPacket(p);
            Log($"RGB: ({(int)SliderR.Value},{(int)SliderG.Value},{(int)SliderB.Value})");
        }

        private void BtnSetL_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var mode = GetMode(ComboL);
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.TriggerUpdate, parameters = new object[] { idx, DSXTrigger.Left, mode } } } };
            SendPacket(p);
            Log($"Left: {mode}");
        }

        private void BtnSetR_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var mode = GetMode(ComboR);
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.TriggerUpdate, parameters = new object[] { idx, DSXTrigger.Right, mode } } } };
            SendPacket(p);
            Log($"Right: {mode}");
        }

        private void BtnSetPlayer_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var led = (PlayerLEDNewRevision)ComboPlayer.SelectedIndex;
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.PlayerLEDNewRevision, parameters = new object[] { idx, led } } } };
            SendPacket(p);
            Log($"Player LED: {led}");
        }

        private void BtnSetMic_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var mic = (MicLEDMode)ComboMic.SelectedIndex;
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.MicLED, parameters = new object[] { idx, mic } } } };
            SendPacket(p);
            Log($"Mic LED: {mic}");
        }

        private void BtnThreshold_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var p = new Packet { instructions = new[]
            {
                new Instruction { type = InstructionType.TriggerThreshold, parameters = new object[] { idx, DSXTrigger.Left, (int)SliderTL.Value } },
                new Instruction { type = InstructionType.TriggerThreshold, parameters = new object[] { idx, DSXTrigger.Right, (int)SliderTR.Value } }
            } };
            SendPacket(p);
            Log($"Threshold L={((int)SliderTL.Value)} R={((int)SliderTR.Value)}");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            int idx = currentDevice?.Index ?? 0;
            var p = new Packet { instructions = new[] { new Instruction { type = InstructionType.ResetToUserSettings, parameters = new object[] { idx } } } };
            SendPacket(p);
            Log("Reset to user settings");
        }
    }
}
