﻿using HslCommunication.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;



/************************************************************************************************
 * 
 *    版权声明：Copyright © 2017 Richard.Hu
 *    
 *    时间：2017年11月18日 10:20:15
 * 
 *************************************************************************************************/





namespace HslCommunication.ModBus
{
    /// <summary>
    /// ModBus的功能码
    /// </summary>
    public enum ModBusFunctionMask
    {
        /// <summary>
        /// 读线圈
        /// </summary>
        ReadCoil = 1,
        /// <summary>
        /// 读离散量
        /// </summary>
        ReadDiscrete = 2,
        /// <summary>
        /// 读保持型寄存器
        /// </summary>
        ReadRegister = 3,
        /// <summary>
        /// 写单个线圈
        /// </summary>
        WriteOneCoil = 5,
        /// <summary>
        /// 写单个寄存器
        /// </summary>
        WriteOneRegister = 6,
        /// <summary>
        /// 写多个线圈
        /// </summary>
        WriteCoil = 0x0F,
        /// <summary>
        /// 写多个寄存器
        /// </summary>
        WriteRegister = 0x10,
    }









    /// <summary>
    /// ModBusTcp的客户端，可以方便的实现指定地点的数据读取和写入
    /// </summary>
    public class ModBusTcpClient : DoubleModeNetBase
    {
        #region Constructor

        /// <summary>
        /// 实例化一个ModBusTcp的客户端，需要指定服务器的地址及端口，默认端口为502
        /// </summary>
        /// <param name="ipAddress">服务器的IP地址</param>
        /// <param name="port">服务器的端口</param>
        /// <param name="station">客户端的站号，可以用来标识不同的客户端，默认255</param>
        public ModBusTcpClient(string ipAddress, int port = 502, byte station = 0xFF)
        {
            serverEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
            simpleHybird = new SimpleHybirdLock();
            this.station = station;
            LogHeaderText = "ModBusTcpClient";
        }


        #endregion

        #region Private Field

        private ushort messageId = 1;                       // 消息头
        private byte station = 0;                           // ModBus的客户端站号
        private SimpleHybirdLock simpleHybird;              // 消息头递增的同步锁

        #endregion

        #region Private Method

        /// <summary>
        /// 通过错误码来获取到对应的文本消息
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private string GetDescriptionByErrorCode( byte code )
        {
            switch (code)
            {
                case 0x01: return "不支持该功能码";
                case 0x02: return "越界";
                case 0x03: return "寄存器数量超出范围";
                case 0x04: return "读写异常";
                default: return "未知异常";
            }
        }

        // 获取消息id，每条指令都递增一
        private ushort GetMessageId()
        {
            ushort result = 0;
            simpleHybird.Enter();
            result = messageId;
            if (messageId == ushort.MaxValue)
            {
                messageId = 0;
            }
            else
            {
                messageId++;
            }
            simpleHybird.Leave();
            return result;
        }

        private byte[] BuildReadCommand(ModBusFunctionMask mask, ushort address, ushort length)
        {
            ushort messageId = GetMessageId();
            byte[] buffer = new byte[12];
            buffer[0] = (byte)(messageId / 256);
            buffer[1] = (byte)(messageId % 256);
            buffer[5] = 0x06;
            buffer[6] = station;
            buffer[7] = (byte)mask;
            buffer[8] = (byte)(address / 256);
            buffer[9] = (byte)(address % 256);
            buffer[10] = (byte)(length / 256);
            buffer[11] = (byte)(length % 256);
            return buffer;
        }


        private byte[] BuildWriteOneCoil(ushort address, bool value)
        {
            ushort messageId = GetMessageId();
            byte[] buffer = new byte[12];
            buffer[0] = (byte)(messageId / 256);
            buffer[1] = (byte)(messageId % 256);
            buffer[5] = 0x06;
            buffer[6] = station;
            buffer[7] = (byte)ModBusFunctionMask.WriteOneCoil;
            buffer[8] = (byte)(address / 256);
            buffer[9] = (byte)(address % 256);
            if (value) buffer[10] = 0xFF;
            return buffer;
        }

        private byte[] BuildWriteOneRegister(ushort address, byte[] data)
        {
            ushort messageId = GetMessageId();
            byte[] buffer = new byte[12];
            buffer[0] = (byte)(messageId / 256);
            buffer[1] = (byte)(messageId % 256);
            buffer[5] = 0x06;
            buffer[6] = station;
            buffer[7] = (byte)ModBusFunctionMask.WriteOneRegister;
            buffer[8] = (byte)(address / 256);
            buffer[9] = (byte)(address % 256);
            buffer[10] = data[1];
            buffer[11] = data[0];
            return buffer;
        }



        private byte[] BuildWriteCoil(ushort address, bool[] value)
        {
            byte[] data = BasicFramework.SoftBasic.BoolArrayToByte(value);
            if (data == null) data = new byte[0];

            ushort messageId = GetMessageId();
            byte[] buffer = new byte[13 + data.Length];
            buffer[0] = (byte)(messageId / 256);
            buffer[1] = (byte)(messageId % 256);
            buffer[4] = (byte)((buffer.Length - 6) / 256);
            buffer[5] = (byte)((buffer.Length - 6) % 256);
            buffer[6] = station;
            buffer[7] = (byte)ModBusFunctionMask.WriteCoil;
            buffer[8] = (byte)(address / 256);
            buffer[9] = (byte)(address % 256);
            buffer[10] = (byte)(value.Length / 256);
            buffer[11] = (byte)(value.Length % 256);

            buffer[12] = (byte)(data.Length);

            data.CopyTo(buffer, 13);
            return buffer;
        }

        private byte[] BuildWriteRegister(ushort address, byte[] data)
        {
            if (data == null) data = new byte[0];


            ushort messageId = GetMessageId();
            byte[] buffer = new byte[13 + data.Length];
            buffer[0] = (byte)(messageId / 256);
            buffer[1] = (byte)(messageId % 256);
            buffer[4] = (byte)((buffer.Length - 6) / 256);
            buffer[5] = (byte)((buffer.Length - 6) % 256);
            buffer[6] = station;
            buffer[7] = (byte)ModBusFunctionMask.WriteRegister;
            buffer[8] = (byte)(address / 256);
            buffer[9] = (byte)(address % 256);
            buffer[10] = (byte)(data.Length / 2 / 256);
            buffer[11] = (byte)(data.Length / 2 % 256);

            buffer[12] = (byte)(data.Length);

            data.CopyTo(buffer, 13);

            return buffer;
        }


        /// <summary>
        /// 单数据读取的转换方法
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private OperateResult<byte[]> BytesTransform(OperateResult<byte[]> result)
        {
            if(result.IsSuccess)
            {
                if (result.Content.Length == 4)
                {
                    result.Content = BytesTransform(result.Content, 4);
                }
                else if(result.Content.Length == 8)
                {
                    result.Content = BytesTransform(result.Content, 8);
                }
            }

            return result;
        }


        /// <summary>
        /// 特殊的高低位置换
        /// </summary>
        /// <remarks>
        ///                  ___________________
        ///                 |                   |
        /// 4字节       byte[0]   byte[1]   byte[2]   byte[3]
        ///                          |___________________|
        /// 
        /// </remarks>
        /// <param name="data">字节数据</param>
        /// <param name="length"></param>
        /// <returns>置换后的结果</returns>
        private byte[] BytesTransform(byte[] data, int length)
        {
            int count = data.Length / length;
            for (int i = 0; i < count; i++)
            {
                if(length == 4)
                {
                    byte buffer = data[4 * i + 0];
                    data[4 * i + 0] = data[4 * i + 2];
                    data[4 * i + 2] = buffer;

                    buffer = data[4 * i + 1];
                    data[4 * i + 1] = data[4 * i + 3];
                    data[4 * i + 3] = buffer;
                }
                else if(length == 8)
                {
                    byte buffer = data[8 * i + 0];
                    data[8 * i + 0] = data[8 * i + 6];
                    data[8 * i + 6] = buffer;

                    buffer = data[8 * i + 1];
                    data[8 * i + 1] = data[8 * i + 7];
                    data[8 * i + 7] = buffer;

                    buffer = data[8 * i + 2];
                    data[8 * i + 2] = data[8 * i + 4];
                    data[8 * i + 4] = buffer;

                    buffer = data[8 * i + 3];
                    data[8 * i + 3] = data[8 * i + 5];
                    data[8 * i + 5] = buffer;
                }
            }

            return data;
        }

        #endregion

        #region DoubleModeNetBase Override

        /// <summary>
        /// 接收服务器的反馈数据的规则
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="response"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        protected override bool ReceiveResponse(Socket socket, out byte[] response, OperateResult result)
        {
            try
            {
                byte[] head = NetSupport.ReadBytesFromSocket(socket, 6);

                if (head[4] == 0x00 && head[5] == 0x00)
                {
                    // 数据异常，再接收一个字节，防止有些比较坑的设备新增一个额外的字节来防止读取
                    for (int i = 0; i < head.Length - 1; i++)
                    {
                        head[i] = head[i + 1];
                    }

                    socket.Receive( head, 5, 1, SocketFlags.None );
                }

                int length = head[4] * 256 + head[5];
                byte[] data = NetSupport.ReadBytesFromSocket(socket, length);

                byte[] buffer = new byte[6 + length];
                head.CopyTo(buffer, 0);
                data.CopyTo(buffer, 6);
                response = buffer;
                return true;
            }
            catch(Exception ex)
            {
                LogNet?.WriteException(LogHeaderText, ex);
                socket?.Close();
                response = null;
                result.Message = ex.Message;
                return false;
            }
        }


        #endregion

        #region Read Write Core

        private OperateResult<byte[]> CheckModbusTcpResponse( byte[] send )
        {
            OperateResult<byte[]> result = ReadFromServerCore( send );
            if(result.IsSuccess)
            {
                if ((send[7] + 0x80) == result.Content[7])
                {
                    // 发生了错误
                    result.IsSuccess = false;
                    result.Message = GetDescriptionByErrorCode( result.Content[8] );
                    result.ErrorCode = result.Content[8];
                }
            }
            return result;
        }

        #endregion

        #region Customer Support

        /// <summary>
        /// 读取自定义的数据类型，只要规定了写入和解析规则
        /// </summary>
        /// <typeparam name="T">类型名称</typeparam>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<T> ReadRegister<T>(ushort address) where T : IDataTransfer, new()
        {
            OperateResult<T> result = new OperateResult<T>();
            T Content = new T();
            OperateResult<byte[]> read = ReadRegister(address, Content.ReadCount);
            if(read.IsSuccess)
            {
                Content.ParseSource(read.Content);
                result.Content = Content;
                result.IsSuccess = true;
                result.Message = "Success";
            }
            else
            {
                result.ErrorCode = read.ErrorCode;
                result.Message = read.Message;
            }
            return result;
        }

        /// <summary>
        /// 写入自定义的数据类型到寄存器去，只要规定了生成字节的方法即可
        /// </summary>
        /// <typeparam name="T">类型名称</typeparam>
        /// <param name="address">起始地址</param>
        /// <param name="data">实例对象</param>
        /// <returns></returns>
        public OperateResult WriteRegister<T>(ushort address , T data) where T : IDataTransfer, new()
        {
            return WriteRegister(address, data.ToSource());
        }


        #endregion

        #region Write Support





        /// <summary>
        /// 写单个线圈，对应的功能码0x05
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">False还是True，代表线圈断和通</param>
        /// <returns></returns>
        public OperateResult WriteOneCoil(ushort address, bool value)
        {
            return CheckModbusTcpResponse(BuildWriteOneCoil(address, value));
        }

        /// <summary>
        /// 写入一个寄存器的数据，对应的功能码0x06
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">有符号的数据值</param>
        /// <returns></returns>
        public OperateResult WriteOneRegister(ushort address, short value)
        {
            return CheckModbusTcpResponse( BuildWriteOneRegister(address, BitConverter.GetBytes(value)));
        }

        /// <summary>
        /// 写入一个寄存器的数据，对应的功能码0x06
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">无符号的数据值</param>
        /// <returns></returns>
        public OperateResult WriteOneRegister(ushort address, ushort value)
        {
            return CheckModbusTcpResponse( BuildWriteOneRegister(address, BitConverter.GetBytes(value)));
        }

        /// <summary>
        /// 写入一个寄存器的数据，对应的功能码0x06
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="High">高位的数据</param>
        /// <param name="Low">地位的数据</param>
        /// <returns></returns>
        public OperateResult WriteOneRegister(ushort address, byte High, byte Low)
        {
            return CheckModbusTcpResponse( BuildWriteOneRegister(address, new byte[] { Low, High }));
        }


        /// <summary>
        /// 写线圈数组，线圈数组不能大于2040个，对应的功能码0x0F
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">线圈的数组</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public OperateResult WriteCoil(ushort address, bool[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 2040) throw new ArgumentOutOfRangeException("value", "长度不能大于2040。");
            return CheckModbusTcpResponse( BuildWriteCoil(address, value));
        }

        /// <summary>
        /// 写多个寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">字节数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, byte[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 255) throw new ArgumentOutOfRangeException("value", "长度不能大于255。");
            return CheckModbusTcpResponse( BuildWriteRegister(address, value));
        }


        /// <summary>
        /// 写ASCII字符串到寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">ASCII编码的字符串</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, string value)
        {
            return WriteRegister(address, Encoding.ASCII.GetBytes(value));
        }




        /// <summary>
        /// 写多个寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">short数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, short[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 128) throw new ArgumentOutOfRangeException("value", "长度不能大于128。");


            return CheckModbusTcpResponse( BuildWriteRegister(address, GetBytesFromArray(value, true)));
        }

        /// <summary>
        /// 写short到寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">short数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, short value)
        {
            return WriteRegister(address, new short[] { value });
        }

        /// <summary>
        /// 写多个寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">ushort数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, ushort[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 128) throw new ArgumentOutOfRangeException("value", "长度不能大于128。");
            
            return CheckModbusTcpResponse( BuildWriteRegister(address, GetBytesFromArray(value, true)));
        }


        /// <summary>
        /// 写ushort到寄存器，寄存器的个数不能大于128个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">ushort数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, ushort value)
        {
            return WriteRegister(address, new ushort[] { value });
        }


        /// <summary>
        /// 写多个寄存器的int数组，寄存器的个数不能大于63个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">int数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, int[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 64) throw new ArgumentOutOfRangeException("value", "长度不能大于63。");

            return CheckModbusTcpResponse( BuildWriteRegister( address, BytesTransform( GetBytesFromArray( value, true ), 4 ) ) );
        }


        /// <summary>
        /// 写int到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">int数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, int value)
        {
            return WriteRegister(address, new int[] { value });
        }


        /// <summary>
        /// 写多个寄存器的uint数组，寄存器的个数不能大于63个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">uint数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, uint[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 64) throw new ArgumentOutOfRangeException("value", "长度不能大于63。");

            return CheckModbusTcpResponse( BuildWriteRegister( address, BytesTransform( GetBytesFromArray( value, true ), 4 ) ) );
        }


        /// <summary>
        /// 写uint到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">uint数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, uint value)
        {
            return WriteRegister(address, new uint[] { value });
        }


        /// <summary>
        /// 写多个寄存器的float数组，数组的长度不能大于63个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">float数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, float[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 63) throw new ArgumentOutOfRangeException("value", "长度不能大于63。");

            return CheckModbusTcpResponse( BuildWriteRegister( address, BytesTransform( GetBytesFromArray( value, true ), 4 ) ) );
        }

        /// <summary>
        /// 写float到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">float数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, float value)
        {
            return WriteRegister(address, new float[] { value });
        }


        /// <summary>
        /// 写多个寄存器的double数组，数组的长度不能大于31，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">double数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, double[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 31) throw new ArgumentOutOfRangeException("value", "长度不能大于31。");

            return CheckModbusTcpResponse( BuildWriteRegister( address, BytesTransform( GetBytesFromArray( value, true ), 8 ) ) );
        }

        /// <summary>
        /// 写double到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">double数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, double value)
        {
            return WriteRegister(address, new double[] { value });
        }



        /// <summary>
        /// 写多个寄存器的long数组，数组最大为31个长度，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">long数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, long[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 31) throw new ArgumentOutOfRangeException("value", "长度不能大于31。");

            return CheckModbusTcpResponse( BuildWriteRegister( address, BytesTransform( GetBytesFromArray( value, true ), 8 ) ) );
        }

        /// <summary>
        /// 写long到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">long数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, long value)
        {
            return WriteRegister(address, new long[] { value });
        }

        
        /// <summary>
        /// 写多个寄存器的ulong数组，寄存器的个数不能大于31个，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">ulong数组</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, ulong[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length > 32) throw new ArgumentOutOfRangeException("value", "长度不能大于31。");

            return CheckModbusTcpResponse( BytesTransform(BuildWriteRegister(address, GetBytesFromArray(value, true)), 8));
        }

        /// <summary>
        /// 写ulong到寄存器，对应功能码0x10
        /// </summary>
        /// <param name="address">写入的起始地址</param>
        /// <param name="value">ulong数据</param>
        /// <returns></returns>
        public OperateResult WriteRegister(ushort address, ulong value)
        {
            return WriteRegister(address, new ulong[] { value });
        }



        #endregion

        #region Read Support


        /// <summary>
        /// 读取服务器的数据，需要指定不同的功能码
        /// </summary>
        /// <param name="function"></param>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private OperateResult<byte[]> ReadModBusBase(ModBusFunctionMask function, ushort address, ushort length)
        {
            OperateResult<byte[]> resultBytes = CheckModbusTcpResponse( BuildReadCommand(function, address, length));
            if (resultBytes.IsSuccess)
            {
                // 二次数据处理
                if (resultBytes.Content?.Length >= 9)
                {
                    byte[] buffer = new byte[resultBytes.Content.Length - 9];
                    Array.Copy(resultBytes.Content, 9, buffer, 0, buffer.Length);
                    resultBytes.Content = buffer;
                }
            }
            return resultBytes;
        }

        /// <summary>
        /// 读取服务器的线圈，对应功能码0x01
        /// </summary>
        /// <param name="address">读取的起始地址</param>
        /// <param name="length">读取的数据长度</param>
        /// <returns></returns>
        public OperateResult<byte[]> ReadCoil(ushort address, ushort length)
        {
            return ReadModBusBase(ModBusFunctionMask.ReadCoil, address, length);
        }

        /// <summary>
        /// 读取服务器的离散量，对应的功能码0x02
        /// </summary>
        /// <param name="address">读取的起始地址</param>
        /// <param name="length">读取的数据长度</param>
        /// <returns></returns>
        public OperateResult<byte[]> ReadDiscrete(ushort address, ushort length)
        {
            return ReadModBusBase(ModBusFunctionMask.ReadDiscrete, address, length);
        }

        /// <summary>
        /// 读取服务器的寄存器，对应的功能码0x03
        /// </summary>
        /// <param name="address">读取的起始地址</param>
        /// <param name="length">读取的数据长度</param>
        /// <returns></returns>
        public OperateResult<byte[]> ReadRegister(ushort address, ushort length)
        {
            return ReadModBusBase(ModBusFunctionMask.ReadRegister, address, length);
        }

        
        
        /// <summary>
        /// 读取指定地址的线圈值，并转化成bool
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<bool> ReadBoolCoil(ushort address)
        {
            return GetBoolResultFromBytes(ReadCoil(address, 1));
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成short
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<short> ReadShortRegister(ushort address)
        {
            return GetInt16ResultFromBytes(ReadRegister(address, 1), true);
        }


        /// <summary>
        /// 读取指定地址的寄存器，并转化成short数组
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">读取的short数组的长度</param>
        /// <returns></returns>
        public OperateResult<short[]> ReadShortRegister(ushort address, ushort length)
        {
            short[] result = new short[length];
            OperateResult<byte[]> read = ReadRegister(address, length);
            if(read.IsSuccess)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    byte[] buffer = new byte[2];
                    buffer[0] = read.Content[2 * i + 1];
                    buffer[1] = read.Content[2 * i + 0];
                    result[i] = BitConverter.ToInt16(buffer, 0);
                }
            }

            OperateResult<short[]> temp = new OperateResult<short[]>();
            temp.IsSuccess = read.IsSuccess;
            temp.ErrorCode = read.ErrorCode;
            temp.Message = read.Message;
            temp.Content = result;
            return temp;
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成ushort
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<ushort> ReadUShortRegister(ushort address)
        {
            return GetUInt16ResultFromBytes(ReadRegister(address, 1), true);
        }



        /// <summary>
        /// 读取指定地址的寄存器，并转化成ushort数组
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">读取的ushort数组的长度</param>
        /// <returns></returns>
        public OperateResult<ushort[]> ReadUShortRegister(ushort address, ushort length)
        {
            ushort[] result = new ushort[length];
            OperateResult<byte[]> read = ReadRegister(address, length);
            if(read.IsSuccess)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    byte[] buffer = new byte[2];
                    buffer[0] = read.Content[2 * i + 1];
                    buffer[1] = read.Content[2 * i + 0];
                    result[i] = BitConverter.ToUInt16(buffer, 0);
                }
            }

            OperateResult<ushort[]> temp = new OperateResult<ushort[]>();
            temp.IsSuccess = read.IsSuccess;
            temp.ErrorCode = read.ErrorCode;
            temp.Message = read.Message;
            temp.Content = result;
            return temp;
        }


        /// <summary>
        /// 读取指定地址的寄存器，并转化成int
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<int> ReadIntRegister(ushort address)
        {
            return GetInt32ResultFromBytes(BytesTransform(ReadRegister(address, 2)), true);
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成uint
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<uint> ReadUIntRegister(ushort address)
        {
            return GetUInt32ResultFromBytes(BytesTransform(ReadRegister(address, 2)), true);
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成float
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<float> ReadFloatRegister(ushort address)
        {
            return GetFloatResultFromBytes(BytesTransform(ReadRegister(address, 2)), true);
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成long
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<long> ReadLongRegister(ushort address)
        {
            return GetInt64ResultFromBytes(BytesTransform(ReadRegister(address, 4)), true);
        }


        /// <summary>
        /// 读取指定地址的寄存器，并转化成ulong
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<ulong> ReadULongRegister(ushort address)
        {
            return GetUInt64ResultFromBytes(BytesTransform(ReadRegister(address, 4)), true);
        }

        /// <summary>
        /// 读取指定地址的寄存器，并转化成double
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <returns></returns>
        public OperateResult<double> ReadDoubleRegister(ushort address)
        {
            return GetDoubleResultFromBytes(BytesTransform(ReadRegister(address, 4)), true);
        }

        /// <summary>
        /// 读取指定地址的String数据，编码为ASCII
        /// </summary>
        /// <param name="address">起始地址的字符串形式</param>
        /// <param name="length">字符串长度，返回字符串为2倍长度</param>
        /// <returns></returns>
        public OperateResult<string> ReadStringRegister(ushort address, ushort length)
        {
            return GetStringResultFromBytes(ReadRegister(address, length));
        }

        #endregion

        #region Object Override

        /// <summary>
        /// 判断实例是否为同一个
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <summary>
        /// 用作特定类型的哈希函数
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// 获取文本表示的形式
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"ModBusTcpClient[{serverEndPoint}]";
        }

        #endregion


    }
}
