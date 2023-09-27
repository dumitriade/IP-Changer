using System;
using System.Management;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net.Sockets;


namespace IP_Changer
{
    public partial class Form1 : Form
    {

        public historyObj[] historyObjs = { };
        public nic[] nics = { };
        public string targetNic = "";
        int maxHistory = 5;

        public Form1()
        {
            InitializeComponent();

            Rectangle workingArea = Screen.GetWorkingArea(this);
            this.Location = new Point(workingArea.Right - Size.Width - 50,
                                      workingArea.Bottom - Size.Height - 50);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon1.BalloonTipText = "I am a NotifyIcon Balloon";
            notifyIcon1.BalloonTipTitle = "Welcome Message";
            //            notifyIcon1.ShowBalloonTip(1000);

            this.Hide();
            saveButton.Enabled = false;

            //get current network settings
            getCurrentSettings();

            updateNics();
            updateMenu();

            //test masks
            for (int i = 0; i <= 32; i++)
            {
                Console.WriteLine("/" + i + " = " + NetworkConfigurator.CidrToSubnet(i));
            }

            //test cidr
            for (int i = 0;i <= 36; i++)
            {
                string testString = "192.168.0.1/" + i.ToString();
                Console.WriteLine(testString + ": " + NetworkConfigurator.confirmIP(testString));
            }
        }

        private void getCurrentSettings()
        {
            string hostName = Dns.GetHostName(); // Retrive the Name of HOST
            string myIP = Dns.GetHostByName(hostName).AddressList[0].ToString();
            string myGateway = GetDefaultGateway();

            Console.WriteLine("current settings: " + myIP + ", " + myGateway);

            historyObj h = new historyObj();

            h.ip = myIP;
            h.gateway = myGateway;

            AddToHistory(h);
        }

        public static string GetDefaultGateway()
        {
            var gateway_address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(e => e.OperationalStatus == OperationalStatus.Up)
                .SelectMany(e => e.GetIPProperties().GatewayAddresses)
                .FirstOrDefault();
            if (gateway_address == null) return null;
            return gateway_address.Address.ToString();
        }

        private void updateNics()
        {
            List<NetworkInterface> values = new List<NetworkInterface>();
            foreach (NetworkInterface netInt in NetworkInterface.GetAllNetworkInterfaces())
            {
                nic n = new nic();
                n.name = netInt.Name;
                n.description = netInt.Description;

                nics = nics.Concat(new nic[] { n }).ToArray();

            }
        }
        private void updateMenu()
        {
            contextMenuStrip1.Items.Clear();
            contextMenuStrip1.Items.Add("DHCP");
            contextMenuStrip1.Items.Add("Custom");

            contextMenuStrip1.Items.Add("-");

            if (historyObjs.Length > 0)
            {
                for (int i = 0; i < historyObjs.Length; i++)
                {
                    historyObj h = historyObjs[i];

                    ToolStripMenuItem newMenuItem = new ToolStripMenuItem();

                    newMenuItem.Text = h.toString;
                    newMenuItem.Tag = i;

                    contextMenuStrip1.Items.Add(newMenuItem);

                    newMenuItem.Enabled = targetNic != "";
                }
                contextMenuStrip1.Items.Add("-");
            }

            ToolStripMenuItem item = new ToolStripMenuItem();
            item.Text = "Target NIC";
            item.Tag = -1;

            for(int i = 0; i < nics.Length; i++)
            {
                nic n = nics[i];
                item.DropDownItems.Add(n.name);
                item.DropDownItems[i].Tag = i;
            }
            item.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.nicSelection_ItemClicked);

            contextMenuStrip1.Items.Add(item);
            contextMenuStrip1.Items.Add("Exit");

        }

        private void CheckEnterKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            Console.WriteLine("checking for enter keypress");
            if (e.KeyChar == (char)Keys.Return || e.KeyChar == (char)Keys.Tab)
            {
                if (NetworkConfigurator.confirmIP(ipTextbox.Text))
                {
                    //confirmed IP autofill subnet and gateway

                    if (ipTextbox.Text.Contains("/")){
                        //get CIDR
                        string cidr = ipTextbox.Text.Substring(ipTextbox.Text.LastIndexOf("/", StringComparison.Ordinal) + 1);
                        subnetTextbox.Text = NetworkConfigurator.CidrToSubnet(int.Parse(cidr));

                        gatewayTextbox.Text = NetworkConfigurator.firstIPinSubnet(ipTextbox.Text, subnetTextbox.Text);
                    }
                }
            }
        }

        private void CheckAllFields(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            saveButton.Enabled = (NetworkConfigurator.confirmIP(ipTextbox.Text) == true) && (NetworkConfigurator.confirmIP(subnetTextbox.Text) == true) && (NetworkConfigurator.confirmIP(gatewayTextbox.Text) == true);
        }

        private void goButton_Click(object sender, EventArgs e)
        {
            historyObj his = new historyObj();
            his.ip = ipTextbox.Text;
            his.gateway = gatewayTextbox.Text;
            his.subnet = subnetTextbox.Text;

            string ip = his.ip;
            if (ip.Contains("/")) ip = ip.Substring(0, his.ip.LastIndexOf("/"));

            his.toString = ip + "/" + NetworkConfigurator.SubnetToCIDR(subnetTextbox.Text);

            AddToHistory(his);

            this.Hide();
            updateMenu();
            NetworkConfigurator.net_adapters();
        }

        private void AddToHistory(historyObj h)
        {
            if (historyObjs.Length > maxHistory)
                historyObjs = historyObjs.Skip(1).ToArray();

            if (h.subnet == null)
                h.subnet = "255.255.255.0";

            if (h.ip.Contains("/")) h.ip = h.ip.Substring(0, h.ip.LastIndexOf("/"));

            h.toString = h.ip + "/" + NetworkConfigurator.SubnetToCIDR(h.subnet);
            historyObjs = historyObjs.Concat(new historyObj[] { h }).ToArray();
        }

        private void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            Console.WriteLine(e.ClickedItem.Text);

            if (e.ClickedItem.Text == "DHCP")
            {
                Console.WriteLine("will change to DHCP");
                if (NetworkConfigurator.SetDHCP(targetNic))
                    Console.WriteLine("updated NIC to DHCP");
                else
                    Console.WriteLine("failed to change to DHCP");
            }
            else if (e.ClickedItem.Text == "Custom")
            {
                this.Show();
            }
            else if (e.ClickedItem.Text == "Exit")
            {
                this.Close();
            }
            else if(e.ClickedItem.Text != "Target NIC")
            {
                historyObj h = historyObjs[(int)e.ClickedItem.Tag];

                Console.WriteLine("Will change to " + h.ip + " / " + h.subnet + " / " + h.gateway);

                string dns = dns1Textbox.Text + "," + dns2Textbox.Text;
                NetworkConfigurator.SetIP(targetNic, h.ip, h.subnet, h.gateway);
                NetworkConfigurator.SetDNS(targetNic, dns);
            }
        }

        private void nicSelection_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            nic n = nics[(int)e.ClickedItem.Tag];

            targetNic = n.description;
            updateMenu();
        }

        private void closeButton_Click(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
        private void exitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }

    public class historyObj {
        public string ip { set; get; }
        public string subnet { set; get; }
        public string gateway { set; get; }

        public string toString { set; get; }
    }

    public class nic
    {
        public string name { set; get; }
        public string description { set; get; }
    }

    public static class NetworkConfigurator
    {
        public static System.Collections.Generic.List<NetworkInterface> net_adapters()
        {
            List<NetworkInterface> values = new List<NetworkInterface>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                values.Add(nic);
            }
            return values;
        }

        public static string CidrToSubnet(int bits)
        {
            string result = "";

            if (bits < 0 || bits > 32) return result;

            int octet1, octet2, octet3, octet4;

            octet1 = bits >= 8 ? 8 : bits;
            octet2 = bits >= 16 ? 8 : bits-8;
            octet3 = bits >= 24 ? 8 : bits-16;
            octet4 = bits >= 32 ? 8 : bits - 24;

            if (octet1 < 0) octet1 = 0;
            if (octet2 < 0) octet2 = 0;
            if (octet3 < 0) octet3 = 0;
            if (octet4 < 0) octet4 = 0;

            int[] masks = new int[9] { 0, 128, 192, 224, 240, 248, 252, 254, 255 };

            result = masks[octet1].ToString() + "." + masks[octet2].ToString() + "." + masks[octet3].ToString() + "." + masks[octet4].ToString();

            return result;
        }

        public static UInt32 SubnetToCIDR(string subnetStr)
        {
            IPAddress subnetAddress = IPAddress.Parse(subnetStr);
            byte[] ipParts = subnetAddress.GetAddressBytes();
            UInt32 subnet = 16777216 * Convert.ToUInt32(ipParts[0]) + 65536 * Convert.ToUInt32(ipParts[1]) + 256 * Convert.ToUInt32(ipParts[2]) + Convert.ToUInt32(ipParts[3]);
            UInt32 mask = 0x80000000;
            UInt32 subnetConsecutiveOnes = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!(mask & subnet).Equals(mask)) break;

                subnetConsecutiveOnes++;
                mask = mask >> 1;
            }
            return subnetConsecutiveOnes;
        }

        public static bool confirmIP(string ip)
        {
            Match match = Regex.Match(ip, @"^(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])($|/(\b([1-9]|[12][0-9]|3[0-2])\b))?$");
            if (match.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string firstIPinSubnet(string ip, string subnet)
        {
            string result = "";

            result = ip.Substring(0, ip.LastIndexOf('.')) + ".1";

            return result;
        }

        public static bool SetDHCP(string nicName)
        {
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface networkInterface = interfaces.FirstOrDefault(x => x.Name == nicName);
            string nicDesc = nicName;

            if (networkInterface != null)
            {
                nicDesc = networkInterface.Description;
            }

            foreach (ManagementObject mo in moc)
            {
                if ((bool)mo["IPEnabled"] == true
                    && mo["Description"].Equals(nicDesc) == true)
                {
                    try
                    {
                        ManagementBaseObject newDNS = mo.GetMethodParameters("SetDNSServerSearchOrder");

                        newDNS["DNSServerSearchOrder"] = null;
                        ManagementBaseObject enableDHCP = mo.InvokeMethod("EnableDHCP", null, null);
                        //                        ManagementBaseObject setDNS = mo.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);

                        SetDNS(nicName, "");
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;

        }

        public static void SetIP(string description, string ip, string subnet, string gateway)
        {
            if (description == null || description.Length == 0)
            {
                MessageBox.Show("Please select the target Network Interface Card first.", "This can't possibly work", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string subnetMask = subnet;
            string address = ip;

            var adapterConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
            var networkCollection = adapterConfig.GetInstances();

            foreach (ManagementObject adapter in networkCollection)
            {
                string d = adapter["Description"] as string;
                if (string.Compare(d, description, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    try
                    {
                        // Set DefaultGateway
                        var newGateway = adapter.GetMethodParameters("SetGateways");
                        newGateway["DefaultIPGateway"] = new string[] { gateway };
                        newGateway["GatewayCostMetric"] = new int[] { 1 };

                        // Set IPAddress and Subnet Mask
                        var newAddress = adapter.GetMethodParameters("EnableStatic");
                        newAddress["IPAddress"] = new string[] { address };
                        newAddress["SubnetMask"] = new string[] { subnetMask };

                        adapter.InvokeMethod("EnableStatic", newAddress, null);
                        adapter.InvokeMethod("SetGateways", newGateway, null);

                        Console.WriteLine("Updated to static IP address!");

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unable to Set IP : " + ex.Message);
                    }
                }
            }
        }

        public static void SetDNS(string NIC, string DNS)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    // if you are using the System.Net.NetworkInformation.NetworkInterface you'll need to change this line to if (objMO["Caption"].ToString().Contains(NIC)) and pass in the Description property instead of the name 
                    if (objMO["Description"].Equals(NIC))
                    {
                        try
                        {
                            ManagementBaseObject newDNS =
                                objMO.GetMethodParameters("SetDNSServerSearchOrder");
                            newDNS["DNSServerSearchOrder"] = DNS.Split(',');
                            ManagementBaseObject setDNS =
                                objMO.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
            }
        }
    }
}