using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using ServerCore4;

namespace Server4
{
    class Packet
    {
        public ushort size; //패킷크기를 알 수 있게
        public ushort packetId; //패킷 종류를 구분할 수 있게
    }

    class PlayerInfoReq : Packet
    {
        public long playerId;
    }

    class PlayerInfoOk : Packet
    {
        public int hp;
        public int attack;
    }

    public enum PacketID
    {
        PlayerInfoReq = 1,
        PlayerInfoOk = 2,
    }

    class ClientSession : PacketSession
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected : {endPoint}");

            //Packet packet = new Packet() { size = 100, packetId = 10 };


            //ArraySegment<byte> openSegment = SendBufferHelper.Open(4096);
            //byte[] buffer = BitConverter.GetBytes(packet.size); // 데이터를 바이트 배열로 바꿔주는 함수
            //byte[] buffer2 = BitConverter.GetBytes(packet.packetId);
            //추출한 데이터를 sendBuff에 넣어줌 추출소스배열->목적지로, 시작위치와 크기도 설정
            //Array.Copy(buffer, 0, openSegment.Array, openSegment.Offset, buffer.Length);
            //먼저 보내놓은게 있으니 먼저번 렝스 다음부터가 시작 오프셋
            //Array.Copy(buffer2, 0, openSegment.Array, openSegment.Offset + buffer.Length, buffer2.Length);
            //ArraySegment<byte> sendBuff = SendBufferHelper.Close(buffer.Length + buffer2.Length);

            //Send(sendBuff);

            Thread.Sleep(5000);

            Disconnected();
        }


        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            ushort count = 0;

            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);//해더추출
            count += 2;
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);//패킷아이디추출
            count += 2;

            switch ((PacketID)id)
            {
                case PacketID.PlayerInfoReq:
                    long playerId = BitConverter.ToInt64(buffer.Array, buffer.Offset + count);
                    count += 8;
                    Console.WriteLine($"PlayerInfoReq: {playerId}");
                    break;
                case PacketID.PlayerInfoOk:
                    break;
                default:
                    break;
            }
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
    }
}
