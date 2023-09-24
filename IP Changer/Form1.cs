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

            updateNics();
            updateMenu();
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

        private void goButton_Click(object sender, EventArgs e)
        {
            historyObj his = new historyObj();
            his.ip = ipTextbox.Text;
            his.gateway = gatewayTextbox.Text;
            his.subnet = subnetTextbox.Text;

            his.toString = ipTextbox.Text + "/24"; //TODO: properly handle subnet 0 128 192 224 240 248 252 254 255

            if (historyObjs.Length > maxHistory)
                historyObjs = historyObjs.Skip(1).ToArray();

            historyObjs = historyObjs.Concat(new historyObj[] { his }).ToArray();

            this.Hide();
            updateMenu();
            NetworkConfigurator.net_adapters();
        }

        private void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            Console.WriteLine(e.ClickedItem.Text);

            if (e.ClickedItem.Text == "DHCP")
            {
                Console.WriteLine("will change to DHCP");
                _ = NetworkConfigurator.SetDHCP(targetNic);
            }
            else if (e.ClickedItem.Text == "Custom")
            {
                this.Show();
            }
            else if(e.ClickedItem.Text == "Target NIC")
            {

            }
            else
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

            saveButton.Enabled = true;
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
                        ManagementBaseObject setDNS = mo.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
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


