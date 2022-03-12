﻿using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartSerial
{
    /// <summary>
    /// Serial.xaml 的交互逻辑
    /// </summary>
    public partial class Serial : UserControl
    {
        #region 变量定义

        #region 内部变量
        private SerialPort serial = new SerialPort();

        private DispatcherTimer autoSendTimer = new DispatcherTimer();
        private DispatcherTimer autoDetectionTimer = new DispatcherTimer();

        static UInt32 receiveBytesCount = 0;
        static UInt32 sendBytesCount = 0;

        byte[] receiveBytes = new byte[10 * 1024 * 1024];   //默认10M的字节空间        

        #endregion

        #endregion
        public Serial()
        {
            InitializeComponent();
            GetValuablePortName();

            // 设置自动检测1秒1次
            autoDetectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            autoDetectionTimer.Tick += new EventHandler(AutoDectionTimer_Tick);
            //开启定时器
            autoDetectionTimer.Start();

            //设置状态栏提示
            statusTextBlock.Text = "准备就绪";
        }
        #region 自动更新串口号
        //自动检测串口名
        private void GetValuablePortName()
        {
            //检测有效的串口并添加到combobox
            string[] serialPortName = System.IO.Ports.SerialPort.GetPortNames();

            foreach (string name in serialPortName)
            {
                portNamesCombobox.Items.Add(name);
            }
        }

        //自动检测串口时间到
        private void AutoDectionTimer_Tick(object sender, EventArgs e)
        {

            string[] serialPortName = System.IO.Ports.SerialPort.GetPortNames();

            if (turnOnButton.IsChecked == true)
            {
                //在找到的有效串口号中遍历当前打开的串口号
                foreach (string name in serialPortName)
                {
                    if (serial.PortName == name)
                        return;                 //找到，则返回，不操作               
                }

                //若找不到已打开的串口:表示当前打开的串口已失效
                //按钮回弹
                turnOnButton.IsChecked = false;
                //删除combobox中的名字
                portNamesCombobox.Items.Remove(serial.PortName);
                portNamesCombobox.SelectedIndex = 0;
                //提示消息
                statusTextBlock.Text = "串口已失效！";
            }
            else
            {
                //检查有效串口和combobox中的串口号个数是否不同
                if (portNamesCombobox.Items.Count != serialPortName.Length)
                {
                    //串口数不同，清空combobox
                    portNamesCombobox.Items.Clear();

                    //重新添加有效串口
                    foreach (string name in serialPortName)
                    {
                        portNamesCombobox.Items.Add(name);
                    }
                    portNamesCombobox.SelectedIndex = 0;

                    statusTextBlock.Text = "串口列表已更新！";

                }
            }
        }
        #endregion

        #region 串口配置面板

        //使能或关闭串口配置相关的控件
        private void serialSettingControlState(bool state)
        {
            portNamesCombobox.IsEnabled = state;
            baudRateCombobox.IsEnabled = state;
            parityCombobox.IsEnabled = state;
            dataBitsCombobox.IsEnabled = state;
            stopBitsCombobox.IsEnabled = state;
        }

        //打开串口
        private void TurnOnButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                //配置串口
                serial.PortName = portNamesCombobox.Text;
                serial.BaudRate = Convert.ToInt32(baudRateCombobox.Text);
                serial.Parity = (Parity)Enum.Parse(typeof(System.IO.Ports.Parity), parityCombobox.Text);
                serial.DataBits = Convert.ToInt16(dataBitsCombobox.Text);
                serial.StopBits = (System.IO.Ports.StopBits)Enum.Parse(typeof(System.IO.Ports.StopBits), stopBitsCombobox.Text);

                //设置串口编码为default：获取操作系统的当前 ANSI 代码页的编码。
                serial.Encoding = Encoding.Default;

                //添加串口事件处理
                serial.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(ReceiveData);

                //开启串口
                serial.Open();

                //关闭串口配置面板
                serialSettingControlState(false);

                statusTextBlock.Text = "串口已开启";

                //显示提示文字
                turnOnButton.Content = "关闭串口";

                serialPortStatusEllipse.Fill = Brushes.Red;

                //使能发送面板
                // sendControlBorder.IsEnabled = true;


            }
            catch
            {
                statusTextBlock.Text = "配置串口出错！";
            }

        }


        //关闭串口
        private void TurnOnButton_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                serial.Close();

                //关闭定时器
                autoSendTimer.Stop();

                //使能串口配置面板
                serialSettingControlState(true);

                statusTextBlock.Text = "串口已关闭";

                //显示提示文字
                turnOnButton.Content = "打开串口";

                serialPortStatusEllipse.Fill = Brushes.Gray;
                //使能发送面板
                //sendControlBorder.IsEnabled = false;
            }
            catch
            {

            }

        }

        #endregion

        #region 接收显示窗口

        //接收数据
        private delegate void UpdateUiTextDelegate(byte[] data);
        private void ReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] receiveData = new byte[serial.BytesToRead];
            serial.Read(receiveData, 0, receiveData.Length);
            Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(ShowData), receiveData);
        }

        //显示数据
        private void ShowData(byte[] data)
        {
            string receiveText = System.Text.Encoding.Default.GetString(data);


            //没有关闭数据显示
            if (stopShowingButton.IsChecked == false)
            {
                //字符串显示
                if (hexadecimalDisplayCheckBox.IsChecked == false)
                {
                    receiveTextBox.AppendText(receiveText);
                    receiveBytesCount += (UInt32)data.Length;
                }
                else //16进制显示
                {
                    StringBuilder stringBuilder = new StringBuilder(8096);
                    foreach (byte str in data)
                    {
                        stringBuilder.AppendFormat("{0:X2} ", str);
                        receiveBytes[receiveBytesCount++] = str;
                        if (receiveBytesCount > receiveBytes.Length * 3 / 4)
                        {
                            byte[] nRec = new byte[receiveBytes.Length * 2];
                            Array.Copy(receiveBytes, nRec, receiveBytes.Length);
                            receiveBytes = nRec;
                        }
                    }
                    receiveTextBox.AppendText(stringBuilder.ToString());
                }
            }
            else
            {
                receiveBytesCount += (UInt32)data.Length;
            }

            //更新接收字节数
            statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();
        }

        //设置滚动条显示到末尾
        private void ReceiveTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (receiveTextBox.LineCount >= 50 && autoClearCheckBox.IsChecked == true)
            {

                receiveTextBox.Clear();
            }
            else
            {
                try
                {
                    receiveScrollViewer.ScrollToEnd();
                }
                catch
                {
                }
            }
        }

        #endregion



        #region 接收设置面板

        //清空接收数据
        private void ClearReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            receiveTextBox.Clear();
        }


        #endregion


        #region 发送控制面板

        //发送数据
        private void SerialPortSend()
        {
            if (!serial.IsOpen)
            {
                statusTextBlock.Text = "请先打开串口！";
                return;
            }
            try
            {
                string sendData = sendTextBox.Text;    //复制发送数据

                //字符串发送
                if (hexadecimalSendCheckBox.IsChecked == false)
                {
                    serial.Write(sendData);

                    //更新发送数据计数
                    sendBytesCount += (UInt32)sendData.Length;
                    statusSendByteTextBlock.Text = sendBytesCount.ToString();

                }
                else //十六进制发送
                {
                    try
                    {
                        sendData.Replace("0x", "");   //去掉0x
                        sendData.Replace("0X", "");   //去掉0X
                                                      //  sendData.


                        string[] strArray = sendData.Split(new char[] { ',', '，', '\r', '\n', ' ', '\t' });
                        int decNum = 0;
                        int i = 0;
                        byte[] sendBuffer = new byte[strArray.Length];  //发送数据缓冲区

                        foreach (string str in strArray)
                        {
                            try
                            {
                                decNum = Convert.ToInt16(str, 16);
                                sendBuffer[i] = Convert.ToByte(decNum);
                                i++;
                            }
                            catch
                            {
                                //MessageBox.Show("字节越界，请逐个字节输入！", "Error");                          
                            }
                        }

                        serial.Write(sendBuffer, 0, sendBuffer.Length);

                        //更新发送数据计数
                        sendBytesCount += (UInt32)sendBuffer.Length;
                        statusSendByteTextBlock.Text = sendBytesCount.ToString();

                    }
                    catch //无法转为16进制时
                    {
                        autoSendCheckBox.IsChecked = false;//关闭自动发送
                        statusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                        return;
                    }

                }

            }
            catch
            {

            }

        }

        //手动发送数据
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SerialPortSend();
        }

        //设置自动发送定时器
        private void AutoSendCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            //创建定时器
            autoSendTimer.Tick += new EventHandler(AutoSendTimer_Tick);

            //设置定时时间，开启定时器
            autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
            autoSendTimer.Start();
        }

        //关闭自动发送定时器
        private void AutoSendCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            autoSendTimer.Stop();
        }


        //自动发送时间到
        void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            //发送数据
            SerialPortSend();

            //设置新的定时时间           
            autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));

        }

        private void ClearSendButton_Click(object sender, RoutedEventArgs e)
        {
            sendTextBox.Clear();
        }
        #endregion


        private void SendTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (hexadecimalSendCheckBox.IsChecked == true)
            {
                MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");

                foreach (Match mat in hexadecimalCollection)
                {
                    sendTextBox.AppendText(mat.Value);
                }

                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }

        private void FileOpen(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.FileName = "serialCom";
            openFile.DefaultExt = ".txt";
            openFile.Filter = "TXT文本|*.txt";
            if (openFile.ShowDialog() == true)
            {
                sendTextBox.Text = File.ReadAllText(openFile.FileName, System.Text.Encoding.Default);

                fileNameTextBox.Text = openFile.FileName;
            }
        }

        private void FileSave(object sender, ExecutedRoutedEventArgs e)
        {

            if (receiveTextBox.Text == string.Empty)
            {
                statusTextBlock.Text = "接收区为空，不保存！";
            }
            else
            {
                SaveFileDialog saveFile = new SaveFileDialog();
                saveFile.Filter = "TXT文本|*.txt";
                if (saveFile.ShowDialog() == true)
                {
                    if (hexadecimalDisplayCheckBox.IsChecked == false)
                    {
                        File.AppendAllText(saveFile.FileName, receiveTextBox.Text);
                        statusTextBlock.Text = "保存成功！";
                    }
                    else
                    {
                        byte[] writeData = new byte[receiveBytesCount];
                        Array.Copy(receiveBytes, writeData, receiveBytesCount);
                        File.WriteAllBytes(saveFile.FileName, writeData);
                        statusTextBlock.Text = "保存成功！";
                    }
                }


            }

        }

        private void WindowClosed(object sender, ExecutedRoutedEventArgs e)
        {

        }


        //清空计数
        private void countClearButton_Click(object sender, RoutedEventArgs e)
        {
            //接收、发送计数清零
            receiveBytesCount = 0;
            sendBytesCount = 0;

            //更新数据显示
            statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();
            statusSendByteTextBlock.Text = sendBytesCount.ToString();

        }

        private void StopShowingButton_Checked(object sender, RoutedEventArgs e)
        {
            stopShowingButton.Content = "恢复显示";
        }

        private void StopShowingButton_Unchecked(object sender, RoutedEventArgs e)
        {
            stopShowingButton.Content = "停止显示";
        }

    }
}
