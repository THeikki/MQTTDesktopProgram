using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MQTTDesktopProgram
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;
        DateTime time;
        string value;
        public float distance;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                comboBox.Items.Add(port);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
                serialPort.Close();
            }
            //isSaving = false;
            Application.Exit();
        }

        /*
         *  BUTTON ACTIONS   
         * 
        */

        private void connectButton_Click(object sender, EventArgs e)
        {
            if (comboBox.Text != "")
            {
                try
                {
                    CreateSerialPortConnection();
                    comboBox.Enabled = false;
                    Thread t = new Thread(GetArduinoValue);
                    t.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Virhe");
                }
            }
            else
            {
                MessageBox.Show("Valitse yhdistettävä portti ensin!", "Virhe");
            }

        }


        private async void startButton_Click(object sender, EventArgs e)
        {
            await ConnectToBroker();
        }

        private void quitButton_Click(object sender, EventArgs e)
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
                serialPort.Close();
            }

            Application.Exit();
        }

        /*
         *  FUNCTIONS
         * 
         */

        public void CreateSerialPortConnection()    //Open serial connections
        {
            serialPort = new SerialPort();
            serialPort.PortName = comboBox.SelectedItem.ToString();
            serialPort.BaudRate = 9600;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Open();
            connectButton.Text = "YHDISTETTY";
            connectButton.BackColor = Color.LightGreen;
            connectButton.Enabled = false;
            comboBox.Enabled = false;
        }

        public void GetArduinoValue()    //  Get sensor value from Arduino
        {
            while (serialPort.IsOpen)
            {
                try
                {
                    value = serialPort.ReadLine();
                    time = DateTime.Now;
                    CheckIfValidValue();
                    textBox.Text = "\r\n" + "Aika: " + time.ToString(("dd-MM-yyyy HH:mm:ss")) + "\r\n\r\n" + "Etäisyys: " + value.ToString();
                    // Thread.Sleep(1000);
                }
                catch (Exception exe)
                {
                    //MessageBox.Show(exe.Message, "Virhe");
                }
            }
        }

        public void CheckIfValidValue()
        {
            distance = (float)Convert.ToDouble(value);

            if (distance < 2 || distance > 400)
            {
                textBox.Text = "";
                MessageBox.Show("Luku on mittausalueen ulkopuolella!", "Virhe");
            }
        }

        private async Task ConnectToBroker()
        {
            var mqttFactory = new MqttFactory();
            IMqttClient client = mqttFactory.CreateMqttClient();

            var messageBuilder = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer("broker.hivemq.com", 1883)
                .WithCleanSession()
                .Build();

            client.UseConnectedHandler(e =>
            {
                MessageBox.Show("Yhdistetty onnistuneesti.");
            });

            client.UseDisconnectedHandler(e =>
            {
                MessageBox.Show("Yhteys katkaistu.");
            });

            await client.ConnectAsync(messageBuilder);

            if (client.IsConnected)
            {
                try
                {
                    while (true)
                    {
                        await client.PublishAsync(new MqttApplicationMessageBuilder()
                            .WithTopic("arduino/sensor/distance")
                            .WithPayload(time.ToString() + ' ' + value)
                            .WithQualityOfServiceLevel(0)
                            .Build());

                        await Task.Delay(1000);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Virhe");
                }
            }
        }
    }
}
