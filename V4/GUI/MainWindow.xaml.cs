using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using DSX.V4.Shared;
using DSXTrigger = DSX.V4.Shared.Trigger;
using DShared = DSX.V4.Shared.Triggers;

namespace DSX.V4.GUI
{
    public partial class MainWindow : Window
    {
        private UdpClient client;
        private IPEndPoint endPoint;
        private DateTime timeSent;
        private bool isConnected = false;
        private Device currentDevice;

        public MainWindow()
        {
            InitializeComponent();
            SliderLeftThreshold.ValueChanged += SliderThreshold_ValueChanged;
            SliderRightThreshold.ValueChanged += SliderThreshold_ValueChanged;
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText.Text += $"[{timestamp}] {message}\n";
        }

        private void SetConnectionStatus(bool connected)
        {
            isConnected = connected;
            if (connected)
            {
                ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                ConnectionText.Text = "Connected";
                StatusText.Text = "Connected to DSX Server";
                BtnConnect.Content = "Disconnect";
                BtnRefresh.IsEnabled = true;
            }
            else
            {
                ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                ConnectionText.Text = "Offline";
                StatusText.Text = "Disconnected";
                BtnConnect.Content = "Connect";
                BtnRefresh.IsEnabled = false;
                currentDevice = null;
                DeviceInfoPanel.Children.Clear();
                DeviceInfoPanel.Children.Add(new TextBlock
                {
                    Text = "No controller connected",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                BatteryBar.Value = 0;
                BatteryText.Text = "--%";
            }
        }

        private int FetchPortNumber()
        {
            const int defaultPort = 6969;
            try
            {
                string appDataPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DSX", "DSX_UDP_PortNumber.txt");
                if (System.IO.File.Exists(appDataPath))
                {
                    string content = System.IO.File.ReadAllText(appDataPath).Trim();
                    if (int.TryParse(content, out int port))
                        return port;
                }
            }
            catch { }
            try
            {
                string tempPath = @"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt";
                if (System.IO.File.Exists(tempPath))
                {
                    string content = System.IO.File.ReadAllText(tempPath).Trim();
                    if (int.TryParse(content, out int port))
                        return port;
                }
            }
            catch { }
            return defaultPort;
        }

        private void SendPacket(Packet packet)
        {
            if (!isConnected) return;
            try
            {
                var data = Encoding.ASCII.GetBytes(DShared.PacketToJson(packet));
                client.Send(data, data.Length, endPoint);
                timeSent = DateTime.Now;
                Log($"Packet sent ({packet.instructions.Length} instructions)");
            }
            catch (Exception ex)
            {
                Log($"Send error: {ex.Message}");
            }
        }

        private ServerResponse ReceiveResponse()
        {
            try
            {
                byte[] bytes = client.Receive(ref endPoint);
                if (bytes.Length > 0)
                {
                    return JsonConvert.DeserializeObject<ServerResponse>(
                        Encoding.ASCII.GetString(bytes));
                }
            }
            catch { }
            return null;
        }

        private void UpdateDeviceInfo(ServerResponse response)
        {
            if (response == null) return;
            TimeSpan elapsed = DateTime.Now - timeSent;
            StatusText.Text = $"Connected | {response.Status} | {elapsed.TotalMilliseconds:F0}ms";

            if (response.Devices != null && response.Devices.Count > 0)
            {
                currentDevice = response.Devices[0];
                DeviceInfoPanel.Children.Clear();

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                AddDeviceInfoRow(grid, 0, "Index", currentDevice.Index.ToString());
                AddDeviceInfoRow(grid, 0, "Type", currentDevice.DeviceType.ToString());
                AddDeviceInfoRow(grid, 0, "Connection", currentDevice.ConnectionType.ToString());
                AddDeviceInfoRow(grid, 0, "Mac", currentDevice.MacAddress ?? "N/A");
                AddDeviceInfoRow(grid, 1, "Adaptive Triggers", currentDevice.IsSupportAT ? "Yes" : "No");
                AddDeviceInfoRow(grid, 1, "Light Bar", currentDevice.IsSupportLightBar ? "Yes" : "No");
                AddDeviceInfoRow(grid, 1, "Player LED", currentDevice.IsSupportPlayerLED ? "Yes" : "No");
                AddDeviceInfoRow(grid, 1, "Mic LED", currentDevice.IsSupportMicLED ? "Yes" : "No");

                DeviceInfoPanel.Children.Add(grid);

                int batt = currentDevice.BatteryLevel;
                BatteryBar.Value = batt;
                BatteryText.Text = $"{batt}%";
                BatteryText.Foreground = batt > 50 ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) :
                                         batt > 20 ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) :
                                                     new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }

        private void AddDeviceInfoRow(Grid grid, int col, string label, string value)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 10
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }

        private Packet CreatePacket()
        {
            return new Packet { instructions = new Instruction[0] };
        }

        private void AddInstruction(Packet packet, InstructionType type, object[] parameters)
        {
            int count = packet.instructions.Length;
            Array.Resize(ref packet.instructions, count + 1);
            packet.instructions[count] = new Instruction { type = type, parameters = parameters };
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                client?.Close();
                SetConnectionStatus(false);
                Log("Disconnected");
                return;
            }
            try
            {
                int port = FetchPortNumber();
                client = new UdpClient();
                endPoint = new IPEndPoint(DShared.localhost, port);
                isConnected = true;
                SetConnectionStatus(true);
                Log($"Connected to port {port}");

                var packet = CreatePacket();
                AddInstruction(packet, InstructionType.GetDSXStatus, new object[] { });
                SendPacket(packet);
                var response = ReceiveResponse();
                UpdateDeviceInfo(response);
            }
            catch (Exception ex)
            {
                Log($"Connection failed: {ex.Message}");
                SetConnectionStatus(false);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.GetDSXStatus, new object[] { });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
        }

        private void SliderRGB_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ValR != null) ValR.Text = ((int)SliderR.Value).ToString();
            if (ValG != null) ValG.Text = ((int)SliderG.Value).ToString();
            if (ValB != null) ValB.Text = ((int)SliderB.Value).ToString();
            if (ColorPreview != null)
            {
                ColorPreview.Fill = new SolidColorBrush(Color.FromRgb(
                    (byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value));
            }
        }

        private void SliderThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ValLeftThreshold != null)
                ValLeftThreshold.Text = ((int)SliderLeftThreshold.Value).ToString();
            if (ValRightThreshold != null)
                ValRightThreshold.Text = ((int)SliderRightThreshold.Value).ToString();
        }

        private void BtnApplyRGB_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.RGBUpdate, new object[]
            {
                idx, (int)SliderR.Value, (int)SliderG.Value, (int)SliderB.Value, 255
            });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"RGB set to ({(int)SliderR.Value}, {(int)SliderG.Value}, {(int)SliderB.Value})");
        }

        private Shared.TriggerMode GetTriggerMode(ComboBox combo)
        {
            return combo.SelectedIndex switch
            {
                0 => Shared.TriggerMode.Normal,
                1 => Shared.TriggerMode.VerySoft,
                2 => Shared.TriggerMode.Soft,
                3 => Shared.TriggerMode.Hard,
                4 => Shared.TriggerMode.VeryHard,
                5 => Shared.TriggerMode.Hardest,
                6 => Shared.TriggerMode.Rigid,
                7 => Shared.TriggerMode.Bow,
                8 => Shared.TriggerMode.Galloping,
                9 => Shared.TriggerMode.SemiAutomaticGun,
                10 => Shared.TriggerMode.AutomaticGun,
                11 => Shared.TriggerMode.Machine,
                12 => Shared.TriggerMode.Resistance,
                13 => Shared.TriggerMode.VibrateTrigger,
                14 => Shared.TriggerMode.VIBRATE_TRIGGER_10Hz,
                _ => Shared.TriggerMode.Normal,
            };
        }

        private void BtnApplyTriggerLeft_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var mode = GetTriggerMode(ComboLeftTrigger);
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.TriggerUpdate, new object[] { idx, DSXTrigger.Left, mode });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"Left trigger: {mode}");
        }

        private void BtnApplyTriggerRight_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var mode = GetTriggerMode(ComboRightTrigger);
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.TriggerUpdate, new object[] { idx, DSXTrigger.Right, mode });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"Right trigger: {mode}");
        }

        private void BtnApplyPlayerLED_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var led = (PlayerLEDNewRevision)ComboPlayerLED.SelectedIndex;
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.PlayerLEDNewRevision, new object[] { idx, led });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"Player LED: {led}");
        }

        private void BtnApplyMicLED_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var mic = (MicLEDMode)ComboMicLED.SelectedIndex;
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.MicLED, new object[] { idx, mic });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"Mic LED: {mic}");
        }

        private void BtnApplyThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.TriggerThreshold, new object[]
                { idx, DSXTrigger.Left, (int)SliderLeftThreshold.Value });
            AddInstruction(packet, InstructionType.TriggerThreshold, new object[]
                { idx, DSXTrigger.Right, (int)SliderRightThreshold.Value });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log($"Thresholds: L={((int)SliderLeftThreshold.Value)} R={((int)SliderRightThreshold.Value)}");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) { Log("Not connected"); return; }
            int idx = currentDevice?.Index ?? 0;
            var packet = CreatePacket();
            AddInstruction(packet, InstructionType.ResetToUserSettings, new object[] { idx });
            SendPacket(packet);
            var response = ReceiveResponse();
            UpdateDeviceInfo(response);
            Log("Reset to user settings");
        }
    }
}
