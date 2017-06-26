using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using Multimedia;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace MOSSimulator
{
    /// <summary>
    /// Делегат, указывающий на функцию для обработки сообщения из UART
    /// </summary>
    public delegate void RcvdMsg(Cmd com);//объявление делегата

    public delegate void ByteBufDelegate(byte[] buf);
    //enum STATE_RX { START, HEADER, DATALENGTH, DATA, CHKSUM }; //состояние приема
    enum STATE_RX { START, ADDRESS, DATALENGTH, CHKSUM1, DATA, CHKSUM2 }; //состояние приема

    public class Uart
    {
        private SerialPort uart;
        private Multimedia.Timer timer;//таймер для ПРИЕМА 
        private long time;
        private STATE_RX state_rx = STATE_RX.START;
        private Cmd command_in;
        int timeout = 50;//50
        const int GSP_PACKET_SIZE = 18;

        bool active;

        byte[] buf;

        /// <summary>
        /// Событие, срабатывает при окончании приема пакета, возникновении ошибки во время приема или таймаута
        /// </summary>
        public event RcvdMsg received;//объявление события

        /// <summary>
        /// Событие, возвращающее отправляемый буфер при его непосредственной отправке в порт
        /// </summary>
        public event ByteBufDelegate showBufferWasSent;

        public Uart()
        {
            uart = new SerialPort();
            uart.DataBits = 8;
            uart.Handshake = System.IO.Ports.Handshake.None;
            uart.StopBits = System.IO.Ports.StopBits.One;
            uart.Parity = System.IO.Ports.Parity.None;
            uart.BaudRate = 921600;
            uart.DataReceived += DataReceived;//подписка на событие приема из SerialPort


            //command_in = new ReceivedCommand(1);

            //timer = new Multimedia.Timer();
            //timer.Stop();
            //timer.Period = 1;
            //timer.Resolution = 0;
            //timer.Tick += timer_Tick;

            active = false;
        }

        public bool Open()
        {
            try
            {
                active = true;
                if (uart.IsOpen)
                {
                    Close();
                    Open();
                }
                else
                    uart.Open();
                return uart.IsOpen;
            }
            catch
            {
                //MessageBox.Show(String.Format("Exception:{1}  \n\r {2}", ex.Message, ex.StackTrace), ex.Source);
                return false;
            }
        }

        public bool Close()
        {
            try
            {
                active = false;
                //while (timer.IsRunning) ;
                //timer.Stop();
                uart.Close();
                return !uart.IsOpen;
            }
            catch
            {
                //MessageBox.Show(String.Format("Exception:{1}  \n\r {2}", ex.Message, ex.StackTrace), ex.Source);
                return false;
            }
        }

        public bool isOpen()
        {
            return uart.IsOpen;
        }

        public Handshake Handshake
        {
            get { return uart.Handshake; }
            set { uart.Handshake = value; }
        }

        public int BaudRate
        {
            get { return uart.BaudRate; }
            set { uart.BaudRate = value; }
        }

        public int DataBits
        {
            get { return uart.DataBits; }
            set { uart.DataBits = value; }
        }

        public Parity Parity
        {
            get { return uart.Parity; }
            set { uart.Parity = value; }
        }

        public StopBits StopBits
        {
            get { return uart.StopBits; }
            set { uart.StopBits = value; }
        }

        public string PortName
        {
            get { return uart.PortName; }
            set { uart.PortName = value; }
        }

        public int ReadBufSize
        {
            get { return uart.ReadBufferSize; }
            set { uart.ReadBufferSize = value; }
        }
        public int WriteBufSize
        {
            get { return uart.WriteBufferSize; }
            set { uart.WriteBufferSize = value; }
        }

        void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte START = 0;
            byte ADDRESS = 0;
            ushort data_length;
            byte[] LENGTH = new byte[2];

            try
            {
                if (state_rx == STATE_RX.START)
                {
                    if (uart.BytesToRead >= 1)
                    {
                        //command_in.START = (byte)uart.ReadByte();
                        START = (byte)uart.ReadByte();
                        if (START == 0x5a)
                            state_rx = STATE_RX.ADDRESS;
                        else
                        {
                            //неправильный стартовый байт
                            command_in = new Cmd();
                            command_in.result = CmdResult.BAD_START_BYTE;
                            //timer.Stop();
                            uart.DiscardInBuffer();
                            //вызов метода uart_received(Cmd com_in) в MainWindow через делегат RcvdMsg для отображения информации
                            received(command_in);
                        }
                    }
                }
                if (state_rx == STATE_RX.ADDRESS)
                {
                    if (uart.BytesToRead >= 1)
                    {
                        ADDRESS = (byte)uart.ReadByte();
                        //command_in.ADDRESS = (byte)uart.ReadByte();

                        state_rx = STATE_RX.DATALENGTH;
                    }
                }
                if (state_rx == STATE_RX.DATALENGTH)
                {
                    if (uart.BytesToRead >= 2)
                    {
                        for (int i = 0; i < 2; i++)
                            LENGTH[i] = (byte)uart.ReadByte();
                        //command_in.LENGTH[i] = (byte)uart.ReadByte();

                        //создание command_in с размером блока данных, полученных в сообщении
                        //*2 так как размер поля данных в сообщении передается в 16-битных словах
                        data_length = BitConverter.ToUInt16(LENGTH, 0);
                        if (data_length * 2 > uart.BytesToRead)
                            throw new OverflowException();
                        command_in = new Cmd(2 * BitConverter.ToUInt16(LENGTH, 0));
                        command_in.START = START;
                        command_in.ADDRESS = ADDRESS;
                        for (int i = 0; i < 2; i++)
                            command_in.LENGTH[i] = LENGTH[i];                      
                        state_rx = STATE_RX.CHKSUM1;
                    }
                }
                if (state_rx == STATE_RX.CHKSUM1)
                {
                    if (uart.BytesToRead >= 2)
                    {
                        byte[] checksum1Arr = new byte[2];
                        checksum1Arr[0] = (byte)uart.ReadByte();
                        checksum1Arr[1] = (byte)uart.ReadByte();

                        command_in.CHECKSUM1 = BitConverter.ToUInt16(checksum1Arr, 0);

                        byte[] b = command_in.GetBufToSend();

                        if (checksum1Arr[0] != b[4] || checksum1Arr[1] != b[5])
                        {
                            command_in.result = CmdResult.BAD_CHKSUM1;
                        }
                        else
                        {
                            state_rx = STATE_RX.DATA;
                        }

                        //timer.Stop();
                        //uart.DiscardInBuffer();
                        //received(command_in);
                    }
                }
                if (state_rx == STATE_RX.DATA)
                {
                    if (uart.BytesToRead >= command_in.DATA.Length)
                    {
                        for (int i = 0; i < command_in.DATA.Length; i++)//длина пакета вх. данных 12 байт для ГСП, 8 для ЛД...
                            command_in.DATA[i] = (byte)uart.ReadByte();
                        state_rx = STATE_RX.CHKSUM2;
                    }
                }
                if (state_rx == STATE_RX.CHKSUM2)//2 байта контр. сумма
                {
                    if (uart.BytesToRead >= 2)
                    {
                        byte[] checksum2Arr = new byte[2];
                        checksum2Arr[0] = (byte)uart.ReadByte();
                        checksum2Arr[1] = (byte)uart.ReadByte();

                        command_in.CHECKSUM2 = BitConverter.ToUInt16(checksum2Arr, 0);

                        byte[] b = command_in.GetBufToSend();

                        if (checksum2Arr[0] != b[GSP_PACKET_SIZE - 2] || checksum2Arr[1] != b[GSP_PACKET_SIZE - 1])
                        {
                            command_in.result = CmdResult.BAD_CHKSUM2;
                        }
                        else
                        {
                            command_in.result = CmdResult.SUCCESS;
                        }

                        //timer.Stop();
                        uart.DiscardInBuffer();
                        received(command_in);//Возбуждаем событие received и передаем ему данные, прием в MainWindow.uart_received() 
                    }
                }
            }
            /*catch (OverflowException)
            {
                Debug.WriteLine("DataReceived() OverflowException error, data_length * 2 > uart.BytesToRead()");
                Debug.WriteLine("time: {0}.", DateTime.Now);
            }
            catch (IOException exc)
            {
                Debug.WriteLine("DataReceived() IOException error", exc.ToString());
                Debug.WriteLine("time: {0}.", DateTime.Now);
                //Console.WriteLine("Error reading data: {0}.",
                //  exc.GetType().Name);
            }*/
            catch (Exception exc)
            {
                Debug.WriteLine("DataReceived() Exception error", exc.ToString());
                Debug.WriteLine("time: {0}.", DateTime.Now);
            }

            /*if (time <= 0)
            {
                command_in.result = CmdResult.TIMEOUT;
                timer.Stop();
                uart.DiscardInBuffer();
                received(command_in);//Возбуждаем событие received и передаем ему данные, прием в MainWindow.uart_received()
            }
            time -= (uint)timer.Period;*/
            return;
        }

        //прием данных по тику таймера обмена 2 мс
        void timer_Tick(object sender, EventArgs e)
        {
            return;//not use data receive/processing on the timer tick since we are using uart.receive event
            if (!active)
            {
                timer.Stop();
                uart.DiscardInBuffer();
                return;
            }
            if (!uart.IsOpen)
            {
                timer.Stop();
                uart.DiscardInBuffer();
                return;
            }
            if (state_rx == STATE_RX.START)
            {
                if (uart.BytesToRead >= 1)
                {
                    command_in.START = (byte)uart.ReadByte();
                    if (command_in.START == 0x5a)
                        state_rx = STATE_RX.ADDRESS;
                    else
                    {
                        //неправильный стартовый байт
                        command_in.result = CmdResult.BAD_START_BYTE;
                        timer.Stop();
                        uart.DiscardInBuffer();
                        //вызов метода uart_received(Cmd com_in) в MainWindow через делегат RcvdMsg для отображения информации
                        received(command_in);
                    }
                }
            }
            if (state_rx == STATE_RX.ADDRESS)
            {
                if (uart.BytesToRead >= 1)
                {                    
                    command_in.ADDRESS = (byte)uart.ReadByte();

                    state_rx = STATE_RX.DATALENGTH;
                }
            }
            if (state_rx == STATE_RX.DATALENGTH)
            {
                if (uart.BytesToRead >= 2)
                {
                    for (int i = 0; i < 2/*command_in.LENGTH.Length*2*/; i++)
                        command_in.LENGTH[i] = (byte)uart.ReadByte();

                    state_rx = STATE_RX.CHKSUM1;
                }
            }
            if (state_rx == STATE_RX.CHKSUM1)
            {
                if (uart.BytesToRead >= 2)
                {
                    byte []checksum1Arr = new byte[2];
                    checksum1Arr[0] = (byte)uart.ReadByte();
                    checksum1Arr[1] = (byte)uart.ReadByte();

                    command_in.CHECKSUM1 = BitConverter.ToUInt16(checksum1Arr, 0);                
                    
                    byte[] b = command_in.GetBufToSend();

                    if (checksum1Arr[0] != b[4] || checksum1Arr[1] != b[5])
                    {
                        command_in.result = CmdResult.BAD_CHKSUM1;
                    }
                    else
                    {
                        state_rx = STATE_RX.DATA;
                    }

                    //timer.Stop();
                    //uart.DiscardInBuffer();
                    //received(command_in);
                }
            }
            if (state_rx == STATE_RX.DATA)
            {
                if (uart.BytesToRead >= command_in.DATA.Length)
                {
                    for (int i = 0; i < 12/*command_in.DATA.Length*/; i++)//должно быть 10 байт, а не 12 ? длина пакета данных 10 байт для ГСП
                        command_in.DATA[i] = (byte)uart.ReadByte();
                    state_rx = STATE_RX.CHKSUM2;
                }
            }
            if (state_rx == STATE_RX.CHKSUM2)
            {
                if (uart.BytesToRead >= 2)
                {
                    byte[] checksum2Arr = new byte[2];
                    checksum2Arr[0] = (byte)uart.ReadByte();
                    checksum2Arr[1] = (byte)uart.ReadByte();

                    command_in.CHECKSUM2 = BitConverter.ToUInt16(checksum2Arr, 0);

                    byte[] b = command_in.GetBufToSend();

                    if (checksum2Arr[0] != b[GSP_PACKET_SIZE - 2] || checksum2Arr[1] != b[GSP_PACKET_SIZE - 1])
                    {
                        command_in.result = CmdResult.BAD_CHKSUM2;
                    }
                    else
                    {
                        command_in.result = CmdResult.SUCCESS;
                    }

                    timer.Stop();
                    uart.DiscardInBuffer();
                    received(command_in);
                }
            }

            if (time <= 0)
            {
                command_in.result = CmdResult.TIMEOUT;
                timer.Stop();
                uart.DiscardInBuffer();
                received(command_in);
            }
            time -= (uint)timer.Period;
            return;
        }

        public void DesetFalse()
        {
            active = false;
        }

        public static void FillComPorts(System.Windows.Controls.ComboBox cbo)
        {
            cbo.Items.Clear();
            string[] portNames = SerialPort.GetPortNames();
            foreach (string s in portNames)
                cbo.Items.Add(s);
            //cbo.Items.AddRange(SerialPort.GetPortNames());
            if (cbo.Items.Count > 0)
                cbo.SelectedItem = cbo.Items[0];
            else
                cbo.Text = "No port!";
        }

        public void SendCommand(CmdPar command_out)
        {
            try
            {
                if (command_out != null)
                {
                    if (!uart.IsOpen) return;

                    uart.DiscardInBuffer();
                    uart.DiscardOutBuffer();

                    buf = command_out.GetBufToSend();
                    uart.Write(buf, 0, buf.Length);
                    showBufferWasSent(buf);

                    //не создавать тут экземпляр command_in, а создавать при приеме сообщения из uart в методе
                    //DataReceived(), т.к. неизвестна длина блока данных
                    //command_in = new Cmd();

                    state_rx = STATE_RX.START;
                    //time = timeout;
                    //timer.Start();
                }

                //if (!uart.IsOpen) uart.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private long CheckSumRFC1071(byte[] buf, int Lenght)
        {
            long CheckSum = 0;
            for (int i = 0; i < Lenght; i += 2)
                CheckSum += (uint)((buf[i] << 8) + buf[i + 1]);
            while ((CheckSum & 0xFFFF0000) != 0)// Если получается неверная контр. сумма, первым делом проверить While
                CheckSum = ((CheckSum & 0xFFFF) + (CheckSum >> 16));
            CheckSum = ~CheckSum;
            return (CheckSum & 0x0000FFFF);
        }

    }
}
