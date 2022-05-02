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
        bool quitIsPressed;
        bool isValid;
        bool isRightFormat;
        float distance;
        double num;

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
            startButton.Enabled = false;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
                serialPort.Close();
            }
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
                    startButton.Enabled = true;
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
            quitIsPressed = true;
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

        public void CreateSerialPortConnection()    //  Open serial connection
        {
            serialPort = new SerialPort();
            serialPort.PortName = comboBox.SelectedItem.ToString();
            serialPort.BaudRate = 9600;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.ReadTimeout = 2000;
            serialPort.WriteTimeout = 2000;
            serialPort.StopBits = StopBits.One;
            serialPort.Open();
            connectButton.Text = "YHDISTETTY";
            connectButton.BackColor = Color.LightGreen;
            connectButton.Enabled = false;
            comboBox.Enabled = false;
        }

        public void GetArduinoValue()    //  Get HC-SR04 value from Arduino
        {
            while (serialPort.IsOpen)
            {
                try
                {
                    value = serialPort.ReadLine();
                    time = DateTime.Now;
                    CheckIfError();
                    CheckIfValidValue();
                    if (isRightFormat && isValid)
                    {
                        textBox.Text = "\r\n" + "Aika: " + time.ToString(("dd-MM-yyyy HH:mm:ss")) + "\r\n\r\n" + "Etäisyys: " + num.ToString();
                    }
                    // Thread.Sleep(1000);
                }
                catch (Exception)
                {
                    connectButton.Text = "EI YHTEYTTÄ";
                    connectButton.BackColor = Color.MistyRose;
                    startButton.Enabled = false;
                    textBox.Text = "";
                    if (quitIsPressed == true)
                    {
                        textBox.Text = "";
                    }
                    else
                    {
                        MessageBox.Show("* Yhteys Arduinoon on katkennut\n\n* Käynnistä ohjelma uudelleen", "Virhe");
                        if (serialPort != null)
                        {
                            serialPort.Dispose();
                            serialPort.Close();
                        }
                    }
                }
            }
        }

        public void CheckIfError()
        {
            try
            {
                int num = 0;
                char invalid = '.';
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] == invalid)
                    {
                        num++;
                        if (num == 2)
                        {
                            textBox.Text = "";
                            isRightFormat = false;
                            //MessageBox.Show("Tästä löytyi pisteitä liikaa", "Virhe");
                        }
                    }
                }

                isRightFormat = true;
            }
            catch (Exception)
            {

            }

        }

        public void CheckIfValidValue()
        {
            try
            {
                distance = (float)Convert.ToDouble(value);
                num = Math.Round(distance, 2);

                if (num < 2 || num > 400)
                {
                    textBox.Text = "";
                    isValid = false;
                }

                isValid = true;
            }
            catch (Exception)
            {

            }
        }

        private async Task ConnectToBroker()
        {
            startButton.Enabled = false;
            var mqttFactory = new MqttFactory();
            IMqttClient client = mqttFactory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer("broker.hivemq.com", 1883)
                .WithCleanSession()
                .Build();

            client.UseConnectedHandler(e =>
            {
                MessageBox.Show("Yhdistetty Broker:iin onnistuneesti.");
            });

            client.UseDisconnectedHandler(e =>
            {
                MessageBox.Show("Yhteys Broker:iin katkaistu.");
            });

            await client.ConnectAsync(options);

            //  Publish message to Broker

            if (client.IsConnected)
            {
                try
                {
                    while (client.IsConnected && serialPort.IsOpen)
                    {
                        await client.PublishAsync(new MqttApplicationMessageBuilder()
                            .WithTopic("arduino/sensor/HC-SR04")
                            .WithPayload(time.ToString() + ',' + num)
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
